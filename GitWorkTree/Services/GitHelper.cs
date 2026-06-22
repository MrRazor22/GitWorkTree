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
        public static string ToFolderFormat(this string branchName, bool preserveHierarchy)
        {
            string cleanBranch = branchName.ToGitCommandExecutableFormat();
            if (preserveHierarchy)
            {
                return cleanBranch.Replace('/', Path.DirectorySeparatorChar);
            }
            else
            {
                return cleanBranch.Replace('/', '-');
            }
        }

        public static string ToGitCommandExecutableFormat(this string branchName) => Regex.Match(branchName,
                @"(?:\+?\s?(?:remotes?\/(?:[^\/]+)\/(?:HEAD -> (?:[^\/]+)\/)?|remotes?\/(?:[^\/]+)\/)?|[^\/]+\/)?([^\/]+(?:\/[^\/]+)*)$")
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

        private async Task<bool> ExecuteAsync(GitCommandArgs gitCommandArgs, Action<string> outputHandler = null, System.Threading.CancellationToken cancellationToken = default)
        {
            if (gitCommandArgs == null)
            {
                _loggingService?.WriteToOutputWindowAsync("The Git command arguments are null");
                return false;
            }

            string fullArgument = $"-c core.longpaths=true -c core.preloadIndex=true -c core.fscache=true -c index.threads=0 {gitCommandArgs.Argument}";

            return await _commandExecutor.ExecuteAsync(_gitPath, fullArgument, gitCommandArgs.WorkingDirectory, outputHandler, cancellationToken);
        }

        private async Task<GitCommandExecutionResult> ExecuteWithResultAsync(GitCommandArgs gitCommandArgs, Action<string> outputHandler = null, System.Threading.CancellationToken cancellationToken = default)
        {
            if (gitCommandArgs == null)
            {
                _loggingService?.WriteToOutputWindowAsync("The Git command arguments are null");
                return new GitCommandExecutionResult(false, "The Git command arguments are null");
            }

            string fullArgument = $"-c core.longpaths=true -c core.preloadIndex=true -c core.fscache=true -c index.threads=0 {gitCommandArgs.Argument}";

            return await _commandExecutor.ExecuteWithResultAsync(_gitPath, fullArgument, gitCommandArgs.WorkingDirectory, outputHandler, cancellationToken);
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

        public async Task<List<WorktreeInfo>> GetWorktreesAsync(string repositoryPath)
        {
            var worktrees = new List<WorktreeInfo>();
            WorktreeInfo current = null;

            var isCompleted = await ExecuteAsync(new GitCommandArgs()
            {
                WorkingDirectory = repositoryPath,
                Argument = "worktree list --porcelain",
            }, (line) =>
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    return;
                }

                if (line.StartsWith("worktree "))
                {
                    if (current != null)
                    {
                        worktrees.Add(current);
                    }
                    string rawPath = line.Substring("worktree ".Length).Trim();
                    current = new WorktreeInfo
                    {
                        Path = Path.GetFullPath(rawPath)
                    };
                }
                else if (current != null)
                {
                    if (line.StartsWith("HEAD "))
                    {
                        current.HeadCommit = line.Substring("HEAD ".Length).Trim();
                    }
                    else if (line.StartsWith("branch "))
                    {
                        string fullRef = line.Substring("branch ".Length).Trim();
                        const string prefix = "refs/heads/";
                        current.Branch = fullRef.StartsWith(prefix) ? fullRef.Substring(prefix.Length) : fullRef;
                    }
                    else if (line == "locked" || line.StartsWith("locked reason:"))
                    {
                        current.IsLocked = true;
                    }
                    else if (line == "prunable")
                    {
                        current.IsPrunable = true;
                    }
                }
            });

            if (current != null)
            {
                worktrees.Add(current);
            }

            if (isCompleted)
            {
                foreach (var w in worktrees)
                {
                    if (string.IsNullOrEmpty(w.Branch))
                    {
                        w.Branch = !string.IsNullOrEmpty(w.HeadCommit) && w.HeadCommit.Length >= 7
                            ? $"({w.HeadCommit.Substring(0, 7)})"
                            : "(detached HEAD)";
                    }
                }
                if (worktrees.Count > 0)
                {
                    worktrees[0].IsMain = true;
                }
                return worktrees;
            }
            return null;
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

        public async Task<GitOperationResult> CreateBranchAsync(string repositoryPath, string newBranchName, string sourceBranchName)
        {
            var result = await ExecuteWithResultAsync(new GitCommandArgs()
            {
                Argument = $"branch {newBranchName} {sourceBranchName.ToGitCommandExecutableFormat()}",
                WorkingDirectory = repositoryPath
            }, (line) =>
            {
                _loggingService?.WriteToOutputWindowAsync(line);
            });
            return new GitOperationResult(result.Success, result.StandardError);
        }

        public async Task<GitOperationResult> DeleteBranchAsync(string repositoryPath, string branchName)
        {
            var result = await ExecuteWithResultAsync(new GitCommandArgs()
            {
                Argument = $"branch -D {branchName}",
                WorkingDirectory = repositoryPath
            }, (line) =>
            {
                _loggingService?.WriteToOutputWindowAsync(line);
            });
            return new GitOperationResult(result.Success, result.StandardError);
        }

        public async Task<GitOperationResult> CreateWorkTreeAsync
            (string repositoryPath, string branchName, string workTreePath)
        {
            var result = await ExecuteWithResultAsync(new GitCommandArgs()
            {
                Argument = $"worktree add {SolutionHelper.NormalizePath(workTreePath)} {branchName.ToGitCommandExecutableFormat()}",
                WorkingDirectory = repositoryPath
            }, (line) =>
            {
                _loggingService?.WriteToOutputWindowAsync(line);
            });
            return new GitOperationResult(result.Success, result.StandardError);
        }

        public async Task<GitOperationResult> RemoveWorkTreeAsync(string repositoryPath, string workTreePath, bool shouldForceCreate)
        {
            string force = shouldForceCreate ? "-f " : "";
            var result = await ExecuteWithResultAsync(new GitCommandArgs()
            {
                Argument = $"worktree remove {force}{SolutionHelper.NormalizePath(workTreePath)}",
                WorkingDirectory = repositoryPath
            }, (line) =>
            {
                _loggingService?.WriteToOutputWindowAsync(line);
            });
            return new GitOperationResult(result.Success, result.StandardError);
        }

        public async Task<GitOperationResult> PruneAsync(string repositoryPath)
        {
            var result = await ExecuteWithResultAsync(new GitCommandArgs()
            {
                Argument = "worktree prune --expire=now",
                WorkingDirectory = repositoryPath
            }, (line) =>
            {
                _loggingService?.WriteToOutputWindowAsync(line);
            });
            return new GitOperationResult(result.Success, result.StandardError);
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

        public async Task<(string Branch, string StatusSummary, List<string> Changes, List<GitCommitInfo> Outgoing)> GetWorkTreeDetailsAsync(string repositoryPath, string workTreePath, System.Threading.CancellationToken cancellationToken = default)
        {
            string branch = "Unknown";
            int staged = 0;
            int unstaged = 0;
            int untracked = 0;
            int ahead = 0;
            int behind = 0;
            List<string> changes = new List<string>();
            List<GitCommitInfo> outgoing = new List<GitCommitInfo>();

            bool hasUpstream = false;
            var statusResult = await ExecuteWithResultAsync(new GitCommandArgs()
            {
                WorkingDirectory = workTreePath,
                Argument = "status --porcelain -b"
            }, (line) =>
            {
                if (string.IsNullOrWhiteSpace(line)) return;

                if (line.StartsWith("## "))
                {
                    // If tracking branch is configured and not gone, line contains "..."
                    hasUpstream = line.Contains("...") && !line.Contains("[gone]");

                    // Parse branch name, e.g. "master...origin/master [ahead 1]" -> "master"
                    string branchPart = line.Substring(3).Trim();
                    int bracketIdx = branchPart.IndexOf('[');
                    if (bracketIdx >= 0)
                    {
                        branchPart = branchPart.Substring(0, bracketIdx).Trim();
                    }

                    int dotIdx = branchPart.IndexOf("...");
                    if (dotIdx > 0)
                    {
                        branch = branchPart.Substring(0, dotIdx);
                    }
                    else
                    {
                        branch = branchPart;
                    }

                    // Parse ahead/behind if present, e.g. ## master...origin/master [ahead 1, behind 2]
                    var match = Regex.Match(line, @"\[ahead\s+(\d+)\]");
                    if (match.Success) ahead = int.Parse(match.Groups[1].Value);
                    
                    match = Regex.Match(line, @"\[behind\s+(\d+)\]");
                    if (match.Success) behind = int.Parse(match.Groups[1].Value);

                    var aheadBehindMatch = Regex.Match(line, @"\[ahead\s+(\d+),\s+behind\s+(\d+)\]");
                    if (aheadBehindMatch.Success)
                    {
                        ahead = int.Parse(aheadBehindMatch.Groups[1].Value);
                        behind = int.Parse(aheadBehindMatch.Groups[2].Value);
                    }
                    return;
                }

                changes.Add(line);

                string statusCodes = line.Substring(0, 2);
                char indexStatus = statusCodes[0];
                char workTreeStatus = statusCodes[1];

                if (indexStatus == '?')
                {
                    untracked++;
                }
                else
                {
                    if (indexStatus != ' ' && indexStatus != 'U') staged++;
                    if (workTreeStatus != ' ' && workTreeStatus != '?' && workTreeStatus != 'U') unstaged++;
                }
            }, cancellationToken).ConfigureAwait(false);

            if (!statusResult.Success)
            {
                throw new InvalidOperationException(statusResult.StandardError);
            }

            // 3. Outgoing commits (git log @{u}..HEAD --pretty=format:"%H%x09%h%x09%s%x09%an%x09%ad" --date=short) if upstream is configured
            if (hasUpstream)
            {
                await ExecuteAsync(new GitCommandArgs()
                {
                    WorkingDirectory = workTreePath,
                    Argument = "log @{u}..HEAD --pretty=format:\"%H%x09%h%x09%s%x09%an%x09%ad\" --date=short -n 50"
                }, (line) =>
                {
                    if (!string.IsNullOrWhiteSpace(line))
                    {
                        string[] parts = line.Trim().Split('\t');
                        if (parts.Length >= 5)
                        {
                            outgoing.Add(new GitCommitInfo(parts[0], parts[1], parts[2], parts[3], parts[4]));
                        }
                        else if (parts.Length >= 3)
                        {
                            outgoing.Add(new GitCommitInfo(parts[0], parts[1], parts[2]));
                        }
                    }
                }, cancellationToken).ConfigureAwait(false);
            }

            string statusSummary = $"{staged} staged, {unstaged + untracked} changes ({untracked} untracked) · ↑{ahead} ↓{behind}";

            return (branch, statusSummary, changes, outgoing);
        }

        public async Task<string> ShowFileContentAsync(string repositoryPath, string revisionAndFilePath)
        {
            var contentBuilder = new StringBuilder();
            bool success = await ExecuteAsync(new GitCommandArgs()
            {
                WorkingDirectory = repositoryPath,
                Argument = $"show {revisionAndFilePath}"
            }, (line) =>
            {
                contentBuilder.AppendLine(line);
            }).ConfigureAwait(false);

            return success ? contentBuilder.ToString() : null;
        }
    }
}
