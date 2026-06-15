using System.Collections.Generic;
using System.Threading.Tasks;

namespace GitWorkTree.Services
{
    public sealed class GitCommitInfo
    {
        public string FullSha { get; }
        public string ShortSha { get; }
        public string Subject { get; }
        public string Author { get; }
        public string Date { get; }
        public string DisplayText => $"{ShortSha} {Subject}";

        public GitCommitInfo(string fullSha, string shortSha, string subject, string author = "", string date = "")
        {
            FullSha = fullSha;
            ShortSha = shortSha;
            Subject = subject;
            Author = author;
            Date = date;
        }
    }

    public sealed class WorktreeInfo
    {
        public string Path { get; set; }
        public string Branch { get; set; }
        public string HeadCommit { get; set; }
        public bool IsMain { get; set; }
        public bool IsLocked { get; set; }
        public bool IsPrunable { get; set; }
    }

    public interface IGitService
    {
        Task<List<string>> GetWorkTreePathsAsync(string repositoryPath);
        Task<List<WorktreeInfo>> GetWorktreesAsync(string repositoryPath);
        Task<List<string>> GetBranchesAsync(string repositoryPath);
        Task<bool> CreateBranchAsync(string repositoryPath, string newBranchName, string sourceBranchName);
        Task<bool> DeleteBranchAsync(string repositoryPath, string branchName);
        Task<bool> CreateWorkTreeAsync(string repositoryPath, string branchName, string workTreePath, bool shouldForceCreate);
        Task<bool> RemoveWorkTreeAsync(string repositoryPath, string workTreePath, bool shouldForceCreate);
        Task<bool> PruneAsync(string repositoryPath);
        Task<string> GetGitFolderDirectoryAsync(string currentSolutionPath);
        Task<bool> IsWorktreeDirtyAsync(string worktreePath);
        Task<(string Branch, string StatusSummary, List<string> Changes, List<GitCommitInfo> Outgoing)> GetWorkTreeDetailsAsync(string repositoryPath, string workTreePath);
        Task<string> ShowFileContentAsync(string repositoryPath, string revisionAndFilePath);
    }
}
