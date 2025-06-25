using EnvDTE;
using Microsoft.VisualStudio.Threading;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Path = System.IO.Path;

namespace GitWorkTree.Helpers
{
    public class GitCommandArgs
    {
        public string Argument { get; set; }
        public string WorkingDirectory { get; set; }
    }

    public static class GitHelper
    {
        private static string GitPath = Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory,
            @"CommonExtensions\Microsoft\TeamFoundation\Team Explorer\Git\cmd\git.exe");
        public static string ToFolderFormat(this string branchName) => Regex.Match(branchName, @"(?:.*\/)?(?:head -> |origin\/|remote\/)?\+?\s*([^'/]+)").Groups[1].Value ?? branchName;
        public static string ToGitCommandExecutableFormat(this string branchName) => Regex.Match(branchName,
                @"(?:\+?\s?(?:remotes?\/(?:origin|main|upstream)\/(?:HEAD -> (?:origin|main|upstream)\/)?|remotes?\/(?:origin|main|upstream)\/)?|[^\/]+\/)?([^\/]+(?:\/[^\/]+)*)$")
                .Groups[1].Value ?? branchName;

        private static async Task<bool> ExecuteAsync(GitCommandArgs gitCommandArgs, Action<string> outputHandler = null)
        {
            LoggingHelper outputWindow = LoggingHelper.Instance;

            if (gitCommandArgs == null || string.IsNullOrEmpty(gitCommandArgs.WorkingDirectory))
            {
                outputWindow?.WriteToOutputWindowAsync("The working directory is invalid or not loaded yet");
                return false;
            }

            if (!File.Exists(GitPath))
            {
                outputWindow?.WriteToOutputWindowAsync($"Git executable not found at: {GitPath}", true);
                return false;
            }

            outputWindow?.WriteToOutputWindowAsync($"Executing Git command: {gitCommandArgs.Argument}", true);

            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = GitPath,
                    Arguments = gitCommandArgs.Argument,
                    WorkingDirectory = gitCommandArgs.WorkingDirectory,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true
                };

                using var process = new System.Diagnostics.Process { StartInfo = startInfo };
                var outputBuilder = new StringBuilder();
                var errorBuilder = new StringBuilder();

                process.OutputDataReceived += (s, e) =>
                {
                    if (e.Data != null)
                    {
                        outputBuilder.AppendLine(e.Data);
                        outputHandler?.Invoke(e.Data);
                    }
                };

                process.ErrorDataReceived += (s, e) =>
                {
                    if (e.Data != null)
                    {
                        errorBuilder.AppendLine(e.Data);
                        outputWindow?.WriteToOutputWindowAsync(e.Data);
                    }
                };

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                await process.WaitForExitAsync();

                bool isError = process.ExitCode != 0;

                if (isError && errorBuilder.Length == 0)
                    outputWindow?.WriteToOutputWindowAsync("Git failed but no error output captured.", true);

                var result = isError ? "failed" : "completed";
                outputWindow.WriteToOutputWindowAsync($"Command execution - {result}");

                return !isError;
            }
            catch (Exception ex)
            {
                outputWindow?.WriteToOutputWindowAsync($"An error occurred during Git command execution: {ex.Message}", true);
                return false;
            }
        }



        public static async Task<List<string>> GetWorkTreePathsAsync(string repositoryPath)
        {
            List<string> workTreePaths = new List<string>();
            var isCompleted = await ExecuteAsync(new GitCommandArgs()
            {
                WorkingDirectory = repositoryPath,
                Argument = "worktree list --porcelain",
            }, (line) =>
            {
                if (line.StartsWith("worktree"))
                {
                    string worktreePath = line.Split(' ').ElementAtOrDefault(1);
                    string mainRepoPath = Path.GetFullPath(repositoryPath);

                    if (!Path.GetFullPath(worktreePath).Equals(mainRepoPath, StringComparison.OrdinalIgnoreCase))
                    {
                        workTreePaths.Add(worktreePath);
                    }
                }
            });
            if (isCompleted) return workTreePaths;
            else return null;
        }

        public static async Task<List<string>> GetBranchesAsync(string repositoryPath)
        {
            List<string> branches = new List<string>();
            var isCompleted = await ExecuteAsync(new GitCommandArgs()
            {
                WorkingDirectory = repositoryPath,
                Argument = "--no-pager branch -a --no-color"
            }, (line) =>
            {
                if (!string.IsNullOrWhiteSpace(line))
                {
                    string branchName = line.Trim().TrimStart('*').Trim();
                    branches.Add(branchName);
                }
            });
            if (isCompleted) return branches;
            else return null;
        }

        public static async Task<bool> CreateWorkTreeAsync
            (string repositoryPath, string branchName, string workTreePath, bool shouldForceCreate)
        {
            string force = shouldForceCreate ? "-f " : "";
            return await ExecuteAsync(new GitCommandArgs()
            {
                Argument = $"worktree add {force}{workTreePath} {branchName.ToGitCommandExecutableFormat()}",
                WorkingDirectory = repositoryPath
            }, (line) =>
            {
                LoggingHelper.Instance?.WriteToOutputWindowAsync(line);
            });
        }

        public static async Task<bool> RemoveWorkTreeAsync(string repositoryPath, string workTreePath, bool shouldForceCreate)
        {
            string force = shouldForceCreate ? "-f " : "";
            return await ExecuteAsync(new GitCommandArgs()
            {
                Argument = $"worktree remove {force}{workTreePath}",
                WorkingDirectory = repositoryPath
            }, (line) =>
            {
                LoggingHelper.Instance?.WriteToOutputWindowAsync(line);
            });
        }

        public static async Task<bool> PruneAsync(string repositoryPath)
        {
            return await ExecuteAsync(new GitCommandArgs()
            {
                Argument = "worktree prune --expire=now",
                WorkingDirectory = repositoryPath
            }, (line) =>
            {
                LoggingHelper.Instance?.WriteToOutputWindowAsync(line);
            });
        }

        public static async Task<string> GetGitFolderDirectoryAsync(string currentSolutionPath)
        {
            string commandoutput = "";
            var isCompleted = await ExecuteAsync(new GitCommandArgs() { WorkingDirectory = currentSolutionPath, Argument = "rev-parse --git-dir", },
                (line) =>
                {
                    if (!string.IsNullOrWhiteSpace(line))
                    {
                        commandoutput = line.Trim();
                    }
                });

            if (isCompleted) return commandoutput;
            return null;
        }
    }
}
