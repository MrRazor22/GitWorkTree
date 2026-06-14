using GitWorkTree.Commands;
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

        #region UI Related Properties
        public ObservableCollection<string> Branches_Worktrees { get; set; }

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

        private string _selectedBranch_Worktree;
        public string SelectedBranch_Worktree
        {
            get { return _selectedBranch_Worktree; }
            set
            {
                _selectedBranch_Worktree = value;
                OnPropertyChanged(nameof(SelectedBranch_Worktree));
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

        private bool _isForceCreateRemove;
        public bool IsForceCreateRemove
        {
            get { return _isForceCreateRemove; }
            set
            {
                _isForceCreateRemove = value;
                OnPropertyChanged(nameof(IsForceCreateRemove));
            }
        }
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
        protected override string Validate(string propertyName = null)
        {
            string errorStatus = "";
            // data validation logic here
            if (propertyName == null || propertyName == nameof(ActiveRepositoryPath))
            {
                if (string.IsNullOrEmpty(ActiveRepositoryPath))
                    errorStatus = "No repository loaded";
            }
            if (string.IsNullOrEmpty(errorStatus) && (propertyName == null || propertyName == nameof(SelectedBranch_Worktree)))
            {
                if (string.IsNullOrEmpty(SelectedBranch_Worktree))
                    errorStatus = "No valid branch/Worktree selected";
            }
            if (string.IsNullOrEmpty(errorStatus) && (propertyName == null || propertyName == nameof(NewBranchName)))
            {
                if (IsNewBranchMode)
                {
                    if (_newBranchNameChanged || _showAllErrors)
                    {
                        if (string.IsNullOrWhiteSpace(NewBranchName))
                        {
                            errorStatus = "Branch name cannot be empty";
                        }
                        else if (NewBranchName.Any(char.IsWhiteSpace))
                        {
                            errorStatus = "Branch name cannot contain spaces";
                        }
                        else if (NewBranchName.Any(c => "~^:?*[\\<>|#\"%&`!@{}()".Contains(c)))
                        {
                            errorStatus = "Branch name contains invalid characters";
                        }
                        else
                        {
                            var cleanedNew = NewBranchName.Trim();
                            // check duplicate in local/remote branches (we loaded all strings from GetBranchesAsync)
                            if (Branches_Worktrees != null && Branches_Worktrees.Any(b => b.ToGitCommandExecutableFormat().Equals(cleanedNew, StringComparison.OrdinalIgnoreCase)))
                            {
                                errorStatus = "Branch name already exists";
                            }
                        }
                    }
                }
            }
            if (string.IsNullOrEmpty(errorStatus) && commandType != CommandType.Manage && (propertyName == null || propertyName == nameof(FolderPath)))
            {
                if (_folderPathChanged || _showAllErrors)
                {
                    if (string.IsNullOrEmpty(FolderPath) || !IsValidPath(FolderPath))
                        errorStatus = "Please enter a valid path for worktree";
                }
            }

            _loggingService?.UpdateStatusBar(errorStatus);
            return errorStatus;
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

        public WorkTreeDialogViewModel() : this(null, CommandType.Create, null, null, null, null) { }
        
        public WorkTreeDialogViewModel(
            string gitRepositoryPath, 
            CommandType commandType, 
            General optionsSaved,
            IGitService gitService = null,
            ISolutionService solutionService = null,
            ILoggingService loggingService = null)
        {
            _loggingService = loggingService ?? LoggingHelper.Instance;
            _gitService = gitService ?? new GitHelper(_loggingService);
            _solutionService = solutionService ?? new SolutionHelper(_loggingService, _gitService);

            //init fields
            this.commandType = commandType;
            this.optionsSaved = optionsSaved;
            Branches_Worktrees = new ObservableCollection<string>();

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
            PruneCommand = new AsyncRelayCommand(async obj => await Prune_WorkTree(), null, _loggingService);
            CreateCommand = new AsyncRelayCommand(async obj => await Create_WorkTree(obj), obj => IsValid, _loggingService);
            RemoveCommand = new AsyncRelayCommand(async obj => await Remove_WorkTree(), obj => IsValid, _loggingService);
            OpenCommand = new AsyncRelayCommand(async obj => await Open_WorkTree(obj), obj => IsValid, _loggingService);
            CancelCommand = new RelayCommand(obj => Close_Dialog(), null, _loggingService);

            _newBranchNameChanged = false;
            _folderPathChanged = false;
            _showAllErrors = false;
        }

        private List<string> GetBranches_Worktrees()
        {
            _loggingService?.SetCommandStatusBusy();
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
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                Branches_Worktrees.Clear();
                foreach (var item in branches_Worktrees)
                {
                    Branches_Worktrees.Add(item);
                }
                SelectedBranch_Worktree = Branches_Worktrees.FirstOrDefault();
                _loggingService?.SetCommandStatusBusy(false);
                return true;
            }
            catch (Exception ex)
            {
                await _loggingService?.WriteToOutputWindowAsync($"Failed to Load Branches/Worktrees: {ex.Message}");
                return false;
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

                string branchToUse = IsNewBranchMode ? NewBranchName : SelectedBranch_Worktree;
                if (string.IsNullOrEmpty(branchToUse))
                {
                    await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                    FolderPath = IsNewBranchMode ? pathPrefix : null;
                    return IsNewBranchMode;
                }

                if (branchToUse.Any(c => "~^:?*[\\<>|#\"%&`!@{}()".Contains(c)))
                {
                    await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                    FolderPath = null;
                    return false;
                }

                string cleanedBranchName = branchToUse.ToFolderFormat();

                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                FolderPath = Path.Combine(pathPrefix, cleanedBranchName);
                return true;
            }
            catch (Exception ex)
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                FolderPath = null;
                await _loggingService?.WriteToOutputWindowAsync($"Failed to set Path: {ex.Message}");
                return false;
            }
        }

        public IAsyncCommand PruneCommand { get; }
        public IAsyncCommand CreateCommand { get; }
        public IAsyncCommand RemoveCommand { get; }
        public IAsyncCommand OpenCommand { get; }
        public ICommand CancelCommand { get; }

        private async Task<bool> Prune_WorkTree()
        {
            return await _gitService.PruneAsync(_activeRepositoryPath).ConfigureAwait(false) &&
                   await PopulateBranches_Worktrees().ConfigureAwait(false);
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
            OnPropertyChanged(nameof(SelectedBranch_Worktree));

            if (!IsValid) return false;

            if (IsNewBranchMode)
            {
                string newBranch = NewBranchName.Trim();
                string sourceBranch = SelectedBranch_Worktree;

                // 1. Create new branch
                if (!await _gitService.CreateBranchAsync(_activeRepositoryPath, newBranch, sourceBranch).ConfigureAwait(false))
                {
                    _loggingService?.UpdateStatusBar("Failed to create new branch.");
                    return false;
                }

                // 2. Create worktree
                if (await _gitService.CreateWorkTreeAsync(_activeRepositoryPath, newBranch, _folderPath, _isForceCreateRemove).ConfigureAwait(false))
                {
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
                    // Cleanup branch
                    if (!await _gitService.DeleteBranchAsync(_activeRepositoryPath, newBranch).ConfigureAwait(false))
                    {
                        await _loggingService?.WriteToOutputWindowAsync($"[Cleanup Failure] Failed to remove the orphan branch '{newBranch}'.");
                    }
                    return false;
                }
            }
            else
            {
                if (await _gitService.CreateWorkTreeAsync(_activeRepositoryPath, _selectedBranch_Worktree, _folderPath, _isForceCreateRemove).ConfigureAwait(false))
                {
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
                return false;
            }
        }

        private async Task<bool> Remove_WorkTree()
        {
            if (!IsValid) return false;

            if (await _gitService.RemoveWorkTreeAsync(_activeRepositoryPath, _selectedBranch_Worktree, _isForceCreateRemove).ConfigureAwait(false))
            {
                await PopulateBranches_Worktrees().ConfigureAwait(false);
                return true;
            }
            return false;
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

            if (behavior == OpenBehavior.Explorer)
            {
                try
                {
                    System.Diagnostics.Process.Start("explorer.exe", _selectedBranch_Worktree);
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
            return await _solutionService.OpenSolution(_selectedBranch_Worktree, openInCurrent).ConfigureAwait(false);
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
