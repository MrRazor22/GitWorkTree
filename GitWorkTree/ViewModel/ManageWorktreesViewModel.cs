using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using GitWorkTree.Services;
using GitWorkTree.Helpers;
using GitWorkTree.Commands;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.TeamFoundation.Git.Extensibility;

namespace GitWorkTree.ViewModel
{
    public enum ManageWorktreesState
    {
        WarmingUp,
        NoRepository,
        LoadingWorktrees,
        NoSelection,
        WorktreeSelected
    }

    public class WorktreeItemViewModel : BaseViewModel
    {
        public string FullPath { get; }
        public string FolderName { get; }
        public bool IsMain { get; }
        public bool IsCurrent { get; }

        private string _branchName;
        public string BranchName
        {
            get => _branchName;
            set
            {
                if (_branchName != value)
                {
                    _branchName = value;
                    OnPropertyChanged(nameof(BranchName));
                }
            }
        }

        private bool _isLoadingStatus;
        public bool IsLoadingStatus
        {
            get => _isLoadingStatus;
            set
            {
                if (_isLoadingStatus != value)
                {
                    _isLoadingStatus = value;
                    OnPropertyChanged(nameof(IsLoadingStatus));
                }
            }
        }

        private bool _isDirty;
        public bool IsDirty
        {
            get => _isDirty;
            set
            {
                if (_isDirty != value)
                {
                    _isDirty = value;
                    OnPropertyChanged(nameof(IsDirty));
                }
            }
        }

        private bool _hasRefreshError;
        public bool HasRefreshError
        {
            get => _hasRefreshError;
            set
            {
                if (_hasRefreshError != value)
                {
                    _hasRefreshError = value;
                    OnPropertyChanged(nameof(HasRefreshError));
                }
            }
        }

        private string _lastErrorMessage = string.Empty;
        public string LastErrorMessage
        {
            get => _lastErrorMessage;
            set
            {
                if (_lastErrorMessage != value)
                {
                    _lastErrorMessage = value;
                    OnPropertyChanged(nameof(LastErrorMessage));
                }
            }
        }

        private string _errorSummary = string.Empty;
        public string ErrorSummary
        {
            get => _errorSummary;
            set
            {
                if (_errorSummary != value)
                {
                    _errorSummary = value;
                    OnPropertyChanged(nameof(ErrorSummary));
                }
            }
        }

        public DateTime LastStatusCheckUtc { get; set; } = DateTime.MinValue;

        public WorktreeItemViewModel(string fullPath, string folderName, bool isMain, bool isCurrent, string branchName = null, bool isDirty = false)
        {
            FullPath = fullPath;
            FolderName = folderName;
            IsMain = isMain;
            IsCurrent = isCurrent;
            _branchName = branchName;
            _isDirty = isDirty;
        }
    }

    public class HierarchyNode : BaseViewModel
    {
        public string Name { get; set; }
        public bool IsFolder => Items != null && Items.Count > 0;
        public WorktreeItemViewModel WorktreeItem { get; set; }
        public ObservableCollection<HierarchyNode> Items { get; set; }

        public HierarchyNode()
        {
            Items = new ObservableCollection<HierarchyNode>();
        }
    }

    public class GitChangeNode : BaseViewModel
    {
        private bool _isExpanded = true;
        public bool IsExpanded
        {
            get => _isExpanded;
            set
            {
                if (_isExpanded != value)
                {
                    _isExpanded = value;
                    OnPropertyChanged(nameof(IsExpanded));
                }
            }
        }

        private bool _isFolder;
        public bool IsFolder
        {
            get => _isFolder;
            set
            {
                if (_isFolder != value)
                {
                    _isFolder = value;
                    OnPropertyChanged(nameof(IsFolder));
                }
            }
        }

        private bool _isFile;
        public bool IsFile
        {
            get => _isFile;
            set
            {
                if (_isFile != value)
                {
                    _isFile = value;
                    OnPropertyChanged(nameof(IsFile));
                }
            }
        }

        public string Name { get; set; }
        public string FullPath { get; set; }
        public string RelativePath { get; set; }
        public string Status { get; set; }
        public bool IsCategory { get; set; }
        public ObservableCollection<GitChangeNode> Children { get; } = new ObservableCollection<GitChangeNode>();
    }

    public class ManageWorktreesViewModel : BaseViewModel
    {
        private readonly IGitService _gitService;
        private readonly ISolutionService _solutionService;
        private readonly ILoggingService _loggingService;
        private readonly IServiceProvider _serviceProvider;
        private string _activeRepositoryPath;
        private System.ComponentModel.INotifyPropertyChanged _gitExtService;
        public string ActiveRepositoryPath
        {
            get => _activeRepositoryPath;
            set
            {
                if (_activeRepositoryPath != value)
                {
                    _activeRepositoryPath = value;
                    OnPropertyChanged(nameof(ActiveRepositoryPath));
                }
            }
        }
        private bool _isLoading;

        public bool IsLoading
        {
            get => _isLoading;
            set { _isLoading = value; OnPropertyChanged(nameof(IsLoading)); OnPropertyChanged(nameof(IsAnyLoading)); }
        }

        private bool _isRefreshing;

        public bool IsRefreshing
        {
            get => _isRefreshing;
            set { _isRefreshing = value; OnPropertyChanged(nameof(IsRefreshing)); OnPropertyChanged(nameof(IsAnyLoading)); }
        }

        private bool _isLoadingDetails;
        public bool IsLoadingDetails
        {
            get => _isLoadingDetails;
            set { _isLoadingDetails = value; OnPropertyChanged(nameof(IsLoadingDetails)); }
        }

        private static readonly TimeSpan StatusRefreshCache = TimeSpan.FromSeconds(30);

