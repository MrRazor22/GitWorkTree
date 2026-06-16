using GitWorkTree.Commands;
using GitWorkTree.Helpers;
using GitWorkTree.Services;
using Microsoft.VisualStudio.Threading;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Input;

namespace GitWorkTree.ViewModel
{
    public class WorkTreeDialogViewModel : BaseViewModel
    {
        private readonly IGitService _gitService;
        private readonly ISolutionService _solutionService;
        private readonly ILoggingService _loggingService;

        private bool _newBranchNameChanged;
        private bool _folderPathChanged;
        private bool _showAllErrors;
        private readonly IServiceProvider _serviceProvider;

        #region UI Related Properties
        public ObservableCollection<BranchInfo> Branches_Worktrees { get; set; }

        private General optionsSaved;

        public General OptionsSaved
        {
            get { return optionsSaved; }
            set
            {
                if (optionsSaved != value)
                {
                    optionsSaved = value;
                    OnPropertyChanged(nameof(optionsSaved));
                }
            }
        }

        private CommandType commandType;
        public CommandType CommandType
        {
            get { return commandType; }
            set
            {
                if (commandType != value)
                {
                    commandType = value;
                    OnPropertyChanged(nameof(commandType));
                }
            }
        }

        private bool _isNewBranchMode;
        public bool IsNewBranchMode
        {
            get { return _isNewBranchMode; }
            set
            {
                if (_isNewBranchMode != value)
                {
                    _isNewBranchMode = value;
                    OnPropertyChanged(nameof(IsNewBranchMode));
                    OnPropertyChanged(nameof(IsExistingBranchMode));
                    if (!_isNewBranchMode && SelectedBranch != null && SelectedBranch.HasLinkedWorktree && Branches_Worktrees != null)
                    {
                        var firstNoWorktree = Branches_Worktrees.FirstOrDefault(b => !b.HasLinkedWorktree);
                        if (firstNoWorktree != null)
                        {
                            SelectedBranch = firstNoWorktree;
                        }
                    }

                    CommandManager.InvalidateRequerySuggested();
                    UpdateFolderPath();

                    if (optionsSaved != null)
                    {
                        optionsSaved.IsNewBranchMode = _isNewBranchMode;
                        _ = System.Threading.Tasks.Task.Run(async () =>
                        {
                            try
                            {
                                await optionsSaved.SaveAsync().ConfigureAwait(false);
                            }
                            catch (Exception ex)
                            {
                                if (_loggingService != null)
                                {
                                    await _loggingService.WriteToOutputWindowAsync($"Failed to save settings: {ex.Message}").ConfigureAwait(false);
                                }
                            }
                        });
                    }
                }
            }
        }

        public bool IsExistingBranchMode
        {
            get { return !IsNewBranchMode; }
            set
            {
                IsNewBranchMode = !value;
            }
        }

        private string _newBranchName;
        public string NewBranchName
        {
            get { return _newBranchName; }
            set
            {
                if (_newBranchName != value)
                {
                    _newBranchName = value;
                    _newBranchNameChanged = true;
                    OnPropertyChanged(nameof(NewBranchName));
                    CommandManager.InvalidateRequerySuggested();
                    UpdateFolderPath();
                }
            }
        }

        private string _windowTitle;
        public string WindowTitle
        {
            get { return _windowTitle; }
            set
            {
                if (_windowTitle != value)
                {
                    _windowTitle = value;
                    OnPropertyChanged(nameof(WindowTitle));
                }
            }
        }

        private string _activeRepositoryPath;
        public string ActiveRepositoryPath
        {
            get { return _activeRepositoryPath; }
            set
            {
                _activeRepositoryPath = value;
                OnPropertyChanged(nameof(ActiveRepositoryPath));
                PopulateBranches_Worktrees();
            }
        }

        private BranchInfo _selectedBranch;
        public BranchInfo SelectedBranch
        {
            get { return _selectedBranch; }
            set
            {
                _selectedBranch = value;
                OnPropertyChanged(nameof(SelectedBranch));
                CommandManager.InvalidateRequerySuggested();
                UpdateFolderPath();
            }
        }

        private string _folderPath;
        public string FolderPath
        {
            get { return _folderPath; }
            set
            {
                if (_folderPath != value)
                {
                    _folderPath = value;
                    _folderPathChanged = true;
                    OnPropertyChanged(nameof(FolderPath));
                }
            }
        }

