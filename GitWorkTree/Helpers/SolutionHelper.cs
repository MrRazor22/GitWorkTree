using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace GitWorkTree.Helpers
{
    public class SolutionHelper : ISolutionService
    {
        private readonly ILoggingService _loggingService;
        private readonly IGitService _gitService;

        public SolutionHelper(ILoggingService loggingService, IGitService gitService)
        {
            _loggingService = loggingService;
            _gitService = gitService;
        }

        public static string NormalizePath(string path) =>
             string.IsNullOrWhiteSpace(path) ? "\"\"" :
                 ((path = path.Trim().Trim('"').TrimEnd('\r', '\n'))
                  .Any(char.IsWhiteSpace) ? $"\"{path}\"" : path);

        public string GetRepositoryPath(string solutionPath)
        {
            try
            {
                if (File.Exists(solutionPath))
                {
                    solutionPath = Path.GetDirectoryName(solutionPath);
                }
                var gitFolderPath = ThreadHelper.JoinableTaskFactory.Run(() => _gitService.GetGitFolderDirectoryAsync(solutionPath));

                string gitFileName = Path.GetFileName(gitFolderPath);
                if (gitFileName != null && gitFileName.Equals(".git")) // It's the main repository
                    return solutionPath;
                else if (gitFolderPath != null && gitFolderPath.Contains(".git/worktrees")) // It's a worktree, get the main repository path - three step outside
                    return Path.GetFullPath(Path.Combine(gitFolderPath, "..", "..", ".."));
                return null;
            }
            catch (Exception ex)
            {
                _loggingService?.WriteToOutputWindowAsync($"Failed to get repository path: {ex.Message}");
                return null;
            }
        }

        public async Task<bool> OpenSolution(string newSolutionPath, bool openInCurrentInstance)
        {
            bool isError = true;
            try
            {
                _loggingService?.SetCommandStatusBusy(true);
                if (string.IsNullOrEmpty(newSolutionPath) || !Directory.Exists(newSolutionPath))
                {
                    _loggingService?.WriteToOutputWindowAsync($"Not a valid folder path {newSolutionPath}");
                    return false;
                }

                string[] solutionFiles = Directory.GetFiles(newSolutionPath, "*.sln");

                if (solutionFiles.Length == 0)
                {
                    _loggingService?.WriteToOutputWindowAsync($"No solution file found, opening the folder {newSolutionPath}");
                    solutionFiles = [newSolutionPath];
                }
                isError = await HandleOpenSolution(newSolutionPath, openInCurrentInstance, solutionFiles);

                return !isError;
            }
            catch (Exception ex)
            {
                _loggingService?.WriteToOutputWindowAsync(ex.Message, true);
                return false;
            }
            finally
            {
                _loggingService?.WriteToOutputWindowAsync($"{newSolutionPath} Loaded");
                _loggingService?.SetCommandCompletionStatus(!isError);
            }
        }

        private async Task<bool> HandleOpenSolution(string newSolutionPath, bool openInCurrentInstance, string[] solutionFiles)
        {
            if (openInCurrentInstance) return await OpenSolutionInCurrentInstance(newSolutionPath, solutionFiles).ConfigureAwait(false);
            return OpenSolutionInNewInstance(newSolutionPath, solutionFiles);
        }

        private bool OpenSolutionInNewInstance(string newSolutionPath, string[] solutionFiles)
        {
            _loggingService?.WriteToOutputWindowAsync($"Opening {newSolutionPath} in new VS instance", true);
            System.Diagnostics.Process.Start("devenv.exe", NormalizePath(solutionFiles[0]));
            return true;
        }

        private async Task<bool> OpenSolutionInCurrentInstance(string newSolutionPath, string[] solutionFiles)
        {
            _loggingService?.WriteToOutputWindowAsync($"Opening {newSolutionPath} in current VS instance", true);

            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            var solutionService = (IVsSolution)Package.GetGlobalService(typeof(SVsSolution));

            if (solutionService == null)
            {
                _loggingService?.WriteToOutputWindowAsync("Unable to obtain IVsSolution service.");
                return false;
            }

            // Close current solution asynchronously
            if (solutionService.CloseSolutionElement((uint)__VSSLNSAVEOPTIONS.SLNSAVEOPT_PromptSave, null, 0) != 0)
            {
                _loggingService?.WriteToOutputWindowAsync("Failed to close the current solution");
                return false;
            }
            // Open the first solution file found 
            if (solutionService.OpenSolutionFile(0, solutionFiles[0]) != 0)
            {
                _loggingService?.WriteToOutputWindowAsync($"Failed to open {newSolutionPath}");
                return false;
            }

            return true;
        }
    }
}