        public bool IsAnyLoading => IsLoading || IsRefreshing;

        private ManageWorktreesState _currentState = ManageWorktreesState.WarmingUp;
        public ManageWorktreesState CurrentState
        {
            get => _currentState;
            set
            {
                if (_currentState != value)
                {
                    _currentState = value;
                    OnPropertyChanged(nameof(CurrentState));
                }
            }
        }

        private System.Threading.CancellationTokenSource _enrichmentCts;
        private System.Threading.CancellationTokenSource _debounceCts;
        private bool _refreshInProgress;
        private bool _refreshPending;
        private bool _pendingIsManual;
        private readonly object _refreshLock = new object();
        private bool _isDialogOrCommandActive;
        private DateTime _lastDialogCloseTimeUtc = DateTime.MinValue;

        private class WorktreeFailureInfo
        {
            public int FailureCount { get; set; }
            public DateTime NextRetryTimeUtc { get; set; }
        }
        private readonly Dictionary<string, WorktreeFailureInfo> _failureTracking = new Dictionary<string, WorktreeFailureInfo>(StringComparer.OrdinalIgnoreCase);
        private readonly object _failureLock = new object();

        public ObservableCollection<WorktreeItemViewModel> RawWorktrees { get; } = new ObservableCollection<WorktreeItemViewModel>();
        public ObservableCollection<HierarchyNode> WorktreeHierarchy { get; } = new ObservableCollection<HierarchyNode>();

        private string _searchText;
        public string SearchText
        {
            get => _searchText;
            set
            {
                if (_searchText != value)
                {
                    _searchText = value;
                    OnPropertyChanged(nameof(SearchText));
                    RefreshHierarchy();
                }
            }
        }

        private WorktreeItemViewModel _selectedWorktree;
        private System.Threading.CancellationTokenSource _detailsCts;
        public WorktreeItemViewModel SelectedWorktree
        {
            get => _selectedWorktree;
            set
            {
                if (_selectedWorktree != value)
                {
                    _selectedWorktree = value;
                    OnPropertyChanged(nameof(SelectedWorktree));
                    OnPropertyChanged(nameof(IsWorktreeSelected));
                    OnPropertyChanged(nameof(CanRemoveWorktree));
                    
                    if (CurrentState == ManageWorktreesState.LoadingWorktrees ||
                        CurrentState == ManageWorktreesState.NoSelection ||
                        CurrentState == ManageWorktreesState.WorktreeSelected)
                    {
                        CurrentState = _selectedWorktree != null ? ManageWorktreesState.WorktreeSelected : ManageWorktreesState.NoSelection;
                    }

                    if (_detailsCts != null)
                    {
                        try { _detailsCts.Cancel(); _detailsCts.Dispose(); } catch { }
                    }
                    _detailsCts = new System.Threading.CancellationTokenSource();
                    _ = LoadDetailsAsync(_detailsCts.Token);
                }
            }
        }

        public bool IsWorktreeSelected => SelectedWorktree != null;
        public bool CanRemoveWorktree => SelectedWorktree != null && !SelectedWorktree.IsMain && !SelectedWorktree.IsCurrent;

        // Detailed Properties
        private string _detailBranchName;
        public string DetailBranchName
        {
            get => _detailBranchName;
            set { _detailBranchName = value; OnPropertyChanged(nameof(DetailBranchName)); }
        }

        private string _detailStatusSummary;
        public string DetailStatusSummary
        {
            get => _detailStatusSummary;
            set { _detailStatusSummary = value; OnPropertyChanged(nameof(DetailStatusSummary)); }
        }

        private int _stagedCount;
        public int StagedCount
        {
            get => _stagedCount;
            set { _stagedCount = value; OnPropertyChanged(nameof(StagedCount)); }
        }

        private int _changesCount;
        public int ChangesCount
        {
            get => _changesCount;
            set { _changesCount = value; OnPropertyChanged(nameof(ChangesCount)); }
        }

        private int _untrackedCount;
        public int UntrackedCount
        {
            get => _untrackedCount;
            set { _untrackedCount = value; OnPropertyChanged(nameof(UntrackedCount)); }
        }

        private int _aheadCount;
        public int AheadCount
        {
            get => _aheadCount;
            set { _aheadCount = value; OnPropertyChanged(nameof(AheadCount)); }
        }

        private int _behindCount;
        public int BehindCount
        {
            get => _behindCount;
            set { _behindCount = value; OnPropertyChanged(nameof(BehindCount)); }
        }

        private string _detailPath;
        public string DetailPath
        {
            get => _detailPath;
            set { _detailPath = value; OnPropertyChanged(nameof(DetailPath)); }
        }

        private General _options;
        public OpenBehavior PreferredOpenAction
        {
            get
            {
                if (_options == null)
                {
                    _options = CommandExecutor.optionsSaved;
                }
                return _options?.PreferredOpenAction ?? OpenBehavior.NewVSWindow;
            }
            set
            {
                if (_options == null)
                {
                    _options = CommandExecutor.optionsSaved;
                }
                if (_options != null && _options.PreferredOpenAction != value)
                {
                    _options.PreferredOpenAction = value;
                    OnPropertyChanged(nameof(PreferredOpenAction));
                    _ = Task.Run(async () =>
                    {
                        await _options.SaveAsync().ConfigureAwait(false);
                    });
                }
            }
        }

        public ObservableCollection<string> Changes { get; } = new ObservableCollection<string>();
        public ObservableCollection<GitChangeNode> StagedChangesTree { get; } = new ObservableCollection<GitChangeNode>();
        public ObservableCollection<GitChangeNode> UnstagedChangesTree { get; } = new ObservableCollection<GitChangeNode>();
        public ObservableCollection<GitCommitInfo> Outgoing { get; } = new ObservableCollection<GitCommitInfo>();

