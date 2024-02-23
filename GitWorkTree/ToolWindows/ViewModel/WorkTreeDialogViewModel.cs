using GitWorktree.ToolWindows.ViewModel;
using GitWorkTree.Commands;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Input;

namespace GitWorkTree.ToolWindows.ViewModel
{
    public class WorkTreeDialogViewModel : INotifyPropertyChanged, IDataErrorInfo
    {
        private readonly VsOutputWindow _OutputWindow;

        public WorkTreeDialogViewModel() { }

        private string _defaultPath;

        public ObservableCollection<string> Branches { get; set; }

        public string this[string columnName]
        {
            get
            {
                string status = "";
                // Add data validation logic here
                if (columnName == nameof(ActiveRepositoryPath))
                {
                    if (string.IsNullOrEmpty(ActiveRepositoryPath))
                        status = "No repository available";
                }
                if (columnName == nameof(SelectedBranch))
                {
                    if (string.IsNullOrEmpty(SelectedBranch))
                        status = "Please enter a valid branch name";
                }
                else if (columnName == nameof(FolderPath))
                {
                    if (string.IsNullOrEmpty(FolderPath))
                        status = "Please enter a valid path for worktree";
                }

                _OutputWindow.UpdateStatusBar(status);
                return status;
            }
        }

        public string Error => null;

        private bool IsValid
        {
            get
            {
                // Check if all properties are valid
                return string.IsNullOrEmpty(this[nameof(ActiveRepositoryPath)])
                    && string.IsNullOrEmpty(this[nameof(SelectedBranch)])
                    && string.IsNullOrEmpty(this[nameof(FolderPath)]);
                // Add validation for other properties as needed
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
                _ = LoadBranchesAsync();
            }
        }

        private string _selectedBranch;
        public string SelectedBranch
        {
            get { return _selectedBranch; }
            set
            {
                _selectedBranch = value;
                OnPropertyChanged(nameof(SelectedBranch));
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

        private bool _isForceCreateRemove;
        public bool IsForceCreateRemove
        {
            get { return _isForceCreateRemove; }
            set
            {
                _isForceCreateRemove = value;
                OnPropertyChanged(nameof(IsForceCreateRemove));
                _OutputWindow.UpdateStatusBar("--Force attributed updated");
            }
        }

        private bool _isPrune;
        public bool IsPrune
        {
            get { return _isPrune; }
            set
            {
                _isPrune = value;
                OnPropertyChanged(nameof(IsPrune));
            }
        }


        public WorkTreeDialogViewModel(string gitRepositoryPath, CommandType commandType, string defaultPath = "", VsOutputWindow OutputWindow = null)
        {
            _commandType = commandType;
            _defaultPath = defaultPath;
            _OutputWindow = OutputWindow;

            Branches = new ObservableCollection<string>();

            WindowTitle = (_commandType == CommandType.Add) ? "Create New Worktree" : "Remove Existing Worktrees";
            ActiveRepositoryPath = gitRepositoryPath;
            FolderPath = _defaultPath == "" ? _activeRepositoryPath : _defaultPath;
        }

        private async Task LoadBranchesAsync()
        {
            if (string.IsNullOrEmpty(ActiveRepositoryPath))
            {
                _OutputWindow.UpdateStatusBar("Select a Repository");
                return;
            }

            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            try
            {
                _OutputWindow.UpdateStatusBar("Breanches are Loading...");
                if (_commandType == CommandType.Add)
                {
                    var branches = await Task.Run(() => GitHelper.GetBranches(ActiveRepositoryPath));

                    // Use ThreadHelper.JoinableTaskFactory.RunAsync to update UI on the UI thread
                    await ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
                    {
                        Branches.Clear();
                        foreach (var branch in branches)
                        {
                            Branches.Add(branch);
                        }
                    });

                    _OutputWindow?.UpdateStatusBar("Branches Loaded successfully");
                }
                else if (_commandType == CommandType.Remove)
                {
                    var workTreePaths = await Task.Run(() => GitHelper.GetWorkTreePaths(ActiveRepositoryPath));

                    // Use ThreadHelper.JoinableTaskFactory.RunAsync to update UI on the UI thread
                    await ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
                    {
                        Branches.Clear();
                        foreach (var path in workTreePaths)
                        {
                            Branches.Add(path);
                        }
                    });

                    _OutputWindow?.UpdateStatusBar("WorkTree Paths Loaded successfully");
                }

                UpdateFolderPath();
            }
            catch (Exception ex)
            {
                _OutputWindow?.UpdateStatusBar($"{ex.Message}");
            }

        }

        private void UpdateFolderPath()
        {
            if (!string.IsNullOrEmpty(SelectedBranch))
            {
                string branchName = SelectedBranch;

                //string cleanedBranchName = Regex.Match(branchName, @"^(?:.*?\/)?(?:head -> |origin\/|remote\/)?\+?([^\s]+)$").Groups[1].Value;
                //string cleanedBranchName = Regex.Match(branchName, @"(?:.*\/)?(?:head -> |origin\/|remote\/)?\+?\s*([^']+)").Groups[1].Value;
                string cleanedBranchName = Regex.Match(branchName, @"(?:.*\/)?(?:head -> |origin\/|remote\/)?\+?\s*([^'/]+)").Groups[1].Value;


                string pathPrefix = string.IsNullOrEmpty(_defaultPath) ? _activeRepositoryPath : _defaultPath;
                FolderPath = $@"{pathPrefix}\{cleanedBranchName}";
            }
        }

        public ICommand CreateCommand => new RelayCommand(obj => CreateRemoveWorkTree());
        public ICommand CancelCommand => new RelayCommand(obj => Cancel());
        public ICommand PruneCommand => new RelayCommand(obj => Prune());

        public bool IsDataValid { get; private set; }

        private void Prune()
        {
            IsDataValid = true;
            IsPrune = true;
            CloseDialog();
        }
        private void CreateRemoveWorkTree()
        {
            if (!IsValid)
            {
                return;
            }
            IsDataValid = true;
            CloseDialog();
        }

        private void Cancel()
        {
            IsDataValid = false;
            CloseDialog();
        }

        private void CloseDialog()
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
