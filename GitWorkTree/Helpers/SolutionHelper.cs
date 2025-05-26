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
            try
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
            catch (Exception ex)
            {
                outputWindow.WriteToOutputWindowAsync($"Failed to get repository path: {ex.Message}");
                return null;
            }
        }
        public static async Task<bool> OpenSolution(string newSolutionPath, bool openInCurrentInstance)
        {
            bool isError = true;
            try
            {
                outputWindow.SetCommandStatusBusy(true);
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

                return !(isError = false);
            }
            catch (Exception ex)
            {
                outputWindow?.WriteToOutputWindowAsync(ex.Message, true);
                return !(isError = true);
            }
            finally
            {
                outputWindow?.WriteToOutputWindowAsync($"{newSolutionPath} Loaded");
                outputWindow.SetCommandCompletionStatus(!isError);
            }
        }


        private static async Task<bool> HandleOpenSolution(string newSolutionPath, bool openInCurrentInstance, string[] solutionFiles)
        {
            if (openInCurrentInstance) return await OpenSolutionInCurrentInstance(newSolutionPath, solutionFiles).ConfigureAwait(false);
            return OpenSolutionInNewInstance(newSolutionPath, solutionFiles);
        }

        private static bool OpenSolutionInNewInstance(string newSolutionPath, string[] solutionFiles)
        {
            outputWindow?.WriteToOutputWindowAsync($"Opening {newSolutionPath} in new VS instance", true);
            System.Diagnostics.Process.Start("devenv.exe", solutionFiles[0]);
            return true;
        }

        private static async Task<bool> OpenSolutionInCurrentInstance(string newSolutionPath, string[] solutionFiles)
        {
            outputWindow?.WriteToOutputWindowAsync($"Opening {newSolutionPath} in current VS instance", true);

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