        public ManageWorktreesViewModel() : this(null, null, null) { }

        public ManageWorktreesViewModel(IGitService gitService = null, ISolutionService solutionService = null, ILoggingService loggingService = null, IServiceProvider serviceProvider = null)
        {
            _loggingService = loggingService ?? LoggingHelper.Instance;
            _gitService = gitService ?? new GitHelper(_loggingService);
            _solutionService = solutionService ?? new SolutionHelper(_loggingService, _gitService);
            // ManageWorktreesViewModel is instantiated from XAML — no injection path at runtime,
            // so fall back to GlobalProvider when no explicit provider is supplied.
            _serviceProvider = serviceProvider ?? ServiceProvider.GlobalProvider;

            RefreshCommand = new AsyncRelayCommand("Refresh Worktrees", async obj => await RefreshAsync(isManual: true), null, _loggingService);
            CreateCommand = new AsyncRelayCommand("Create Worktree", async obj => await CreateWorktreeAsync(), null, _loggingService);
            PruneCommand = new AsyncRelayCommand("Prune Worktrees", async obj => await PruneWorktreeAsync(), null, _loggingService);
            OpenCommand = new AsyncRelayCommand("Open Worktree", async obj => await OpenWorktreeAsync(obj), obj => IsWorktreeSelected, _loggingService);
            RemoveCommand = new AsyncRelayCommand("Remove Worktree", async obj => await RemoveWorktreeAsync(), obj => CanRemoveWorktree, _loggingService);
            OpenCommitDetailsCommand = new AsyncRelayCommand("Open Commit Details", async obj => await OpenCommitDetailsAsync(obj), null, _loggingService);
            CopyPathCommand = new RelayCommand("Copy Worktree Path", obj => { CopyPathToClipboard(); return true; }, obj => IsWorktreeSelected, _loggingService);
            CopyRepositoryPathCommand = new RelayCommand("Copy Repository Path", obj => { CopyRepositoryPathToClipboard(); return true; }, obj => !string.IsNullOrEmpty(ActiveRepositoryPath), _loggingService);
            ExplorePathCommand = new RelayCommand("Explore Path", obj =>
            {
                string path = obj as string;
                if (string.IsNullOrEmpty(path)) return false;
                try
                {
                    System.Diagnostics.Process.Start("explorer.exe", path);
                    return true;
                }
                catch (Exception ex)
                {
                    _loggingService?.WriteToOutputWindowAsync($"Failed to open folder: {ex.Message}");
                    return false;
                }
            }, null, _loggingService);

            InitializeRepositoryPath();
        }

        private void ResolveActiveRepositoryPath()
        {
            try
            {
                ThreadHelper.ThrowIfNotOnUIThread();

                EnvDTE80.DTE2 dte = Package.GetGlobalService(typeof(EnvDTE.DTE)) as EnvDTE80.DTE2;
                if (dte != null)
                {
                    string solutionPath = dte.Solution?.FullName;
                    string resolved = _solutionService.GetRepositoryPath(solutionPath);
                    if (!string.IsNullOrEmpty(resolved))
                    {
                        ActiveRepositoryPath = resolved;
                    }
                }
            }
            catch
            {
                // Fallback for tests
            }
        }

        private void InitializeRepositoryPath()
        {
            try
            {
                ThreadHelper.ThrowIfNotOnUIThread();
                ResolveActiveRepositoryPath();

                SubscribeToGitExtEvents();
                
                if (!string.IsNullOrEmpty(ActiveRepositoryPath))
                {
                    _ = RefreshAsync(isManual: false);
                }
                else
                {
                    CurrentState = ManageWorktreesState.NoRepository;
                }
            }
            catch
            {
                CurrentState = ManageWorktreesState.NoRepository;
            }
        }

        private void SubscribeToGitExtEvents()
        {
            try
            {
                var gitExtType = Type.GetType("Microsoft.VisualStudio.TeamFoundation.Git.Extensibility.IGitExt, Microsoft.TeamFoundation.Git.Provider");
                if (gitExtType == null) return;

                var packageType = Type.GetType("Microsoft.VisualStudio.Shell.Package, Microsoft.VisualStudio.Shell.15.0");
                if (packageType == null) return;

                var getGlobalServiceMethod = packageType.GetMethod("GetGlobalService", new[] { typeof(Type) });
                if (getGlobalServiceMethod == null) return;

                var gitExt = getGlobalServiceMethod.Invoke(null, new object[] { gitExtType }) as System.ComponentModel.INotifyPropertyChanged;
                if (gitExt != null && _gitExtService == null)
                {
                    _gitExtService = gitExt;
                    _gitExtService.PropertyChanged += OnGitExtPropertyChanged;
                }
            }
            catch
            {
                // Ignore fallback for tests
            }
        }

