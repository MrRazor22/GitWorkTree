using Microsoft.VisualStudio.Shell.Interop;
using System.IO;
using System.Threading.Tasks;

namespace GitWorkTree.Helpers
{
    public static class SolutionHelper
    {
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
            LoggingHelper outputWindow = LoggingHelper.Instance;
            try
            {
                outputWindow?.WriteToOutputWindowAsync($"Opening Worktree Solution at {newSolutionPath}", true);

                if (string.IsNullOrEmpty(newSolutionPath) || !Directory.Exists(newSolutionPath))
                {
                    outputWindow?.WriteToOutputWindowAsync($"Not a valid folder path {newSolutionPath}");
                    return false;
                }

                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                // Get IVsSolution service
                var solutionService = (IVsSolution)Package.GetGlobalService(typeof(SVsSolution));

                if (solutionService == null)
                {
                    outputWindow?.WriteToOutputWindowAsync("Unable to obtain IVsSolution service.");
                    return false;
                }

                string[] solutionFiles = Directory.GetFiles(newSolutionPath, "*.sln");

                // Check if any solution files exist
                if (solutionFiles.Length == 0)
                {
                    outputWindow?.WriteToOutputWindowAsync($"No solution file found, opening the folder {newSolutionPath}");
                    solutionFiles = [newSolutionPath];
                }

                if (openInCurrentInstance)
                {
                    //dialogViewModel.CloseDialog();
                    // Close current solution
                    solutionService.CloseSolutionElement((uint)__VSSLNSAVEOPTIONS.SLNSAVEOPT_PromptSave, null, 0);
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
                outputWindow?.WriteToOutputWindowAsync(ex.Message, true);
                return false;
            }
        }
    }
}