        public OpenBehavior PreferredCreateAction
        {
            get => optionsSaved?.PreferredCreateAction ?? OpenBehavior.NewVSWindow;
            set
            {
                if (optionsSaved != null && optionsSaved.PreferredCreateAction != value)
                {
                    optionsSaved.PreferredCreateAction = value;
                    OnPropertyChanged(nameof(PreferredCreateAction));
                    SaveSettings();
                }
            }
        }

        public OpenBehavior PreferredOpenAction
        {
            get => optionsSaved?.PreferredOpenAction ?? OpenBehavior.NewVSWindow;
            set
            {
                if (optionsSaved != null && optionsSaved.PreferredOpenAction != value)
                {
                    optionsSaved.PreferredOpenAction = value;
                    OnPropertyChanged(nameof(PreferredOpenAction));
                    SaveSettings();
                }
            }
        }

        private void SaveSettings()
        {
            if (optionsSaved != null)
            {
                try
                {
                    if (ThreadHelper.JoinableTaskFactory == null)
                    {
                        // Skip persistence in unit tests where VS Shell/threading is not initialized
                        return;
                    }

                    ThreadHelper.JoinableTaskFactory.Run(async () =>
                    {
                        try
                        {
                            await optionsSaved.SaveAsync().ConfigureAwait(false);
                        }
                        catch (Exception ex)
                        {
                            if (_loggingService != null)
                                await _loggingService.WriteToOutputWindowAsync($"Failed to save settings: {ex.Message}").ConfigureAwait(false);
                        }
                    });
                }
                catch
                {
                    // Fallback to prevent crashes in unit test environments
                }
            }
        }

        private bool _isBusy;
        public bool IsBusy
        {
            get => _isBusy;
            set
            {
                if (_isBusy != value)
                {
                    _isBusy = value;
                    OnPropertyChanged(nameof(IsBusy));
                    OnPropertyChanged(nameof(IsNotBusy));
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        public bool IsNotBusy => !_isBusy;

        #endregion

        #region IDataErrorInfo Validation
        private bool IsValid
        {
            get
            {
                // Check if all properties are valid
                return string.IsNullOrEmpty(Validate());
            }
        }
        private bool IsValidBranchName(string branchName)
        {
            if (string.IsNullOrWhiteSpace(branchName)) return false;
            if (branchName.Any(char.IsWhiteSpace)) return false;
            if (branchName.StartsWith("/") || branchName.EndsWith("/")) return false;
            if (branchName.Contains("//")) return false;
            if (branchName.Contains("..")) return false;
            if (branchName.Any(c => "~^:?*[\\<>|#\"%&`!@{}()".Contains(c))) return false;
            return true;
        }

        private string GetErrorForProperty(string propertyName)
        {
            if (propertyName == nameof(ActiveRepositoryPath))
            {
                if (string.IsNullOrEmpty(ActiveRepositoryPath))
                    return "No repository loaded";
            }
            if (propertyName == nameof(SelectedBranch))
            {
                if (SelectedBranch == null || string.IsNullOrEmpty(SelectedBranch.Name))
                    return "No valid branch/Worktree selected";

                if (commandType == CommandType.Create && IsExistingBranchMode && SelectedBranch.HasLinkedWorktree)
                    return "This branch already has a worktree.";
            }
            if (propertyName == nameof(NewBranchName))
            {
                if (IsNewBranchMode)
                {
                    if (_newBranchNameChanged || _showAllErrors)
                    {
                        if (string.IsNullOrWhiteSpace(NewBranchName))
                        {
                            return "Branch name cannot be empty";
                        }
                        if (NewBranchName.Any(char.IsWhiteSpace))
                        {
                            return "Branch name cannot contain spaces";
                        }
                        if (NewBranchName.StartsWith("/") || NewBranchName.EndsWith("/"))
                        {
                            return "Branch name cannot start or end with a slash";
                        }
                        if (NewBranchName.Contains("//"))
                        {
                            return "Branch name cannot contain consecutive slashes";
                        }
                        if (NewBranchName.Contains(".."))
                        {
                            return "Branch name cannot contain '..'";
                        }
                        if (NewBranchName.Any(c => "~^:?*[\\<>|#\"%&`!@{}()".Contains(c)))
                        {
                            return "Branch name contains invalid characters";
                        }
                        
                        var cleanedNew = NewBranchName.Trim();
                        if (Branches_Worktrees != null && Branches_Worktrees.Any(b => b.Name.Equals(cleanedNew, StringComparison.OrdinalIgnoreCase)))
                        {
                            return "Branch name already exists";
                        }
                    }
                }
            }
            if (commandType != CommandType.Manage && propertyName == nameof(FolderPath))
            {
                if (_folderPathChanged || _showAllErrors)
                {
                    if (string.IsNullOrEmpty(FolderPath) || !IsValidPath(FolderPath))
                        return "Please enter a valid path for worktree";
                }
            }
            return "";
        }

        protected override string Validate(string propertyName = null)
        {
            string overallError = "";
            if (string.IsNullOrEmpty(overallError)) overallError = GetErrorForProperty(nameof(ActiveRepositoryPath));
            if (string.IsNullOrEmpty(overallError)) overallError = GetErrorForProperty(nameof(SelectedBranch));
            if (string.IsNullOrEmpty(overallError)) overallError = GetErrorForProperty(nameof(NewBranchName));
            if (string.IsNullOrEmpty(overallError)) overallError = GetErrorForProperty(nameof(FolderPath));

            _loggingService?.UpdateStatusBar(overallError);
            return propertyName == null ? overallError : GetErrorForProperty(propertyName);
        }

        private bool IsValidPath(string path)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(path)) return false;
                Path.GetFullPath(path);
                return true;
            }
            catch
            {
                return false;
            }
        }
        #endregion

