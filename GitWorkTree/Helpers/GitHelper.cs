using EnvDTE;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
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

        //private static void Execute(GitCommandArgs gitCommandArgs, Action<string, GitOutputType> outputHandler = null)
        //{
        //    if (gitCommandArgs == null || string.IsNullOrEmpty(gitCommandArgs.WorkingDirectory))
        //    {
        //        throw new ArgumentException("GitCommandArgs must be provided with a valid working directory.");
        //    }

        //    if (!File.Exists(GitPath))
        //    {
        //        throw new FileNotFoundException($"Git executable not found at: {GitPath}");
        //    }

        //    try
        //    {

        //        var startInfo = new ProcessStartInfo
        //        {
        //            FileName = GitPath,
        //            Arguments = gitCommandArgs.Argument,
        //            WorkingDirectory = gitCommandArgs.WorkingDirectory,
        //            UseShellExecute = false,
        //            CreateNoWindow = true,
        //            RedirectStandardError = true,
        //            RedirectStandardOutput = true
        //        };

        //        using var process = new System.Diagnostics.Process { StartInfo = startInfo };
        //        process.OutputDataReceived += (sender, e) =>
        //        {
        //            if (e.Data != null)
        //            {
        //                //outputHandler?.Invoke(e.Data, GitOutputType.Standard);
        //                outputHandler?.Invoke(e.Data, e.Data.IndexOf("fatal",
        //                    StringComparison.OrdinalIgnoreCase) >= 0 || e.Data.IndexOf("error", StringComparison.OrdinalIgnoreCase) >= 0 ?
        //                    GitOutputType.Error : GitOutputType.Standard);
        //            }
        //        };

        //        process.ErrorDataReceived += (sender, e) =>
        //        {
        //            if (e.Data != null)
        //            {
        //                outputHandler?.Invoke(e.Data, GitOutputType.Error);
        //            }
        //        };

        //        process.Start();
        //        process.BeginOutputReadLine();
        //        process.BeginErrorReadLine();
        //        process.WaitForExit();
        //    }
        //    catch (Exception ex)
        //    {
        //        outputHandler?.Invoke($"An error occurred during Git command execution: {ex.Message}", GitOutputType.Error);
        //        throw;
        //    }
        //}

        private static async Task<bool> ExecuteAsync(GitCommandArgs gitCommandArgs, Action<string> outputHandler = null)
        {
            LoggingHelper outputWindow = LoggingHelper.Instance;
            bool isError = false;
            if (gitCommandArgs == null || string.IsNullOrEmpty(gitCommandArgs.WorkingDirectory))
                throw new ArgumentException("The working directory is not valid");

            if (!File.Exists(GitPath))
                throw new FileNotFoundException($"Git executable not found at: {GitPath}");

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
                var outputDataReceivedTask = new TaskCompletionSource<bool>();
                var errorDataReceivedTask = new TaskCompletionSource<bool>();

                process.OutputDataReceived += (sender, e) =>
                {
                    if (e.Data != null)
                    {
                        if (e.Data.IndexOf("fatal", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        e.Data.IndexOf("error", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            outputWindow?.WriteToOutputWindowAsync(e.Data);
                            isError = true;
                        }
                        else outputHandler?.Invoke(e.Data);

                    }
                };

                process.ErrorDataReceived += (sender, e) =>
                {
                    if (e.Data != null)
                    {
                        outputWindow?.WriteToOutputWindowAsync(e.Data);
                        outputHandler?.Invoke(e.Data);
                        isError = true;
                    }
                };

                process.EnableRaisingEvents = true;

                process.Exited += (sender, e) =>
                {
                    outputDataReceivedTask.TrySetResult(true);
                    errorDataReceivedTask.TrySetResult(true);
                };

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                await Task.WhenAll(outputDataReceivedTask.Task, errorDataReceivedTask.Task);
                return !isError;
            }
            catch (Exception ex)
            {
                isError = outputWindow.ShowOutputPane = true;
                outputWindow?.WriteToOutputWindowAsync($"An error occurred during Git command execution: {ex.Message}");
                return !isError;
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
                Argument = "branch -a"
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
            LoggingHelper.Instance?.WriteToOutputWindowAsync("Executing create git worktree command");
            var isCompleted = await ExecuteAsync(new GitCommandArgs()
            {
                Argument = $"worktree add {force}{workTreePath} {branchName.ToGitCommandExecutableFormat()}",

                WorkingDirectory = repositoryPath
            });//Regex.Match(branchName,
               // @"(?:\+?\s?(?:remotes?\/(?:origin|main|upstream)\/(?:HEAD -> (?:origin|main|upstream)\/)?|remotes?\/(?:origin|main|upstream)\/)?|[^\/]+\/)?([^\/]+(?:\/[^\/]+)*)$")
               // .Groups[1].Value
            var result = isCompleted ? "completed" : "failed";
            LoggingHelper.Instance?.WriteToOutputWindowAsync($"Git command execution - {result}");
            return isCompleted;
        }

        public static async Task<bool> RemoveWorkTreeAsync(string repositoryPath, string workTreePath, bool shouldForceCreate)
        {
            string force = shouldForceCreate ? "-f " : "";
            LoggingHelper.Instance?.WriteToOutputWindowAsync("Executing remove git worktree command");
            var isCompleted = await ExecuteAsync(new GitCommandArgs()
            {
                Argument = $"worktree remove {force}{workTreePath}",
                WorkingDirectory = repositoryPath
            });
            var result = isCompleted ? "completed" : "failed";
            LoggingHelper.Instance?.WriteToOutputWindowAsync($"Git command execution - {result}");
            return isCompleted;
        }

        public static async Task<bool> PruneAsync(string repositoryPath)
        {
            LoggingHelper.Instance?.WriteToOutputWindowAsync("Executing prune git worktree command");
            var isCompleted = await ExecuteAsync(new GitCommandArgs()
            {
                Argument = "prune",
                WorkingDirectory = repositoryPath
            });
            var result = isCompleted ? "completed" : "failed";
            LoggingHelper.Instance?.WriteToOutputWindowAsync($"Git command execution - {result}");
            return isCompleted;
        }

        public static async Task<string> GetgitFolderDirectoryAsync(string currentSolutionPath)
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
