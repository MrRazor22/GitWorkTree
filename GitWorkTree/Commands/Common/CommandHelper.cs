using EnvDTE;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.TeamFoundation.Git.Extensibility;
using Microsoft;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using EnvDTE80;
using System.IO;
using GitWorkTree.ToolWindows.ViewModel;
using GitWorkTree.ToolWindows.View;
using System.Windows.Interop;

namespace GitWorkTree.Commands
{
    public enum CommandType { Add, Remove };
    internal class CommandHelper
    {
        private AsyncPackage package;
        private CommandType _commandType;

        public List<string> ActiveRepositories { get; private set; }
        public VsOutputWindow outputWindow { get; private set; }

        public static Action<General> OnSettingsSaved = general => optionsSaved = general;
        private bool isLoadSolution;
        private string defaultBranchPath;
        private IServiceProvider serviceProvider;
        private DTE2 dte;
        private WorkTreeDialogViewModel dialogViewModel;
        public static General optionsSaved { get; set; }

        public CommandHelper(AsyncPackage asyncPackage, CommandType commandType)
        {
            package = asyncPackage;
            _commandType = commandType;
        }

        public bool PreRequisite()
        {
            try
            {
                outputWindow = VsOutputWindow.Instance;
                //Get services nneded
                serviceProvider = package as IServiceProvider;
                Assumes.Present(serviceProvider);
                dte = serviceProvider.GetService(typeof(DTE)) as DTE2;
                Assumes.Present(dte);
                var gitExt = serviceProvider.GetService(typeof(IGitExt3)) as IGitExt3;
                Assumes.Present(gitExt);

                ActiveRepositories = gitExt.ActiveRepositories.Select(i => i.RepositoryPath).ToList();
                if (ActiveRepositories == null || ActiveRepositories.Count == 0)
                {
                    outputWindow?.UpdateStatusBar("No Repository loaded!");
                    return false;
                }

                isLoadSolution = optionsSaved != null ? optionsSaved.IsLoadSolution : false;
                defaultBranchPath = optionsSaved?.DefaultBranchPath != null ? optionsSaved.DefaultBranchPath : ActiveRepositories.FirstOrDefault();

                return true;
            }
            catch (Exception ex) { outputWindow?.WriteToOutputWindowAsync(ex.Message); return false; }
        }

        public bool GetDataRequired()
        {
            try
            {
                ThreadHelper.ThrowIfNotOnUIThread();

                dialogViewModel = new WorkTreeDialogViewModel(ActiveRepositories, _commandType, defaultBranchPath, outputWindow);
                WorkTreeDialogWindow dialog = new WorkTreeDialogWindow
                {
                    DataContext = dialogViewModel
                };
                IntPtr hwnd = new IntPtr((long)dte.MainWindow.HWnd);
                WindowInteropHelper helper = new WindowInteropHelper(dialog);
                helper.Owner = hwnd;
                dialog.ShowDialog();

                if (dialogViewModel.IsDataValid == false)
                {
                    return false;
                }

                return true;
            }
            catch (Exception ex) { outputWindow?.WriteToOutputWindowAsync(ex.Message); return false; }
        }

        public async Task<bool> RunGitCommandAsync()
        {
            var RepoPath = dialogViewModel.SelectedRepository;
            var BranchName = dialogViewModel.SelectedBranch;
            var WorkTreePath = dialogViewModel.FolderPath;
            var shouldForce = dialogViewModel.IsForceCreateRemove;
            bool isError = false;

            try
            {
                outputWindow.ShowOutputPane = true;
                if (_commandType == CommandType.Add)
                {
                    outputWindow?.WriteToOutputWindowAsync($"Create worktree for branch {BranchName} - Enter");
                    await Task.Run(() =>
                    {
                        GitBuddy.CreateWorkTree(RepoPath, BranchName, WorkTreePath, shouldForce, (line, type) =>
                        {
                            isError = (type == GitOutputType.Error);
                            outputWindow?.WriteToOutputWindowAsync(line, isError);
                        });
                    });

                }
                else if (_commandType == CommandType.Remove)
                {
                    if (dialogViewModel.IsPrune)
                    {
                        outputWindow?.UpdateStatusBar($"Prune Worktree Executing");

                        await Task.Run(() =>
                        {
                            GitBuddy.Prune(RepoPath, (line, type) =>
                            {
                                isError = (type == GitOutputType.Error);
                                outputWindow?.WriteToOutputWindowAsync(line, isError);
                            });
                        });
                        outputWindow?.UpdateStatusBar($"Prune Worktree Executed");
                    }

                    if (dialogViewModel.SelectedBranch != null)
                    {
                        outputWindow?.WriteToOutputWindowAsync($"Remove worktree for branch {BranchName} - Enter");

                        await Task.Run(() =>
                        {
                            GitBuddy.RemoveWorkTree(RepoPath, BranchName, shouldForce, (line, type) =>
                            {
                                isError = (type == GitOutputType.Error);
                                outputWindow?.WriteToOutputWindowAsync(line, isError);
                            });
                        });
                    }

                }
                var status = isError ? "Failed" : "Completed";
                outputWindow?.WriteToOutputWindowAsync($"Worktree command - {status}");
                return !isError;
            }
            catch (Exception ex) { outputWindow?.WriteToOutputWindowAsync(ex.Message); return false; }

        }

        public async Task<bool> CloseAndOpenSolutionAsync()
        {
            try
            {
                outputWindow?.WriteToOutputWindowAsync($"Loade Solution set to True, Opening Worktree Solution");

                var newSolutionPath = dialogViewModel?.FolderPath;
                if (string.IsNullOrEmpty(newSolutionPath) || !Directory.Exists(newSolutionPath))
                {
                    outputWindow?.WriteToOutputWindowAsync("Please provide a valid folder path.");
                    return false;
                }

                await package.JoinableTaskFactory.SwitchToMainThreadAsync();

                // Get IVsSolution service
                var solutionService = (IVsSolution)await package.GetServiceAsync(typeof(SVsSolution));

                if (solutionService == null)
                {
                    outputWindow?.WriteToOutputWindowAsync("Unable to obtain IVsSolution service.");
                    return false;
                }

                string[] solutionFiles = Directory.GetFiles(newSolutionPath, "*.sln");

                // Check if any solution files exist
                if (solutionFiles.Length == 0)
                {
                    outputWindow?.WriteToOutputWindowAsync($"No solution file found in the folder: {newSolutionPath}");
                    return false;
                }

                // Close current solution
                solutionService.CloseSolutionElement((uint)__VSSLNSAVEOPTIONS.SLNSAVEOPT_PromptSave, null, 0);

                // Get all solution files in the folder


                // Open the first solution file found
                solutionService.OpenSolutionFile(0, solutionFiles[0]);

                outputWindow?.UpdateStatusBar($"The worktree solution in path {newSolutionPath} is loaded");
                return true;
            }
            catch (Exception ex)
            {
                outputWindow?.WriteToOutputWindowAsync(ex.Message);
                return false;
            }
        }
    }
}