        public bool IsWorktreeCreated { get; private set; }

        public WorkTreeDialogViewModel() : this(null, CommandType.Create, null, null, null, null) { }
        
        public WorkTreeDialogViewModel(
            string gitRepositoryPath, 
            CommandType commandType, 
            General optionsSaved,
            IGitService gitService = null,
            ISolutionService solutionService = null,
            ILoggingService loggingService = null,
            IServiceProvider serviceProvider = null)
        {
            _loggingService = loggingService ?? LoggingHelper.Instance;
            _gitService = gitService ?? new GitHelper(_loggingService);
            _solutionService = solutionService ?? new SolutionHelper(_loggingService, _gitService);
            _serviceProvider = serviceProvider;

            //init fields
            this.commandType = commandType;
            this.optionsSaved = optionsSaved;
            Branches_Worktrees = new ObservableCollection<BranchInfo>();

            //set title
            if (this.commandType == CommandType.Create)
            {
                WindowTitle = "Create New Worktree";
                IsNewBranchMode = optionsSaved?.IsNewBranchMode ?? false;
            }
            else if (commandType == CommandType.Manage)
            {
                WindowTitle = "Manage Existing Worktrees";
            }

            //set repo and folder path (branch/worktree updated based on repo set)
            ActiveRepositoryPath = gitRepositoryPath;

            // Initialize commands
            PruneCommand = new AsyncRelayCommand("Prune Worktrees", async obj => await Prune_WorkTree(), null, _loggingService);
            CreateCommand = new AsyncRelayCommand("Create Worktree", async obj => await Create_WorkTree(obj), obj => IsValid, _loggingService);
            RemoveCommand = new AsyncRelayCommand("Remove Worktree", async obj => await Remove_WorkTree(), obj => IsValid, _loggingService);
            OpenCommand = new AsyncRelayCommand("Open Worktree", async obj => await Open_WorkTree(obj), obj => IsValid, _loggingService);
            CancelCommand = new RelayCommand("Cancel Dialog", obj => Close_Dialog(), null, _loggingService);
            OpenOptionsCommand = new AsyncRelayCommand("Open Settings", async obj => await OpenOptionsAsync(), null, _loggingService);

            _newBranchNameChanged = false;
            _folderPathChanged = false;
            _showAllErrors = false;
        }

        private List<string> GetBranches_Worktrees()
        {
            _loggingService?.SetCommandStatusBusy();
            
            bool useFallback = false;
            try
            {
                var factory = ThreadHelper.JoinableTaskFactory;
                if (factory == null)
                {
                    useFallback = true;
                }
            }
            catch
            {
                useFallback = true;
            }

            if (useFallback)
            {
                if (commandType == CommandType.Create)
                    return _gitService.GetBranchesAsync(_activeRepositoryPath).GetAwaiter().GetResult();
                else if (commandType == CommandType.Manage)
                    return _gitService.GetWorkTreePathsAsync(_activeRepositoryPath).GetAwaiter().GetResult();
                return null;
            }

            return ThreadHelper.JoinableTaskFactory.Run(async () =>
            {
                if (commandType == CommandType.Create)
                    return await _gitService.GetBranchesAsync(_activeRepositoryPath).ConfigureAwait(false);
                else if (commandType == CommandType.Manage)
                    return await _gitService.GetWorkTreePathsAsync(_activeRepositoryPath).ConfigureAwait(false);
                return null;
            });
        }

