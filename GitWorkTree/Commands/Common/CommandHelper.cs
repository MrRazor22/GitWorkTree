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
    public enum CommandType { Add, Manage };
    internal class CommandHelper
    {
        private AsyncPackage package;
        private CommandType _commandType;

        //private CommandActions _commandActions;
        public string ActiveRepositoryPath { get; private set; }
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

                bool isError = false;
                ActiveRepositoryPath = GitHelper.GetMainRepositoryDirectory((line, type) =>
                {
                    if (type == GitOutputType.Error)
                    {
                        isError = true;
                        outputWindow?.UpdateStatusBar(line);
                    }
                });

                if (string.IsNullOrEmpty(ActiveRepositoryPath) || isError)
                {
                    if (!isError) outputWindow?.UpdateStatusBar("No Repository loaded!");
                    return false;
                }

                isLoadSolution = optionsSaved != null ? optionsSaved.IsLoadSolution : false;
                defaultBranchPath = optionsSaved?.DefaultBranchPath != null ? optionsSaved.DefaultBranchPath : ActiveRepositoryPath;

                return true;
            }
            catch (Exception ex)
            {
                outputWindow.ShowOutputPane = true;
                outputWindow?.WriteToOutputWindowAsync(ex.Message); return false;
            }
        }

        public bool GetDataRequired()
        {
            try
            {
                ThreadHelper.ThrowIfNotOnUIThread();

                dialogViewModel = new WorkTreeDialogViewModel(ActiveRepositoryPath, _commandType, defaultBranchPath, outputWindow);
                WorkTreeDialogWindow dialog = new WorkTreeDialogWindow
                {
                    DataContext = dialogViewModel
                };
                IntPtr hwnd = new IntPtr((long)dte.MainWindow.HWnd);
                WindowInteropHelper helper = new WindowInteropHelper(dialog);
                helper.Owner = hwnd;
                if (_commandType == CommandType.Add) dialog.ShowDialog();
                else
                {
                    dialogViewModel.CommandInvoked += DialogViewModel_CommandInvoked;
                    dialog.Show();
                }

                if (dialogViewModel.IsDataValid == false)
                {
                    return false;
                }
                return true;
            }
            catch (Exception ex)
            {
                outputWindow.ShowOutputPane = true;
                outputWindow?.WriteToOutputWindowAsync(ex.Message); return false;
            }
        }

        private void DialogViewModel_CommandInvoked(object sender, CommandActionsEventArgs e)
        {
            RunGitCommandAsync(e.commandAction);
        }
        public Task<bool> RunCreateWorktreeCommandAsync()
        {
            return RunGitCommandAsync(CommandActions.Create);
        }

        public async Task<bool> RunGitCommandAsync(CommandActions _commandActions)
        {
            var RepoPath = dialogViewModel.ActiveRepositoryPath;
            var BranchName = dialogViewModel.SelectedBranch;
            var WorkTreePath = dialogViewModel.FolderPath;
            var shouldForce = dialogViewModel.IsForceCreateRemove;
            bool isError = false;

            try
            {
                outputWindow.ShowOutputPane = true;
                switch (_commandActions)
                {
                    case CommandActions.Create:
                        isError = await CreateWorktree(RepoPath, BranchName, WorkTreePath, shouldForce, isError);
                        if (!isError)
                            if (optionsSaved.IsLoadSolution)
                                await OpenSolutionAsync(dialogViewModel.FolderPath, true);
                        break;

                    case CommandActions.Remove:
                        isError = await RemoveWorktree(RepoPath, BranchName, shouldForce, isError);
                        break;

                    case CommandActions.Prune:
                        isError = await PruneWorktrees(RepoPath, isError);

                        if (dialogViewModel.SelectedBranch != null)
                            await RemoveWorktree(RepoPath, BranchName, shouldForce, isError);
                        break;

                    case CommandActions.Open:
                        outputWindow?.WriteToOutputWindowAsync($"Opening worktree {BranchName} - Enter");
                        await OpenSolutionAsync(dialogViewModel.SelectedBranch, !dialogViewModel.IfOpenInNewVisualStudio);
                        break;
                }

                var status = isError ? "Failed" : "Executed";
                outputWindow?.WriteToOutputWindowAsync($"Worktree command - {status}");
                return !isError;
            }
            catch (Exception ex)
            {
                outputWindow.ShowOutputPane = true;
                outputWindow?.WriteToOutputWindowAsync(ex.Message);
                return false;
            }
        }

        private async Task<bool> CreateWorktree(string RepoPath, string BranchName, string WorkTreePath, bool shouldForce, bool isError)
        {
            outputWindow?.WriteToOutputWindowAsync($"Create worktree for branch {BranchName} - Enter");
            await Task.Run(() =>
            {
                GitHelper.CreateWorkTree(RepoPath, BranchName, WorkTreePath, shouldForce, (line, type) =>
                {
                    isError = (type == GitOutputType.Error);
                    outputWindow?.WriteToOutputWindowAsync(line, isError);
                });
            });

            return isError;
        }

        private async Task<bool> PruneWorktrees(string RepoPath, bool isError)
        {
            outputWindow?.UpdateStatusBar($"Prune Worktree Executing");
            await Task.Run(() =>
            {
                GitHelper.Prune(RepoPath, (line, type) =>
                {
                    if (type == GitOutputType.Error)
                    {
                        isError = true;
                        outputWindow?.WriteToOutputWindowAsync(line, isError);
                    }
                });
            });
            outputWindow?.UpdateStatusBar($"Prune Worktree Executed");
            return isError;
        }

        private async Task<bool> RemoveWorktree(string RepoPath, string BranchName, bool shouldForce, bool isError)
        {
            if (dialogViewModel.SelectedBranch != null)
            {
                outputWindow?.WriteToOutputWindowAsync($"Remove worktree for branch {BranchName} - Enter");
                await Task.Run(() =>
                {
                    GitHelper.RemoveWorkTree(RepoPath, BranchName, shouldForce, (line, type) =>
                    {
                        isError = (type == GitOutputType.Error);
                        outputWindow?.WriteToOutputWindowAsync(line, isError);
                    });
                });
            }
            if (!isError) await dialogViewModel.LoadBranchesAsync();
            return isError;
        }

        public async Task<bool> OpenSolutionAsync(string newSolutionPath, bool openInCurrentInstance)
        {
            try
            {
                outputWindow?.WriteToOutputWindowAsync($"Load Solution set to True, Opening Worktree Solution");

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
                    solutionFiles = [newSolutionPath];
                }

                if (openInCurrentInstance)
                {
                    // Close current solution
                    solutionService.CloseSolutionElement((uint)__VSSLNSAVEOPTIONS.SLNSAVEOPT_PromptSave, null, 0);

                    // Get all solution files in the folder


                    // Open the first solution file found
                    solutionService.OpenSolutionFile(0, solutionFiles[0]);
                }
                else
                {
                    System.Diagnostics.Process.Start("devenv.exe", solutionFiles[0]);
                }

                outputWindow?.UpdateStatusBar($"The worktree solution in path {newSolutionPath} is loaded");
                return true;
            }
            catch (Exception ex)
            {
                outputWindow.ShowOutputPane = true;
                outputWindow?.WriteToOutputWindowAsync(ex.Message); return false;
            }
        }
    }
}
