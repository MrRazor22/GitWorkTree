using GitWorkTree.Commands;
using GitWorkTree.Helpers;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Input;

namespace GitWorkTree.ViewModel
{
    public class WorkTreeDialogViewModel : INotifyPropertyChanged, IDataErrorInfo
    {
        private readonly LoggingHelper outputWindow = LoggingHelper.Instance;
        private string _defaultPath;

        #region IDataErrorInfo Props
        public string this[string columnName]
        {
            get
            {
                string errorStatus = "";
                // data validation logic here
                if (columnName == nameof(ActiveRepositoryPath))
                {
                    if (string.IsNullOrEmpty(ActiveRepositoryPath))
                        errorStatus = "No repository available";
                }
                if (columnName == nameof(SelectedBranch_Worktree))
                {
                    if (string.IsNullOrEmpty(SelectedBranch_Worktree))
                        errorStatus = "Please enter a valid branch/Worktree";
                }
                else if (columnName == nameof(FolderPath))
                {
                    if (string.IsNullOrEmpty(FolderPath))
                        errorStatus = "Please enter a valid path for worktree";
                }

                outputWindow.UpdateStatusBar(errorStatus);
                return errorStatus;
            }
        }
        public string Error => null;
        #endregion

        #region UI Related Properties
        public ObservableCollection<string> Branches_Worktrees { get; set; }
        private bool IsValid
        {
            get
            {
                // Check if all properties are valid
                return string.IsNullOrEmpty(this[nameof(ActiveRepositoryPath)])
                    && string.IsNullOrEmpty(this[nameof(SelectedBranch_Worktree)])
                    && string.IsNullOrEmpty(this[nameof(FolderPath)]);
            }
        }


        private CommandType _commandType;
        public CommandType CommandType
        {
            get { return _commandType; }
            set
            {
                if (_commandType != value)
                {
                    _commandType = value;
                    OnPropertyChanged(nameof(_commandType));
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

        public WorkTreeDialogViewModel() { }
        public WorkTreeDialogViewModel(string gitRepositoryPath, CommandType commandType, string defaultPath = "")
        {
            //init fields
            _commandType = commandType;
            _defaultPath = defaultPath;
            Branches_Worktrees = new ObservableCollection<string>();

            //set title
            if (_commandType == CommandType.Add)
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
            FolderPath = _defaultPath == "" ? _activeRepositoryPath : _defaultPath;
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
            var status = $"Loading {(_commandType == CommandType.Add ? "Branches" : "Worktrees")}...";
            try
            {
                outputWindow.UpdateStatusBar(status);
                if (_commandType == CommandType.Add)
                    branches_Worktrees = await GitHelper.GetBranchesAsync(ActiveRepositoryPath);
                else if (_commandType == CommandType.Manage)
                    branches_Worktrees = await GitHelper.GetWorkTreePathsAsync(ActiveRepositoryPath);

                await PopulateBranches_Worktrees(branches_Worktrees);
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
                string cleanedBranchName = SelectedBranch_Worktree.ToFolderFormat();//Regex.Match(branchName, @"(?:.*\/)?(?:head -> |origin\/|remote\/)?\+?\s*([^'/]+)").Groups[1].Value;
                string pathPrefix = string.IsNullOrEmpty(_defaultPath) ? _activeRepositoryPath : _defaultPath;
                FolderPath = $@"{pathPrefix}\{cleanedBranchName}";
            }
        }

        public ICommand CreateCommand => new RelayCommand(async obj => await CreateRemoveWorkTree());
        public ICommand OpenCommand => new RelayCommand(async obj => await OpenWorkTree());
        public ICommand CancelCommand => new RelayCommand(obj => CloseDialog());
        public ICommand PruneCommand => new RelayCommand(async obj => await Prune());

        public bool IsDataValid { get; private set; }

        private async Task Prune()
        {
            if (await GitHelper.PruneAsync(ActiveRepositoryPath))
                await LoadBranches_WorktreesAsync();
        }
        private async Task CreateRemoveWorkTree()
        {
            if (!IsValid) return;
            if (CommandType == CommandType.Add)
            {
                CloseDialog();
                await GitHelper.CreateWorkTreeAsync(ActiveRepositoryPath, SelectedBranch_Worktree, FolderPath, IsForceCreateRemove);
            }
            else if (CommandType == CommandType.Manage)
            {
                if (SelectedBranch_Worktree != null)
                {
                    if (await GitHelper.RemoveWorkTreeAsync(ActiveRepositoryPath, SelectedBranch_Worktree, IsForceCreateRemove))
                        await LoadBranches_WorktreesAsync();
                }
            }
        }

        private async Task OpenWorkTree()
        {
            if (!IsValid) return;
            if (!IfOpenInNewVisualStudio) CloseDialog();
            await SolutionHelper.OpenSolutionAsync(SelectedBranch_Worktree, !IfOpenInNewVisualStudio);
        }

        public void CloseDialog()
        {
            // Close the UI
            // (this is assuming your ViewModel is associated with a Window)
            var window = System.Windows.Application.Current.Windows.OfType<System.Windows.Window>().FirstOrDefault(w => w.DataContext == this);

            if (window != null)
            {
                window.Close();
            }
        }
        // Implement INotifyPropertyChanged
        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }



}