        private async Task<bool> PopulateBranches_Worktrees()
        {
            try
            {
                List<string> branches_Worktrees = GetBranches_Worktrees();
                if (branches_Worktrees == null)
                {
                    _loggingService?.UpdateStatusBar("No Branches/Worktrees available for this repository");
                    return false;
                }

                bool switchThread = false;
                try
                {
                    if (ThreadHelper.JoinableTaskFactory != null)
                    {
                        switchThread = true;
                    }
                }
                catch
                {
                    switchThread = false;
                }

                if (switchThread)
                {
                    await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                }
                Branches_Worktrees.Clear();

                if (commandType == CommandType.Create)
                {
                    var uniqueBranches = new Dictionary<string, (string originalRef, bool hasWorktree)>(StringComparer.OrdinalIgnoreCase);
                    var insertionOrder = new List<string>();

                    foreach (var item in branches_Worktrees)
                    {
                        string trimmed = item.Trim();
                        if (string.IsNullOrWhiteSpace(trimmed) || trimmed.Contains("HEAD") || trimmed.Contains("->"))
                        {
                            continue;
                        }

                        bool hasWorktree = false;
                        string cleanName = trimmed;
                        if (trimmed.StartsWith("+ "))
                        {
                            cleanName = trimmed.Substring(2).Trim();
                            hasWorktree = true;
                        }
                        else if (trimmed.StartsWith("+"))
                        {
                            cleanName = trimmed.Substring(1).Trim();
                            hasWorktree = true;
                        }

                        string logicalName = cleanName.ToGitCommandExecutableFormat();
                        if (string.IsNullOrEmpty(logicalName))
                        {
                            continue;
                        }

                        if (!uniqueBranches.ContainsKey(logicalName))
                        {
                            uniqueBranches[logicalName] = (trimmed, hasWorktree);
                            insertionOrder.Add(logicalName);
                        }
                        else
                        {
                            var existing = uniqueBranches[logicalName];
                            bool mergedHasWorktree = existing.hasWorktree || hasWorktree;
                            string originalRef = existing.originalRef;

                            if (originalRef.StartsWith("remotes/", StringComparison.OrdinalIgnoreCase) && !trimmed.StartsWith("remotes/", StringComparison.OrdinalIgnoreCase))
                            {
                                originalRef = trimmed;
                            }

                            uniqueBranches[logicalName] = (originalRef, mergedHasWorktree);
                        }
                    }

                    foreach (var logicalName in insertionOrder)
                    {
                        var data = uniqueBranches[logicalName];
                        string originalRef = data.originalRef;
                        if (originalRef.StartsWith("+ "))
                        {
                            originalRef = originalRef.Substring(2).Trim();
                        }
                        else if (originalRef.StartsWith("+"))
                        {
                            originalRef = originalRef.Substring(1).Trim();
                        }

                        Branches_Worktrees.Add(new BranchInfo(logicalName, originalRef, data.hasWorktree));
                    }
                }
                else
                {
                    foreach (var item in branches_Worktrees)
                    {
                        string path = item.Trim();
                        Branches_Worktrees.Add(new BranchInfo(path, path, false));
                    }
                }

                SelectedBranch = (commandType == CommandType.Create && IsExistingBranchMode)
                    ? (Branches_Worktrees.FirstOrDefault(b => !b.HasLinkedWorktree) ?? Branches_Worktrees.FirstOrDefault())
                    : Branches_Worktrees.FirstOrDefault();
                _loggingService?.SetCommandStatusBusy(false);
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception in PopulateBranches_Worktrees: {ex}");
                await _loggingService?.WriteToOutputWindowAsync($"Failed to Load Branches/Worktrees: {ex.Message}");
                return false;
            }
        }

        private async Task SafeSwitchToMainThreadAsync()
        {
            if (System.Windows.Application.Current != null)
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            }
        }

