using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace GitWorkTree
{
    public enum GitOutputType { Standard, Error };

    public class GitCommandArgs
    {
        public string Argument { get; set; }
        public string WorkingDirectory { get; set; }
    }

    public static class GitBuddy
    {
        private static string GitPath = Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory,
            @"CommonExtensions\Microsoft\TeamFoundation\Team Explorer\Git\cmd\git.exe");

        public static void Execute(GitCommandArgs gitCommandArgs, Action<string, GitOutputType> outputHandler = null)
        {
            if (gitCommandArgs == null || string.IsNullOrEmpty(gitCommandArgs.WorkingDirectory))
            {
                throw new ArgumentException("GitCommandArgs must be provided with a valid working directory.");
            }

            if (!File.Exists(GitPath))
            {
                throw new FileNotFoundException($"Git executable not found at: {GitPath}");
            }

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

                using var process = new Process { StartInfo = startInfo };
                process.OutputDataReceived += (sender, e) =>
                {
                    if (e.Data != null)
                    {
                        //outputHandler?.Invoke(e.Data, GitOutputType.Standard);
                        outputHandler?.Invoke(e.Data, e.Data.IndexOf("fatal",
                            StringComparison.OrdinalIgnoreCase) >= 0 || e.Data.IndexOf("error", StringComparison.OrdinalIgnoreCase) >= 0 ?
                            GitOutputType.Error : GitOutputType.Standard);
                    }
                };

                process.ErrorDataReceived += (sender, e) =>
                {
                    if (e.Data != null)
                    {
                        outputHandler?.Invoke(e.Data, GitOutputType.Error);
                    }
                };

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                process.WaitForExit();
            }
            catch (Exception ex)
            {
                throw new Exception($"An error occurred during Git command execution: {ex.Message}");
            }
        }

        public static List<string> GetWorkTreePaths(string repositoryPath, Action<string, GitOutputType> outputHandler = null)
        {
            List<string> workTreePaths = new List<string>();

            Execute(new GitCommandArgs()
            {
                WorkingDirectory = repositoryPath,
                Argument = "worktree list --porcelain",
            }, (line, type) =>
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

                outputHandler?.Invoke(line, type);
            });

            return workTreePaths;
        }

        public static List<string> GetBranches(string repositoryPath, Action<string, GitOutputType> outputHandler = null)
        {
            List<string> branches = new List<string>();
            string getAllBranchesGitCommand = "branch -a";

            Execute(new GitCommandArgs()
            {
                WorkingDirectory = repositoryPath,
                Argument = getAllBranchesGitCommand
            }, (line, type) =>
            {
                if (!string.IsNullOrWhiteSpace(line))
                {
                    string branchName = line.Trim().TrimStart('*').Trim();
                    branches.Add(branchName);
                }

                outputHandler?.Invoke(line, type);
            });

            return branches;
        }

        public static void CreateWorkTree(string repositoryPath, string branchName, string workTreePath,
            bool shouldForceCreate, Action<string, GitOutputType> outputHandler = null)
        {
            string force = shouldForceCreate ? "-f " : "";
            Execute(new GitCommandArgs()
            {
                Argument = $"worktree add {force}{workTreePath} {Regex.Match(branchName,
                @"(?:\+?\s?(?:remotes?\/(?:origin|main|upstream)\/(?:HEAD -> (?:origin|main|upstream)\/)?|remotes?\/(?:origin|main|upstream)\/)?|[^\/]+\/)?([^\/]+(?:\/[^\/]+)*)$")
                .Groups[1].Value}",

                WorkingDirectory = repositoryPath
            }, outputHandler);
        }

        public static void Prune(string repositoryPath, Action<string, GitOutputType> outputHandler = null)
        {
            Execute(new GitCommandArgs()
            {
                Argument = "prune",
                WorkingDirectory = repositoryPath
            }, outputHandler);
        }

        public static void RemoveWorkTree(string repositoryPath, string branchName, bool shouldForceCreate,
            Action<string, GitOutputType> outputHandler = null)
        {
            string force = shouldForceCreate ? "-f " : "";
            Execute(new GitCommandArgs()
            {
                Argument = $"worktree remove {force}{branchName}",
                WorkingDirectory = repositoryPath
            }, outputHandler);
        }
    }
}