        private void OnGitExtPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "ActiveRepositories")
            {
                // VS fires multiple repository change notifications during
                // modal dialog focus restoration. Suppress them briefly to
                // avoid redundant refresh storms.
                if (_isDialogOrCommandActive || (DateTime.UtcNow - _lastDialogCloseTimeUtc) < TimeSpan.FromSeconds(1.5))
                {
                    return; // Ignore ActiveRepositories change notifications while dialog/command is active or immediately after it closes
                }

                if (_debounceCts != null)
                {
                    try { _debounceCts.Cancel(); _debounceCts.Dispose(); } catch { }
                }
                _debounceCts = new System.Threading.CancellationTokenSource();
                var token = _debounceCts.Token;

                _ = Task.Run(async () =>
                {
                    try
                    {
                        await Task.Delay(300, token);
                        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                        await RefreshAsync(isManual: false);
                    }
                    catch (TaskCanceledException) { }
                    catch (OperationCanceledException) { }
                });
            }
        }

        public IAsyncCommand RefreshCommand { get; }
        public IAsyncCommand CreateCommand { get; }
        public IAsyncCommand PruneCommand { get; }
        public IAsyncCommand OpenCommand { get; }
        public IAsyncCommand RemoveCommand { get; }
        public IAsyncCommand OpenCommitDetailsCommand { get; }
        public ICommand CopyPathCommand { get; }
        public ICommand CopyRepositoryPathCommand { get; }
        public ICommand ExplorePathCommand { get; }

        public async Task RefreshAsync(bool isManual = false)
        {
            lock (_refreshLock)
            {
                if (isManual)
                {
                    _pendingIsManual = true;
                }
                if (_refreshInProgress)
                {
                    _refreshPending = true;
                    return;
                }
                _refreshInProgress = true;
            }

            try
            {
                bool activeManual = isManual;
                lock (_refreshLock)
                {
                    if (_pendingIsManual)
                    {
                        activeManual = true;
                        _pendingIsManual = false;
                    }
                }
                await RefreshInternalAsync(activeManual);
            }
            finally
            {
                bool shouldRefreshAgain = false;
                bool nextManual = false;
                lock (_refreshLock)
                {
                    _refreshInProgress = false;
                    if (_refreshPending)
                    {
                        _refreshPending = false;
                        shouldRefreshAgain = true;
                        nextManual = _pendingIsManual;
                        _pendingIsManual = false;
                    }
                }

                if (shouldRefreshAgain)
                {
                    _ = Task.Run(async () =>
                    {
                        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                        await RefreshAsync(nextManual);
                    });
                }
            }
        }

        private async Task RefreshInternalAsync(bool isManual)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            ResolveActiveRepositoryPath();
            if (string.IsNullOrEmpty(ActiveRepositoryPath))
            {
                CurrentState = ManageWorktreesState.NoRepository;
                return;
            }

            if (RawWorktrees.Count == 0)
            {
                CurrentState = ManageWorktreesState.LoadingWorktrees;
            }

            // Clear the failure tracking only when user explicitly invokes refresh (isManual == true)
            if (isManual)
            {
                lock (_failureLock)
                {
                    _failureTracking.Clear();
                }
            }

            // 1. Cancel previous enrichment
            if (_enrichmentCts != null)
            {
                try
                {
                    _enrichmentCts.Cancel();
                    _enrichmentCts.Dispose();
                }
                catch { }
            }
            _enrichmentCts = new System.Threading.CancellationTokenSource();
            var token = _enrichmentCts.Token;

            try
            {
                IsLoading = true;
                _loggingService?.SetCommandStatusBusy(true);

                List<WorktreeInfo> worktreeInfos = await _gitService.GetWorktreesAsync(ActiveRepositoryPath).ConfigureAwait(false);
                if (worktreeInfos == null) return;

                string mainRepoPath = Path.GetFullPath(ActiveRepositoryPath);

                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                string currentWorktreePath = mainRepoPath;
                var gitExt = Package.GetGlobalService(typeof(IGitExt)) as IGitExt2;
                if (gitExt != null && gitExt.ActiveRepositories != null && gitExt.ActiveRepositories.Count > 0)
                {
                    string activePath = gitExt.ActiveRepositories.FirstOrDefault()?.RepositoryPath;
                    if (!string.IsNullOrEmpty(activePath))
                    {
                        activePath = Path.GetFullPath(activePath);
                        if (activePath.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
                        {
                            activePath = Path.GetDirectoryName(activePath);
                        }
                        else if (activePath.Contains(".git"))
                        {
                            int gitIdx = activePath.IndexOf(".git", StringComparison.OrdinalIgnoreCase);
                            if (gitIdx > 0)
                            {
                                activePath = activePath.Substring(0, gitIdx);
                            }
                        }
                        currentWorktreePath = Path.GetFullPath(activePath);
                    }
                }
                else
                {
                    EnvDTE80.DTE2 dte = Package.GetGlobalService(typeof(EnvDTE.DTE)) as EnvDTE80.DTE2;
                    if (dte != null && dte.Solution != null && !string.IsNullOrEmpty(dte.Solution.FullName))
                    {
                        string solDir = Path.GetDirectoryName(Path.GetFullPath(dte.Solution.FullName));
                        foreach (var w in worktreeInfos)
                        {
                            string fullPath = Path.GetFullPath(w.Path);
                            if (solDir.StartsWith(fullPath, StringComparison.OrdinalIgnoreCase))
                            {
                                currentWorktreePath = fullPath;
                                break;
                            }
                        }
                    }
                }

                // Preserve existing items state to prevent flickering
                var existingItemsMap = RawWorktrees.ToDictionary(x => x.FullPath, StringComparer.OrdinalIgnoreCase);

                RawWorktrees.Clear();

                string normalizedMainRepoPath = Path.GetFullPath(mainRepoPath).TrimEnd('\\', '/');
                string normalizedCurrentWorktreePath = Path.GetFullPath(currentWorktreePath).TrimEnd('\\', '/');

                foreach (var info in worktreeInfos)
                {
                    string normalizedPath = Path.GetFullPath(info.Path).TrimEnd('\\', '/');
                    bool isMain = info.IsMain;
                    bool isCurrent = normalizedPath.Equals(normalizedCurrentWorktreePath, StringComparison.OrdinalIgnoreCase);
                    var item = new WorktreeItemViewModel(
                        info.Path,
                        Path.GetFileName(info.Path),
                        isMain: isMain,
                        isCurrent: isCurrent,
                        branchName: info.Branch
                    );

                    if (existingItemsMap.TryGetValue(info.Path, out var cached))
                    {
                        item.IsDirty = cached.IsDirty;
                        if (!isManual)
                        {
                            item.HasRefreshError = cached.HasRefreshError;
                            item.LastErrorMessage = cached.LastErrorMessage;
                            item.ErrorSummary = cached.ErrorSummary;
                            item.LastStatusCheckUtc = cached.LastStatusCheckUtc;
                        }
                        else
                        {
                            // Clear warning status and status check cache on manual refresh
                            item.HasRefreshError = false;
                            item.LastErrorMessage = string.Empty;
                            item.ErrorSummary = string.Empty;
                            item.LastStatusCheckUtc = DateTime.MinValue;
                        }
                    }

                    RawWorktrees.Add(item);
                }

                string previouslySelectedPath = SelectedWorktree?.FullPath;

                RefreshHierarchy();

                if (RawWorktrees.Count > 0)
                {
                    var previouslySelected = RawWorktrees.FirstOrDefault(w => w.FullPath.Equals(previouslySelectedPath, StringComparison.OrdinalIgnoreCase));
                    SelectedWorktree = previouslySelected ?? RawWorktrees.FirstOrDefault(w => w.IsCurrent) ?? RawWorktrees.First();
                }

                CurrentState = SelectedWorktree != null ? ManageWorktreesState.WorktreeSelected : ManageWorktreesState.NoSelection;

                IsLoading = false;
                _loggingService?.SetCommandStatusBusy(false);

                // 2. Start background enrichment for dirty states
                IsRefreshing = true;
                _ = EnrichWorktreesAsync(RawWorktrees.ToList(), token, isManual);
            }
            catch (Exception ex)
            {
                await _loggingService?.WriteToOutputWindowAsync($"Failed to load worktrees: {ex.Message}");
                IsLoading = false;
                IsRefreshing = false;
                _loggingService?.SetCommandStatusBusy(false);
                CurrentState = ManageWorktreesState.NoRepository;
            }
        }

        private async Task EnrichWorktreesAsync(List<WorktreeItemViewModel> items, System.Threading.CancellationToken token, bool isManual)
        {
            try
            {
                // 1. Process active/current worktree first and await it
                var currentItem = items.FirstOrDefault(x => x.IsCurrent);
                if (currentItem != null)
                {
                    await EnrichSingleItemAsync(currentItem, token, isManual).ConfigureAwait(false);
                }

                // 2. Process other worktrees with strict concurrency throttling of 2 (letting tasks run naturally)
                using (var semaphore = new System.Threading.SemaphoreSlim(2))
                {
                    var otherItems = items.Where(x => !x.IsCurrent).ToList();
                    var tasks = otherItems.Select(async item =>
                    {
                        await semaphore.WaitAsync(token).ConfigureAwait(false);
                        try
                        {
                            await EnrichSingleItemAsync(item, token, isManual).ConfigureAwait(false);
                        }
                        finally
                        {
                            semaphore.Release();
                        }
                    });

                    await Task.WhenAll(tasks).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
            {
                // Ignore
            }
            catch (Exception ex)
            {
                _loggingService?.WriteToOutputWindowAsync($"Enrichment failed: {ex.Message}");
            }
            finally
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                IsRefreshing = false;
            }
        }

        private async Task EnrichSingleItemAsync(WorktreeItemViewModel item, System.Threading.CancellationToken token, bool isManual)
        {
            try
            {
                token.ThrowIfCancellationRequested();

                // Skip checking if this is an auto-refresh and the status check was done recently (within 30s)
                if (!isManual && DateTime.UtcNow - item.LastStatusCheckUtc < StatusRefreshCache)
                {
                    return;
                }

                // Skip checking if we are in the temporary failure retry backoff period
                lock (_failureLock)
                {
                    if (_failureTracking.TryGetValue(item.FullPath, out var failInfo))
                    {
                        if (DateTime.UtcNow < failInfo.NextRetryTimeUtc)
                        {
                            return;
                        }
                    }
                }

                token.ThrowIfCancellationRequested();
                item.IsLoadingStatus = true;

                // Missing Directory Fast Path: skip Git execution entirely
                if (!System.IO.Directory.Exists(item.FullPath))
                {
                    item.IsDirty = false;
                    item.HasRefreshError = true;
                    item.ErrorSummary = "Worktree folder missing.";
                    item.LastErrorMessage = $"Directory not found: {item.FullPath}";
                    lock (_failureLock)
                    {
                        _failureTracking[item.FullPath] = new WorktreeFailureInfo { FailureCount = 1, NextRetryTimeUtc = DateTime.UtcNow.AddMinutes(2) };
                    }
                    return;
                }

                var result = await _gitService.IsWorktreeDirtyAsync(item.FullPath).ConfigureAwait(false);
                token.ThrowIfCancellationRequested();

                if (result.Success)
                {
                    item.IsDirty = result.Value;
                    item.HasRefreshError = false;
                    item.LastErrorMessage = string.Empty;
                    item.ErrorSummary = string.Empty;

                    lock (_failureLock)
                    {
                        _failureTracking.Remove(item.FullPath);
                    }
                }
                else
                {
                    item.IsDirty = false;
                    item.HasRefreshError = true;
                    item.LastErrorMessage = result.ErrorMessage;
                    item.ErrorSummary = "Unable to refresh worktree status. See Output window for details.";

                    int currentFailCount = 1;
                    lock (_failureLock)
                    {
                        if (_failureTracking.TryGetValue(item.FullPath, out var failInfo))
                        {
                            failInfo.FailureCount++;
                            currentFailCount = failInfo.FailureCount;
                        }
                        else
                        {
                            failInfo = new WorktreeFailureInfo { FailureCount = 1 };
                            _failureTracking[item.FullPath] = failInfo;
                        }

                        TimeSpan backoffDuration;
                        if (currentFailCount == 1)
                        {
                            backoffDuration = TimeSpan.FromSeconds(30);
                        }
                        else if (currentFailCount == 2)
                        {
                            backoffDuration = TimeSpan.FromMinutes(2);
                        }
                        else
                        {
                            backoffDuration = TimeSpan.FromMinutes(5);
                        }

                        failInfo.NextRetryTimeUtc = DateTime.UtcNow.Add(backoffDuration);
                    }

                    _loggingService?.WriteToOutputWindowAsync(
                        $"Failed to refresh status for {item.FolderName} ({item.FullPath}). " +
                        $"Error: {result.ErrorMessage}. Backoff attempt: {currentFailCount}. Skipping checks for the next retry interval."
                    );
                }
            }
            catch (OperationCanceledException)
            {
                // Ignore
            }
            catch (Exception ex)
            {
                _loggingService?.WriteToOutputWindowAsync($"Failed to get status for {item.FolderName}: {ex.Message}");
            }
            finally
            {
                item.LastStatusCheckUtc = DateTime.UtcNow;
                item.IsLoadingStatus = false;
            }
        }

        private void RefreshHierarchy()
        {
            var localHierarchy = new System.Collections.Generic.List<HierarchyNode>();

            // Filter raw items first
            var filteredItems = RawWorktrees.Where(item => 
                string.IsNullOrWhiteSpace(SearchText) || 
                item.FolderName.IndexOf(SearchText, StringComparison.OrdinalIgnoreCase) >= 0
            ).ToList();

            // Root Node for Branches / Tags (similar to Visual Studio's layout)
            var rootNode = new HierarchyNode { Name = "Branches" };
            localHierarchy.Add(rootNode);

            foreach (var item in filteredItems)
            {
                string branchName = item.BranchName ?? "Unknown";

                var parts = branchName.Split('/');
                ObservableCollection<HierarchyNode> currentLevel = rootNode.Items;

                for (int i = 0; i < parts.Length; i++)
                {
                    string part = parts[i];
                    bool isLast = (i == parts.Length - 1);

                    var existingNode = currentLevel.FirstOrDefault(n => n.Name.Equals(part, StringComparison.OrdinalIgnoreCase));
                    if (existingNode == null)
                    {
                        var newNode = new HierarchyNode { Name = part };
                        if (isLast)
                        {
                            newNode.WorktreeItem = item;
                        }
                        currentLevel.Add(newNode);
                        existingNode = newNode;
                    }
                    currentLevel = existingNode.Items;
                }
            }

            WorktreeHierarchy.Clear();
            foreach (var node in localHierarchy)
            {
                WorktreeHierarchy.Add(node);
            }
        }

        private async Task LoadDetailsAsync(System.Threading.CancellationToken token)
        {
            if (SelectedWorktree == null)
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                DetailBranchName = "";
                DetailStatusSummary = "";
                DetailPath = "";
                StagedCount = 0;
                ChangesCount = 0;
                UntrackedCount = 0;
                AheadCount = 0;
                BehindCount = 0;
                Changes.Clear();
                StagedChangesTree.Clear();
                UnstagedChangesTree.Clear();
                Outgoing.Clear();
                return;
            }

            try
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                IsLoadingDetails = true;

                var worktree = SelectedWorktree;

                var details = await _gitService.GetWorkTreeDetailsAsync(ActiveRepositoryPath, worktree.FullPath).ConfigureAwait(false);

                token.ThrowIfCancellationRequested();

                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                
                token.ThrowIfCancellationRequested();

                if (SelectedWorktree != worktree) return;

                // Clear the worktree's failure/warning state only after a successful load
                worktree.HasRefreshError = false;
                worktree.ErrorSummary = string.Empty;
                worktree.LastErrorMessage = string.Empty;
                lock (_failureLock)
                {
                    _failureTracking.Remove(worktree.FullPath);
                }

                DetailBranchName = details.Branch;
                DetailStatusSummary = details.StatusSummary;
                DetailPath = worktree.FullPath;

                ParseStatusSummary(details.StatusSummary);

                Changes.Clear();
                foreach (var change in details.Changes) Changes.Add(change);
                BuildChangesTree(details.Changes);

                Outgoing.Clear();
                foreach (var commit in details.Outgoing) Outgoing.Add(commit);

                worktree.IsDirty = details.Changes != null && details.Changes.Count > 0;
            }
            catch (OperationCanceledException)
            {
                // Ignore
            }
            catch (Exception ex)
            {
                await _loggingService?.WriteToOutputWindowAsync($"Failed to load details for {SelectedWorktree?.FolderName}: {ex.Message}");
                if (SelectedWorktree != null)
                {
                    SelectedWorktree.HasRefreshError = true;
                    SelectedWorktree.ErrorSummary = "Unable to refresh worktree status. See Output window for details.";
                    SelectedWorktree.LastErrorMessage = ex.Message;
                    lock (_failureLock)
                    {
                        _failureTracking[SelectedWorktree.FullPath] = new WorktreeFailureInfo { FailureCount = 1 };
                    }
                }
            }
            finally
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                IsLoadingDetails = false;
            }
        }

        private void ParseStatusSummary(string statusSummary)
        {
            if (string.IsNullOrEmpty(statusSummary))
            {
                StagedCount = 0;
                ChangesCount = 0;
                UntrackedCount = 0;
                AheadCount = 0;
                BehindCount = 0;
                return;
            }

            try
            {
                // Format: "{staged} staged, {unstaged + untracked} changes ({untracked} untracked) · ↑{ahead} ↓{behind}"
                // E.g.: "0 staged, 0 changes (0 untracked) · ↑0 ↓0"
                var match = System.Text.RegularExpressions.Regex.Match(statusSummary, @"(\d+)\s+staged,\s+(\d+)\s+changes\s*\((\d+)\s+untracked\)\s*·\s*↑(\d+)\s+↓(\d+)");
                if (match.Success)
                {
                    StagedCount = int.Parse(match.Groups[1].Value);
                    ChangesCount = int.Parse(match.Groups[2].Value);
                    UntrackedCount = int.Parse(match.Groups[3].Value);
                    AheadCount = int.Parse(match.Groups[4].Value);
                    BehindCount = int.Parse(match.Groups[5].Value);
                }
                else
                {
                    StagedCount = 0;
                    ChangesCount = 0;
                    UntrackedCount = 0;
                    AheadCount = 0;
                    BehindCount = 0;
                }
            }
            catch
            {
                StagedCount = 0;
                ChangesCount = 0;
                UntrackedCount = 0;
                AheadCount = 0;
                BehindCount = 0;
            }
        }

        private async Task CreateWorktreeAsync()
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            try
            {
                _isDialogOrCommandActive = true;
                var createCommandExecutor = new CommandExecutor(CommandType.Create);
                if (createCommandExecutor.PreRequisite())
                {
                    createCommandExecutor.Execute();
                    _lastDialogCloseTimeUtc = DateTime.UtcNow;
                    _isDialogOrCommandActive = false;

                    if (createCommandExecutor.IsWorktreeCreated)
                    {
                        await RefreshAsync(isManual: true);
                    }
                }
            }
            catch (Exception ex)
            {
                await _loggingService?.WriteToOutputWindowAsync($"Failed to open create dialog: {ex.Message}");
            }
            finally
            {
                _lastDialogCloseTimeUtc = DateTime.UtcNow;
                _isDialogOrCommandActive = false;
            }
        }

        private async Task PruneWorktreeAsync()
        {
            var result = await _gitService.PruneAsync(ActiveRepositoryPath).ConfigureAwait(false);
            if (!result.Success)
            {
                DialogHelper.ShowOperationError(_serviceProvider, "Prune Worktrees", result.ErrorMessage);
            }
            await RefreshAsync().ConfigureAwait(false);
        }

        private async Task OpenWorktreeAsync(object parameter)
        {
            if (SelectedWorktree == null) return;

            OpenBehavior behavior = PreferredOpenAction;
            if (parameter is OpenBehavior b)
            {
                behavior = b;
                PreferredOpenAction = b;
            }
            else if (parameter is string bStr && Enum.TryParse(bStr, out OpenBehavior parsedB))
            {
                behavior = parsedB;
                PreferredOpenAction = parsedB;
            }

            if (behavior == OpenBehavior.Explorer)
            {
                try
                {
                    System.Diagnostics.Process.Start("explorer.exe", SelectedWorktree.FullPath);
                }
                catch (Exception ex)
                {
                    await _loggingService?.WriteToOutputWindowAsync($"Failed to open folder: {ex.Message}");
                }
                return;
            }

            bool openInCurrent = (behavior == OpenBehavior.CurrentWindow);
            await _solutionService.OpenSolution(SelectedWorktree.FullPath, openInCurrent).ConfigureAwait(false);
        }

        private async Task RemoveWorktreeAsync()
        {
            if (SelectedWorktree == null || SelectedWorktree.IsMain || SelectedWorktree.IsCurrent) return;

            // First attempt: Standard Remove without force
            var result = await _gitService.RemoveWorkTreeAsync(ActiveRepositoryPath, SelectedWorktree.FullPath, shouldForceCreate: false).ConfigureAwait(false);
            bool success = result.Success;
            if (!success)
            {
                // Prompt with Force confirmation dialog if failed
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                var msgResult = System.Windows.MessageBox.Show(
                    $"Worktree '{SelectedWorktree.FolderName}' has uncommitted or staged changes. Force remove it?\n\nError Details:\n{result.ErrorMessage}",
                    "Force Remove Worktree",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning
                );

                if (msgResult == MessageBoxResult.Yes)
                {
                    var forceResult = await _gitService.RemoveWorkTreeAsync(ActiveRepositoryPath, SelectedWorktree.FullPath, shouldForceCreate: true).ConfigureAwait(false);
                    success = forceResult.Success;
                    if (!success)
                    {
                        DialogHelper.ShowOperationError(_serviceProvider, "Remove Worktree", forceResult.ErrorMessage);
                    }
                }
            }

            if (success)
            {
                await RefreshAsync().ConfigureAwait(false);
            }
        }

        private string ResolveCommitRepositoryPath(string worktreeFullPath, string activeRepositoryPath)
        {
            if (!string.IsNullOrEmpty(activeRepositoryPath))
            {
                return activeRepositoryPath;
            }

            if (!string.IsNullOrEmpty(worktreeFullPath))
            {
                string resolved = _solutionService.GetRepositoryPath(worktreeFullPath);
                if (!string.IsNullOrEmpty(resolved))
                {
                    return resolved;
                }
            }

            return worktreeFullPath;
        }

        private async Task<IGitExt2> EnsureGitExtLoadedAsync()
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            var gitExt = Package.GetGlobalService(typeof(IGitExt)) as IGitExt2;
            if (gitExt == null)
            {
                // Force load Git package by showing Git Changes / Git Repository
                var dte = Package.GetGlobalService(typeof(EnvDTE.DTE)) as EnvDTE.DTE;
                if (dte != null)
                {
                    try { dte.ExecuteCommand("View.GitChanges"); } catch { }
                    try { dte.ExecuteCommand("View.GitRepository"); } catch { }
                }
                await Task.Delay(250);
                gitExt = Package.GetGlobalService(typeof(IGitExt)) as IGitExt2;
            }
            return gitExt;
        }

        private async Task OpenCommitDetailsAsync(object parameter)
        {
            if (!(parameter is GitCommitInfo commit)) return;
            if (SelectedWorktree == null) return; // Exit early if SelectedWorktree is null

            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            var gitExt = await EnsureGitExtLoadedAsync();
            if (gitExt != null)
            {
                string activeRepoPath = gitExt.ActiveRepositories?.FirstOrDefault()?.RepositoryPath;
                string repoPath = ResolveCommitRepositoryPath(SelectedWorktree.FullPath, activeRepoPath);

                if (_loggingService != null)
                {
                    await _loggingService.WriteToOutputWindowAsync($"Opening commit details for SHA: {commit.FullSha} in repo: {repoPath}");
                }

                try
                {
                    gitExt.ViewCommitDetails(repoPath, null, commit.FullSha);
                }
                catch (Exception ex)
                {
                    if (_loggingService != null)
                    {
                        await _loggingService.WriteToOutputWindowAsync($"Unable to show commit details for hash: {commit.FullSha}. ex: {ex.Message}", true);
                    }
                }
            }
            else
            {
                if (_loggingService != null)
                {
                    await _loggingService.WriteToOutputWindowAsync($"Unable to show commit details for hash: {commit.FullSha}. IGitExt service is not available.", true);
                }
            }
        }
        
        private void CopyPathToClipboard()
        {
            if (SelectedWorktree != null)
            {
                try
                {
                    Clipboard.SetText(SelectedWorktree.FullPath);
                    _loggingService?.UpdateStatusBar("Path copied to clipboard!");
                }
                catch (Exception ex)
                {
                    _loggingService?.WriteToOutputWindowAsync($"Failed to copy path: {ex.Message}");
                }
            }
        }

        private void CopyRepositoryPathToClipboard()
        {
            if (!string.IsNullOrEmpty(ActiveRepositoryPath))
            {
                try
                {
                    Clipboard.SetText(ActiveRepositoryPath);
                    _loggingService?.UpdateStatusBar("Repository path copied to clipboard!");
                }
                catch (Exception ex)
                {
                    _loggingService?.WriteToOutputWindowAsync($"Failed to copy repository path: {ex.Message}");
                }
            }
        }

        private void BuildChangesTree(List<string> porcelainChanges)
        {
            StagedChangesTree.Clear();
            UnstagedChangesTree.Clear();
            if (porcelainChanges == null || porcelainChanges.Count == 0) return;

            var stagedRoot = new GitChangeNode { Name = "Staged Changes", IsCategory = true };
            var unstagedRoot = new GitChangeNode { Name = "Changes", IsCategory = true };

            foreach (var change in porcelainChanges)
            {
                if (change.Length < 3) continue;

                string statusPart = change.Substring(0, 2);
                string fileRelativePath = change.Substring(3).Trim();

                // Normalize directory entries (e.g., untracked folders with trailing slashes)
                if (fileRelativePath.EndsWith("/") || fileRelativePath.EndsWith("\\"))
                {
                    fileRelativePath = fileRelativePath.TrimEnd('/', '\\');
                }

                // Handle renamed/copied files in porcelain status (e.g., "R  old -> new")
                if (fileRelativePath.Contains(" -> "))
                {
                    int arrowIndex = fileRelativePath.IndexOf(" -> ");
                    fileRelativePath = fileRelativePath.Substring(arrowIndex + 4).Trim();
                }

                if (string.IsNullOrEmpty(fileRelativePath)) continue;

                // Safely filter out internal metadata paths (.git, .vs) without matching normal files containing these substrings
                string[] parts = fileRelativePath.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Any(p => p.Equals(".git", StringComparison.OrdinalIgnoreCase) || p.Equals(".vs", StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                bool isStaged = statusPart[0] != ' ' && statusPart[0] != '?';
                bool isUnstaged = statusPart[1] != ' ';

                if (isStaged)
                {
                    AddPathToTree(stagedRoot, fileRelativePath, statusPart[0].ToString());
                }
                if (isUnstaged)
                {
                    string status = (statusPart == "??") ? "??" : statusPart[1].ToString();
                    AddPathToTree(unstagedRoot, fileRelativePath, status);
                }
            }

            // Recursively update node properties (IsFolder, IsFile)
            UpdateNodeTypes(stagedRoot);
            UpdateNodeTypes(unstagedRoot);

            foreach (var child in stagedRoot.Children)
            {
                StagedChangesTree.Add(child);
            }
            foreach (var child in unstagedRoot.Children)
            {
                UnstagedChangesTree.Add(child);
            }
        }

        private void UpdateNodeTypes(GitChangeNode node)
        {
            if (node == null) return;
            if (!node.IsCategory)
            {
                node.IsFolder = node.Children != null && node.Children.Count > 0;
                node.IsFile = !node.IsFolder;
            }
            foreach (var child in node.Children)
            {
                UpdateNodeTypes(child);
            }
        }

        private int CountFiles(GitChangeNode node)
        {
            if (node.Children == null || node.Children.Count == 0) return 1;
            return node.Children.Sum(CountFiles);
        }

        private void AddPathToTree(GitChangeNode root, string relativePath, string status)
        {
            string[] parts = relativePath.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
            GitChangeNode current = root;

            for (int i = 0; i < parts.Length; i++)
            {
                string partName = parts[i];
                bool isLast = (i == parts.Length - 1);

                GitChangeNode child = current.Children.FirstOrDefault(c => c.Name.Equals(partName, StringComparison.OrdinalIgnoreCase));
                if (child == null)
                {
                    child = new GitChangeNode { Name = partName };
                    current.Children.Add(child);
                }

                if (isLast)
                {
                    child.RelativePath = relativePath;
                    child.FullPath = Path.Combine(SelectedWorktree?.FullPath ?? "", relativePath);
                    child.Status = status;
                }

                current = child;
            }
        }
    }
}
