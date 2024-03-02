using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell.Interop;
using System.IO;
using System.Threading.Tasks;

namespace GitWorkTree.Helpers
{
    public static class SolutionHelper
    {
        static LoggingHelper outputWindow = LoggingHelper.Instance;
        public static string GetRepositoryPath(string solutionPath)
        {
            if (File.Exists(solutionPath))
            {
                solutionPath = Path.GetDirectoryName(solutionPath);
            }
            var gitFolderPath = ThreadHelper.JoinableTaskFactory.Run(() => GitHelper.GetGitFolderDirectoryAsync(solutionPath));

            string gitFileName = Path.GetFileName(gitFolderPath);
            if (gitFileName != null && gitFileName.Equals(".git")) // It's the main repository
                return solutionPath;
            else if (gitFolderPath != null && gitFolderPath.Contains(".git/worktrees")) // It's a worktree, get the main repository path - three step outside
                return Path.GetFullPath(Path.Combine(gitFolderPath, "..", "..", ".."));
            return null;
        }
        public static async Task<bool> OpenSolutionAsync(string newSolutionPath, bool openInCurrentInstance)
        {
            bool isError = true;
            try
            {
                outputWindow?.WriteToOutputWindowAsync($"Loading Solution at {newSolutionPath}", true);
                outputWindow?.UpdateStatusBar($"Opening {newSolutionPath}...");

                if (string.IsNullOrEmpty(newSolutionPath) || !Directory.Exists(newSolutionPath))
                {
                    outputWindow?.WriteToOutputWindowAsync($"Not a valid folder path {newSolutionPath}");
                    return false;
                }

                string[] solutionFiles = Directory.GetFiles(newSolutionPath, "*.sln");

                if (solutionFiles.Length == 0)
                {
                    outputWindow?.WriteToOutputWindowAsync($"No solution file found, opening the folder {newSolutionPath}");
                    solutionFiles = [newSolutionPath];
                }
                isError = await HandleOpenSolution(newSolutionPath, openInCurrentInstance, solutionFiles);

                outputWindow?.WriteToOutputWindowAsync($"The worktree solution in path {newSolutionPath} is loaded");
                return isError = false;
            }
            catch (Exception ex)
            {
                outputWindow?.WriteToOutputWindowAsync(ex.Message, true);
                return !(isError = true);
            }
            finally
            {
                if (isError) outputWindow.UpdateStatusBar($"Load failed: {newSolutionPath}");
                else outputWindow?.UpdateStatusBar($"{newSolutionPath} Loaded");
            }
        }


        private static async Task<bool> HandleOpenSolution(string newSolutionPath, bool openInCurrentInstance, string[] solutionFiles)
        {
            bool result = false;
            await ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
            {
                if (openInCurrentInstance)
                {
                    result = await OpenSolutionInCurrentInstance(newSolutionPath, solutionFiles);
                }
                else
                {
                    // Start a new process asynchronously
                    System.Diagnostics.Process.Start("devenv.exe", solutionFiles[0]);
                }
            });
            return result;
        }

        private static async Task<bool> OpenSolutionInCurrentInstance(string newSolutionPath, string[] solutionFiles)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            var solutionService = (IVsSolution)Package.GetGlobalService(typeof(SVsSolution));

            if (solutionService == null)
            {
                outputWindow?.WriteToOutputWindowAsync("Unable to obtain IVsSolution service.");
                return false;
            }

            // Close current solution asynchronously
            if (solutionService.CloseSolutionElement((uint)__VSSLNSAVEOPTIONS.SLNSAVEOPT_PromptSave, null, 0) != 0)
            {
                outputWindow?.WriteToOutputWindowAsync("Failed to close the current solution");
                return false;
            }
            // Open the first solution file found 
            if (solutionService.OpenSolutionFile(0, solutionFiles[0]) != 0)
            {
                outputWindow?.WriteToOutputWindowAsync($"Failed to open {newSolutionPath}");
                return false;
            }

            return true;
        }
    }
}
