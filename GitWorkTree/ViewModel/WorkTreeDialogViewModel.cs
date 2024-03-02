using GitWorkTree.Commands;
using GitWorkTree.Helpers;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Input;

namespace GitWorkTree.ViewModel
{
    public class WorkTreeDialogViewModel : BaseViewModel
    {
        private readonly LoggingHelper outputWindow = LoggingHelper.Instance;
        private General optionsSaved;

        #region UI Related Properties
        public ObservableCollection<string> Branches_Worktrees { get; set; }

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
                _ = LoadBranches_WorktreesAsync();
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
                    OnPropertyChanged(nameof(FolderPath));
                }
            }
        }

        private bool _ifOpenInNewVisualStudio;
        public bool IfOpenInNewVisualStudio
        {
            get { return _ifOpenInNewVisualStudio; }
            set
            {
                _ifOpenInNewVisualStudio = value;
                OnPropertyChanged(nameof(IfOpenInNewVisualStudio));
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
            if (propertyName == nameof(ActiveRepositoryPath))
            {
                if (string.IsNullOrEmpty(ActiveRepositoryPath))
                    errorStatus = "No repository available";
            }
            if (propertyName == nameof(SelectedBranch_Worktree))
            {
                if (string.IsNullOrEmpty(SelectedBranch_Worktree))
                    errorStatus = "Please enter a valid branch/Worktree";
            }
            else if (propertyName == nameof(FolderPath))
            {
                if (string.IsNullOrEmpty(FolderPath))
                    errorStatus = "Please enter a valid path for worktree";
            }

            outputWindow.UpdateStatusBar(errorStatus);
            return errorStatus;
        }
        #endregion

        public WorkTreeDialogViewModel() { }
        public WorkTreeDialogViewModel(string gitRepositoryPath, CommandType commandType, General optionsSaved)
        {
            //init fields
            this.commandType = commandType;
            this.optionsSaved = optionsSaved;
            Branches_Worktrees = new ObservableCollection<string>();

            //set title
            if (this.commandType == CommandType.Create)
            {
                WindowTitle = "Create New Worktree";
            }
            else if (commandType == CommandType.Manage)
            {
                WindowTitle = "Manage Existing Worktrees";
                IfOpenInNewVisualStudio = true;
            }

            //set repo and folder path (branch/worktree updated based on repo set)
            ActiveRepositoryPath = gitRepositoryPath;
            FolderPath = optionsSaved.DefaultBranchPath == "" ? _activeRepositoryPath : optionsSaved.DefaultBranchPath;
        }

        private async Task LoadBranches_WorktreesAsync()
        {
            if (string.IsNullOrEmpty(ActiveRepositoryPath))
            {
                outputWindow.UpdateStatusBar("No Repository loaded");
                return;
            }
            List<string> branches_Worktrees = null;
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            var status = $"Loading {(commandType == CommandType.Create ? "Branches" : "Worktrees")}...";
            try
            {
                outputWindow.UpdateStatusBar(status);
                if (commandType == CommandType.Create)
                    branches_Worktrees = await GitHelper.GetBranchesAsync(ActiveRepositoryPath).ConfigureAwait(false);
                else if (commandType == CommandType.Manage)
                    branches_Worktrees = await GitHelper.GetWorkTreePathsAsync(ActiveRepositoryPath).ConfigureAwait(false);

                await PopulateBranches_Worktrees(branches_Worktrees).ConfigureAwait(false);
                if (branches_Worktrees != null) outputWindow?.UpdateStatusBar("", status);
                UpdateFolderPath();
            }
            catch (Exception ex)
            {
                outputWindow?.WriteToOutputWindowAsync($"{status} failed {ex.Message}");
            }

        }

        private async Task PopulateBranches_Worktrees(List<string> branches_Worktrees)
        {
            if (branches_Worktrees == null) { outputWindow?.UpdateStatusBar("No Branches/Worktrees available for this repository"); return; }
            await ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                Branches_Worktrees.Clear();
                foreach (var item in branches_Worktrees)
                {
                    Branches_Worktrees.Add(item);
                }
                SelectedBranch_Worktree = Branches_Worktrees.FirstOrDefault();
            });
        }

        private void UpdateFolderPath()
        {
            if (!string.IsNullOrEmpty(SelectedBranch_Worktree))
            {
                string cleanedBranchName = SelectedBranch_Worktree.ToFolderFormat();
                string pathPrefix = string.IsNullOrEmpty(optionsSaved.DefaultBranchPath) ? _activeRepositoryPath : optionsSaved.DefaultBranchPath;
                FolderPath = $@"{pathPrefix}\{cleanedBranchName}";
            }
        }

        public ICommand PruneCommand => new RelayCommand(async obj => await Prune());
        public ICommand CreateCommand => new RelayCommand(async obj => await CreateRemoveWorkTree());
        public ICommand OpenCommand => new RelayCommand(async obj => await OpenWorkTree());
        public ICommand CancelCommand => new RelayCommand(obj => CloseDialog());

        private async Task Prune()
        {
            if (await GitHelper.PruneAsync(ActiveRepositoryPath).ConfigureAwait(false))
                await LoadBranches_WorktreesAsync().ConfigureAwait(false);
        }

        private async Task CreateRemoveWorkTree()
        {
            if (!IsValid) return;

            if (CommandType == CommandType.Create)
            {
                CloseDialog();

                // Run the asynchronous operation without capturing the synchronization context 
                if (await GitHelper.CreateWorkTreeAsync(ActiveRepositoryPath, SelectedBranch_Worktree, FolderPath, IsForceCreateRemove).ConfigureAwait(false))
                    if (optionsSaved.IsLoadSolution) await SolutionHelper.OpenSolutionAsync(FolderPath, true).ConfigureAwait(false);
            }
            else if (CommandType == CommandType.Manage)
            {
                if (SelectedBranch_Worktree != null)
                {
                    if (await GitHelper.RemoveWorkTreeAsync(ActiveRepositoryPath, SelectedBranch_Worktree, IsForceCreateRemove).ConfigureAwait(false))
                        await LoadBranches_WorktreesAsync().ConfigureAwait(false);
                }
            }
        }
        private async Task OpenWorkTree()
        {
            if (!IsValid) return;
            if (!IfOpenInNewVisualStudio) CloseDialog();
            await SolutionHelper.OpenSolutionAsync(SelectedBranch_Worktree, !IfOpenInNewVisualStudio).ConfigureAwait(false);
        }

        public void CloseDialog()
        {
            var window = System.Windows.Application.Current.Windows.OfType<System.Windows.Window>().FirstOrDefault(w => w.DataContext == this);
            if (window != null) window.Close();
        }
    }
}