        private async Task<bool> UpdateFolderPath()
        {
            try
            {
                if (!(commandType == CommandType.Create)) return false;

                string worktreePath = Path.Combine(Directory.GetParent(_activeRepositoryPath).FullName, $"{_activeRepositoryPath}_Worktrees");
                string pathPrefix;
                if (!String.IsNullOrWhiteSpace(optionsSaved.WorktreeSubFolder))
                    pathPrefix = Path.Combine(_activeRepositoryPath, optionsSaved.WorktreeSubFolder);
                else if (!String.IsNullOrEmpty(optionsSaved.DefaultWorktreeDirectory))
                    pathPrefix = optionsSaved.DefaultWorktreeDirectory;
                else
                    pathPrefix = worktreePath;

                string branchToUse = IsNewBranchMode ? NewBranchName : SelectedBranch?.Name;
                if (string.IsNullOrEmpty(branchToUse))
                {
                    await SafeSwitchToMainThreadAsync();
                    _folderPath = pathPrefix;
                    OnPropertyChanged(nameof(FolderPath));
                    return true;
                }

                string branchNameForValidation = branchToUse.ToGitCommandExecutableFormat();
                if (!IsValidBranchName(branchNameForValidation))
                {
                    await SafeSwitchToMainThreadAsync();
                    _folderPath = null;
                    OnPropertyChanged(nameof(FolderPath));
                    return false;
                }

                string cleanedBranchName = branchToUse.ToFolderFormat(optionsSaved?.PreserveBranchHierarchy ?? true);

                await SafeSwitchToMainThreadAsync();
                _folderPath = Path.Combine(pathPrefix, cleanedBranchName);
                OnPropertyChanged(nameof(FolderPath));
                return true;
            }
            catch (Exception ex)
            {
                await SafeSwitchToMainThreadAsync();
                _folderPath = null;
                OnPropertyChanged(nameof(FolderPath));
                await _loggingService?.WriteToOutputWindowAsync($"Failed to set Path: {ex.Message}");
                return false;
            }
        }

        public IAsyncCommand PruneCommand { get; }
        public IAsyncCommand CreateCommand { get; }
        public IAsyncCommand RemoveCommand { get; }
        public IAsyncCommand OpenCommand { get; }
        public ICommand CancelCommand { get; }
        public IAsyncCommand OpenOptionsCommand { get; }

        private async Task OpenOptionsAsync()
        {
            try
            {
                await SafeSwitchToMainThreadAsync();
                await VS.Settings.OpenAsync<OptionsProvider.GeneralOptions>();
            }
            catch (Exception ex)
            {
                if (_loggingService != null)
                {
                    await _loggingService.WriteToOutputWindowAsync($"Failed to open options: {ex.Message}");
                }
            }
        }

