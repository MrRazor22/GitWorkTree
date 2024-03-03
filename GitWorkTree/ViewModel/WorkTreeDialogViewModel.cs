using GitWorkTree.Commands;
using GitWorkTree.Helpers;
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
                    errorStatus = "No repository loaded";
            }
            if (propertyName == nameof(SelectedBranch_Worktree))
            {
                if (string.IsNullOrEmpty(SelectedBranch_Worktree))
                    errorStatus = "No valid branch/Worktree selected";
            }
            else if (commandType != CommandType.Manage && propertyName == nameof(FolderPath))
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
        }

        private List<string> GetBranches_Worktrees()
        {
            outputWindow.SetCommandStatusBusy();
            return ThreadHelper.JoinableTaskFactory.Run(async () =>
            {
                if (commandType == CommandType.Create)
                    return await GitHelper.GetBranchesAsync(_activeRepositoryPath).ConfigureAwait(false);
                else if (commandType == CommandType.Manage)
                    return await GitHelper.GetWorkTreePathsAsync(_activeRepositoryPath).ConfigureAwait(false);
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
                    outputWindow?.UpdateStatusBar("No Branches/Worktrees available for this repository");
                    return false;
                }
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                Branches_Worktrees.Clear();
                foreach (var item in branches_Worktrees)
                {
                    Branches_Worktrees.Add(item);
                }
                SelectedBranch_Worktree = Branches_Worktrees.FirstOrDefault();
                outputWindow.SetCommandStatusBusy(false);
                return true;
            }
            catch (Exception ex)
            {
                await outputWindow?.WriteToOutputWindowAsync($"Failed to Load Branches/Worktrees: {ex.Message}");
                return false;
            }
        }

        private async Task<bool> UpdateFolderPath()
        {
            try
            {
                if (!(commandType == CommandType.Create) || _selectedBranch_Worktree == null) return false;
                string worktreePath = Path.Combine(Directory.GetParent(_activeRepositoryPath).FullName, $"{_activeRepositoryPath}_Worktrees");
                string pathPrefix = optionsSaved.DefaultBranchPath == "" ? $"{worktreePath}" : optionsSaved.DefaultBranchPath;
                string cleanedBranchName = _selectedBranch_Worktree.ToFolderFormat();

                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                FolderPath = Path.Combine(pathPrefix, cleanedBranchName);
                return true;
            }
            catch (Exception ex)
            {
                await outputWindow?.WriteToOutputWindowAsync($"Failed to set Path: {ex.Message}");
                return false;
            }
        }

        public ICommand PruneCommand => new RelayCommand(async obj => await Prune_WorkTree());
        public ICommand CreateCommand => new RelayCommand(async (obj) =>
            {
                if (CommandType == CommandType.Create) return await Create_WorkTree();
                return await Remove_WorkTree();
            });
        public ICommand OpenCommand => new RelayCommand(async obj => await Open_WorkTree());
        public ICommand CancelCommand => new RelayCommand(obj => Close_Dialog());

        private async Task<bool> Prune_WorkTree()
        {
            return await GitHelper.PruneAsync(_activeRepositoryPath).ConfigureAwait(false) &&
                   await PopulateBranches_Worktrees().ConfigureAwait(false);
        }

        private async Task<bool> Create_WorkTree()
        {
            if (!IsValid) return false;

            Close_Dialog();
            if (await GitHelper.CreateWorkTreeAsync(_activeRepositoryPath, _selectedBranch_Worktree, _folderPath, _isForceCreateRemove).ConfigureAwait(false))
            {
                if (optionsSaved.IsLoadSolution) await PopulateBranches_Worktrees().ConfigureAwait(false);
                return true;
            }
            return false;
        }
        private async Task<bool> Remove_WorkTree()
        {
            if (!IsValid) return false;

            if (await GitHelper.RemoveWorkTreeAsync(_activeRepositoryPath, _selectedBranch_Worktree, _isForceCreateRemove).ConfigureAwait(false))
            {
                await PopulateBranches_Worktrees().ConfigureAwait(false);
                return true;
            }
            return false;
        }

        private async Task<bool> Open_WorkTree()
        {
            if (!IsValid) return false;
            if (!IfOpenInNewVisualStudio) Close_Dialog();
            return await SolutionHelper.OpenSolution(_selectedBranch_Worktree, !_ifOpenInNewVisualStudio).ConfigureAwait(false);
        }


        public bool Close_Dialog()
        {
            var window = System.Windows.Application.Current.Windows.OfType<System.Windows.Window>().FirstOrDefault(w => w.DataContext == this);
            if (window != null) window.Close();
            return true;
        }


    }
}



