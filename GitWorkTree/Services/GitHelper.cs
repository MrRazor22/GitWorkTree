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

        public GitHelper(ILoggingService loggingService, IGitCommandExecutor commandExecutor = null)
        {
            _loggingService = loggingService;
            _commandExecutor = commandExecutor ?? new GitCommandExecutor(loggingService);
        }

        public async Task<string> GetRepositoryPathAsync(string solutionPath)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(solutionPath)) return null;
                if (File.Exists(solutionPath))
                {
                    solutionPath = Path.GetDirectoryName(solutionPath);
                }
                var gitFolderPath = await GetGitFolderDirectoryAsync(solutionPath).ConfigureAwait(false);

                string gitFileName = Path.GetFileName(gitFolderPath);
                if (gitFileName != null && gitFileName.Equals(".git", StringComparison.OrdinalIgnoreCase))
                    return solutionPath;
                else if (gitFolderPath != null && gitFolderPath.Replace('\\', '/').Contains(".git/worktrees"))
                    return Path.GetFullPath(Path.Combine(gitFolderPath, "..", "..", ".."));
                return null;
            }
            catch (Exception ex)
            {
                _loggingService?.WriteToOutputWindowAsync($"Failed to get repository path: {ex.Message}");
                return null;
            }
        }

        public async Task<List<string>> GetWorkTreePathsAsync(string repositoryPath)
        {
            List<string> workTreePaths = new List<string>();
            var result = await _commandExecutor.ExecuteAsync("worktree list --porcelain", repositoryPath, (line) =>
            {
                if (line.StartsWith("worktree "))
                {
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
            return result.Success ? workTreePaths : null;
        }

        public async Task<List<WorktreeInfo>> GetWorktreesAsync(string repositoryPath)
        {
            var worktrees = new List<WorktreeInfo>();
            WorktreeInfo current = null;

            var result = await _commandExecutor.ExecuteAsync("worktree list --porcelain", repositoryPath, (line) =>
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

            if (result.Success)
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
            var result = await _commandExecutor.ExecuteAsync("--no-pager branch -a --no-color", repositoryPath, (line) =>
            {
                if (!string.IsNullOrWhiteSpace(line))
                {
                    string branchName = line.Trim().TrimStart('*').Trim();
                    branches.Add(branchName);
                }
            });
            if (result.Success) return branches;
            else return null;
        }

        public async Task<GitOperationResult> CreateBranchAsync(string repositoryPath, string newBranchName, string sourceBranchName)
        {
            var result = await _commandExecutor.ExecuteAsync($"branch {newBranchName} {sourceBranchName.ToGitCommandExecutableFormat()}", repositoryPath, (line) =>
            {
                _loggingService?.WriteToOutputWindowAsync(line);
            });
            return new GitOperationResult(result.Success, result.StandardError);
        }

        public async Task<GitOperationResult> DeleteBranchAsync(string repositoryPath, string branchName)
        {
            var result = await _commandExecutor.ExecuteAsync($"branch -D {branchName}", repositoryPath, (line) =>
            {
                _loggingService?.WriteToOutputWindowAsync(line);
            });
            return new GitOperationResult(result.Success, result.StandardError);
        }

        public async Task<GitOperationResult> CreateWorkTreeAsync(string repositoryPath, string branchName, string workTreePath)
        {
            var result = await _commandExecutor.ExecuteAsync($"worktree add {SolutionHelper.NormalizePath(workTreePath)} {branchName.ToGitCommandExecutableFormat()}", repositoryPath, (line) =>
            {
                _loggingService?.WriteToOutputWindowAsync(line);
            });
            return new GitOperationResult(result.Success, result.StandardError);
        }

        public async Task<GitOperationResult> RemoveWorkTreeAsync(string repositoryPath, string workTreePath, bool shouldForceCreate)
        {
            string force = shouldForceCreate ? "-f " : "";
            var result = await _commandExecutor.ExecuteAsync($"worktree remove {force}{SolutionHelper.NormalizePath(workTreePath)}", repositoryPath, (line) =>
            {
                _loggingService?.WriteToOutputWindowAsync(line);
            });
            return new GitOperationResult(result.Success, result.StandardError);
        }

        public async Task<GitOperationResult> PruneAsync(string repositoryPath)
        {
            var result = await _commandExecutor.ExecuteAsync("worktree prune --expire=now", repositoryPath, (line) =>
            {
                _loggingService?.WriteToOutputWindowAsync(line);
            });
            return new GitOperationResult(result.Success, result.StandardError);
        }

        public async Task<string> GetGitFolderDirectoryAsync(string currentSolutionPath)
        {
            string commandoutput = "";
            var result = await _commandExecutor.ExecuteAsync("rev-parse --git-dir", currentSolutionPath, (line) =>
            {
                if (!string.IsNullOrWhiteSpace(line))
                {
                    commandoutput = line.Trim();
                }
            });

            if (result.Success) return commandoutput;
            return null;
        }

        public async Task<WorktreeInfo> GetWorkTreeDetailsAsync(string repositoryPath, string workTreePath, System.Threading.CancellationToken cancellationToken = default)
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
            var statusResult = await _commandExecutor.ExecuteAsync("status --porcelain -b", workTreePath, (line) =>
            {
                if (string.IsNullOrWhiteSpace(line)) return;

                if (line.StartsWith("## "))
                {
                    hasUpstream = line.Contains("...") && !line.Contains("[gone]");

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

            if (hasUpstream)
            {
                await _commandExecutor.ExecuteAsync("log @{u}..HEAD --pretty=format:\"%H%x09%h%x09%s%x09%an%x09%ad\" --date=short -n 50", workTreePath, (line) =>
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

            return new WorktreeInfo
            {
                Path = workTreePath,
                Branch = branch,
                StatusSummary = statusSummary,
                Changes = changes,
                Outgoing = outgoing
            };
        }

        public async Task<string> ShowFileContentAsync(string repositoryPath, string revisionAndFilePath)
        {
            var contentBuilder = new StringBuilder();
            var result = await _commandExecutor.ExecuteAsync($"show {revisionAndFilePath}", repositoryPath, (line) =>
            {
                contentBuilder.AppendLine(line);
            }).ConfigureAwait(false);

            return result.Success ? contentBuilder.ToString() : null;
        }
    }
}