        private async Task<bool> Prune_WorkTree()
        {
            try
            {
                IsBusy = true;
                var result = await _gitService.PruneAsync(_activeRepositoryPath).ConfigureAwait(false);
                if (!result.Success)
                {
                    DialogHelper.ShowOperationError(_serviceProvider, "Prune Worktrees", result.ErrorMessage);
                    return false;
                }
                return await PopulateBranches_Worktrees().ConfigureAwait(false);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task<bool> Create_WorkTree(object parameter)
        {
            OpenBehavior behavior = PreferredCreateAction;
            if (parameter is OpenBehavior b)
            {
                behavior = b;
            }
            else if (parameter is string bStr && Enum.TryParse(bStr, out OpenBehavior parsedB))
            {
                behavior = parsedB;
            }

            PreferredCreateAction = behavior;

            _showAllErrors = true;
            OnPropertyChanged(nameof(NewBranchName));
            OnPropertyChanged(nameof(FolderPath));
            OnPropertyChanged(nameof(SelectedBranch));

            if (!IsValid) return false;

            string targetBranch = IsNewBranchMode ? NewBranchName.Trim() : SelectedBranch?.Name;
            if (_loggingService != null)
            {
                await _loggingService.WriteToOutputWindowAsync($"Creating worktree for branch '{targetBranch}'...", true);
            }

            try
            {
                IsBusy = true;
                if (IsNewBranchMode)
                {
                    string newBranch = NewBranchName.Trim();
                    string sourceBranch = SelectedBranch?.FullRef;

                    // 1. Create new branch
                    var branchResult = await _gitService.CreateBranchAsync(_activeRepositoryPath, newBranch, sourceBranch).ConfigureAwait(false);
                    if (!branchResult.Success)
                    {
                        _loggingService?.UpdateStatusBar("Failed to create new branch.");
                        DialogHelper.ShowOperationError(_serviceProvider, "Create Branch", branchResult.ErrorMessage);
                        return false;
                    }

                    // 2. Create worktree
                    var worktreeResult = await _gitService.CreateWorkTreeAsync(_activeRepositoryPath, newBranch, _folderPath).ConfigureAwait(false);
                    if (worktreeResult.Success)
                    {
                        IsWorktreeCreated = true;
                        Close_Dialog();
                        if (behavior == OpenBehavior.NewVSWindow)
                        {
                            await _solutionService.OpenSolution(FolderPath, false).ConfigureAwait(false);
                        }
                        else if (behavior == OpenBehavior.CurrentWindow)
                        {
                            await _solutionService.OpenSolution(FolderPath, true).ConfigureAwait(false);
                        }
                        return true;
                    }
                    else
                    {
                        _loggingService?.UpdateStatusBar("Failed to create worktree. Rolling back branch creation.");
                        DialogHelper.ShowOperationError(_serviceProvider, "Create Worktree", worktreeResult.ErrorMessage);
                        // Cleanup branch
                        var cleanupResult = await _gitService.DeleteBranchAsync(_activeRepositoryPath, newBranch).ConfigureAwait(false);
                        if (!cleanupResult.Success)
                        {
                            await _loggingService?.WriteToOutputWindowAsync($"[Cleanup Failure] Failed to remove the orphan branch '{newBranch}'.");
                        }
                        return false;
                    }
                }
                else
                {
                    var worktreeResult = await _gitService.CreateWorkTreeAsync(_activeRepositoryPath, SelectedBranch?.FullRef, _folderPath).ConfigureAwait(false);
                    if (worktreeResult.Success)
                    {
                        IsWorktreeCreated = true;
                        Close_Dialog();
                        if (behavior == OpenBehavior.NewVSWindow)
                        {
                            await _solutionService.OpenSolution(FolderPath, false).ConfigureAwait(false);
                        }
                        else if (behavior == OpenBehavior.CurrentWindow)
                        {
                            await _solutionService.OpenSolution(FolderPath, true).ConfigureAwait(false);
                        }
                        return true;
                    }
                    DialogHelper.ShowOperationError(_serviceProvider, "Create Worktree", worktreeResult.ErrorMessage);
                    return false;
                }
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task<bool> Remove_WorkTree()
        {
            if (!IsValid) return false;
 
            try
            {
                IsBusy = true;
                var result = await _gitService.RemoveWorkTreeAsync(_activeRepositoryPath, SelectedBranch?.Name, false).ConfigureAwait(false);
                if (result.Success)
                {
                    await PopulateBranches_Worktrees().ConfigureAwait(false);
                    return true;
                }
                DialogHelper.ShowOperationError(_serviceProvider, "Remove Worktree", result.ErrorMessage);
                return false;
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task<bool> Open_WorkTree(object parameter)
        {
            OpenBehavior behavior = PreferredOpenAction;
            if (parameter is OpenBehavior b)
            {
                behavior = b;
            }
            else if (parameter is string bStr && Enum.TryParse(bStr, out OpenBehavior parsedB))
            {
                behavior = parsedB;
            }

            PreferredOpenAction = behavior;

            if (!IsValid) return false;
 
            try
            {
                IsBusy = true;
                if (behavior == OpenBehavior.Explorer)
                {
                    try
                    {
                        System.Diagnostics.Process.Start("explorer.exe", SelectedBranch?.Name);
                        return true;
                    }
                    catch (Exception ex)
                    {
                        await _loggingService?.WriteToOutputWindowAsync($"Failed to open folder in Explorer: {ex.Message}");
                        return false;
                    }
                }

                if (behavior == OpenBehavior.DoNotOpen)
                {
                    behavior = OpenBehavior.NewVSWindow;
                }

                bool openInCurrent = (behavior == OpenBehavior.CurrentWindow);
                if (openInCurrent) Close_Dialog();
                return await _solutionService.OpenSolution(SelectedBranch?.Name, openInCurrent).ConfigureAwait(false);
            }
            finally
            {
                IsBusy = false;
            }
        }

        public bool Close_Dialog()
        {
            try
            {
                ThreadHelper.JoinableTaskFactory.Run(async () =>
                {
                    await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                    var window = System.Windows.Application.Current?.Windows?.OfType<System.Windows.Window>().FirstOrDefault(w => w.DataContext == this);
                    if (window != null) window.Close();
                });
            }
            catch
            {
                // Graceful fallback for unit tests where VS SDK ThreadHelper is not initialized
            }
            return true;
        }
    }
}
