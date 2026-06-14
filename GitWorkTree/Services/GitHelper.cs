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

namespace GitWorkTree.Services
{
    public class GitCommandArgs
    {
        public string Argument { get; set; }
        public string WorkingDirectory { get; set; }
    }

    public static class GitHelperExtensions
    {
        public static string ToFolderFormat(this string branchName) => Regex.Match(branchName, @"(?:.*\/)?(?:head -> |origin\/|remote\/)?\+?\s*([^'/]+)").Groups[1].Value ?? branchName;
        public static string ToGitCommandExecutableFormat(this string branchName) => Regex.Match(branchName,
                @"(?:\+?\s?(?:remotes?\/(?:origin|main|upstream)\/(?:HEAD -> (?:origin|main|upstream)\/)?|remotes?\/(?:origin|main|upstream)\/)?|[^\/]+\/)?([^\/]+(?:\/[^\/]+)*)$")
                .Groups[1].Value ?? branchName;
    }

    public class GitHelper : IGitService
    {
        private readonly ILoggingService _loggingService;
        private readonly IGitCommandExecutor _commandExecutor;
        private readonly string _gitPath;

        public GitHelper(ILoggingService loggingService, IGitCommandExecutor commandExecutor = null, string gitPath = null)
        {
            _loggingService = loggingService;
            _commandExecutor = commandExecutor ?? new GitCommandExecutor(loggingService);
            _gitPath = gitPath ?? Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory,
                @"CommonExtensions\Microsoft\TeamFoundation\Team Explorer\Git\cmd\git.exe");
        }

        private async Task<bool> ExecuteAsync(GitCommandArgs gitCommandArgs, Action<string> outputHandler = null)
        {
            if (gitCommandArgs == null)
            {
                _loggingService?.WriteToOutputWindowAsync("The Git command arguments are null");
                return false;
            }

            string fullArgument = $"-c core.longpaths=true {gitCommandArgs.Argument}";

            return await _commandExecutor.ExecuteAsync(_gitPath, fullArgument, gitCommandArgs.WorkingDirectory, outputHandler);
        }

        public async Task<List<string>> GetWorkTreePathsAsync(string repositoryPath)
        {
            List<string> workTreePaths = new List<string>();
            var isCompleted = await ExecuteAsync(new GitCommandArgs()
            {
                WorkingDirectory = repositoryPath,
                Argument = "worktree list --porcelain",
            }, (line) =>
            {
                if (line.StartsWith("worktree "))
                {
                    // everything after "worktree " is the path (can contain spaces)
                    string rawPath = line.Substring("worktree ".Length).Trim();
                    if (!string.IsNullOrEmpty(rawPath))
                    {
                        string worktreePath = Path.GetFullPath(rawPath);
                        string mainRepoPath = Path.GetFullPath(repositoryPath);

                        if (!worktreePath.Equals(mainRepoPath, StringComparison.OrdinalIgnoreCase))
                        {
                            workTreePaths.Add(worktreePath);
                        }
                    }
                }
            });
            return isCompleted ? workTreePaths : null;
        }

        public async Task<List<string>> GetBranchesAsync(string repositoryPath)
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

        public async Task<bool> CreateBranchAsync(string repositoryPath, string newBranchName, string sourceBranchName)
        {
            return await ExecuteAsync(new GitCommandArgs()
            {
                Argument = $"branch {newBranchName} {sourceBranchName.ToGitCommandExecutableFormat()}",
                WorkingDirectory = repositoryPath
            }, (line) =>
            {
                _loggingService?.WriteToOutputWindowAsync(line);
            });
        }

        public async Task<bool> DeleteBranchAsync(string repositoryPath, string branchName)
        {
            return await ExecuteAsync(new GitCommandArgs()
            {
                Argument = $"branch -D {branchName}",
                WorkingDirectory = repositoryPath
            }, (line) =>
            {
                _loggingService?.WriteToOutputWindowAsync(line);
            });
        }

        public async Task<bool> CreateWorkTreeAsync
            (string repositoryPath, string branchName, string workTreePath, bool shouldForceCreate)
        {
            string force = shouldForceCreate ? "-f " : "";
            return await ExecuteAsync(new GitCommandArgs()
            {
                Argument = $"worktree add {force}{SolutionHelper.NormalizePath(workTreePath)} {branchName.ToGitCommandExecutableFormat()}",
                WorkingDirectory = repositoryPath
            }, (line) =>
            {
                _loggingService?.WriteToOutputWindowAsync(line);
            });
        }

        public async Task<bool> RemoveWorkTreeAsync(string repositoryPath, string workTreePath, bool shouldForceCreate)
        {
            string force = shouldForceCreate ? "-f " : "";
            return await ExecuteAsync(new GitCommandArgs()
            {
                Argument = $"worktree remove {force}{SolutionHelper.NormalizePath(workTreePath)}",
                WorkingDirectory = repositoryPath
            }, (line) =>
            {
                _loggingService?.WriteToOutputWindowAsync(line);
            });
        }

        public async Task<bool> PruneAsync(string repositoryPath)
        {
            return await ExecuteAsync(new GitCommandArgs()
            {
                Argument = "worktree prune --expire=now",
                WorkingDirectory = repositoryPath
            }, (line) =>
            {
                _loggingService?.WriteToOutputWindowAsync(line);
            });
        }

        public async Task<string> GetGitFolderDirectoryAsync(string currentSolutionPath)
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
