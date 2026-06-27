using System.Collections.Generic;
using System.Threading.Tasks;

namespace GitWorkTree.Services
{
    public class GitCommitInfo
    {
        public string FullSha { get; }
        public string ShortSha { get; }
        public string Subject { get; }
        public string Author { get; }
        public string Date { get; }

        public GitCommitInfo(string fullSha, string shortSha, string subject, string author = null, string date = null)
        {
            FullSha = fullSha;
            ShortSha = shortSha;
            Subject = subject;
            Author = author;
            Date = date;
        }
    }

    public class WorktreeInfo
    {
        public string Path { get; set; }
        public string Branch { get; set; }
        public string HeadCommit { get; set; }
        public bool IsMain { get; set; }
        public bool IsLocked { get; set; }
        public bool IsPrunable { get; set; }
        public string StatusSummary { get; set; }
        public List<string> Changes { get; set; } = new List<string>();
        public List<GitCommitInfo> Outgoing { get; set; } = new List<GitCommitInfo>();
    }

    public interface IGitService
    {
        Task<string> GetRepositoryPathAsync(string solutionPath);
        Task<List<string>> GetWorkTreePathsAsync(string repositoryPath);
        Task<List<WorktreeInfo>> GetWorktreesAsync(string repositoryPath);
        Task<List<string>> GetBranchesAsync(string repositoryPath);
        Task<GitOperationResult> CreateBranchAsync(string repositoryPath, string newBranchName, string sourceBranchName);
        Task<GitOperationResult> DeleteBranchAsync(string repositoryPath, string branchName);
        Task<GitOperationResult> CreateWorkTreeAsync(string repositoryPath, string branchName, string workTreePath);
        Task<GitOperationResult> RemoveWorkTreeAsync(string repositoryPath, string workTreePath, bool shouldForceCreate);
        Task<GitOperationResult> PruneAsync(string repositoryPath);
        Task<string> GetGitFolderDirectoryAsync(string currentSolutionPath);
        Task<WorktreeInfo> GetWorkTreeDetailsAsync(string repositoryPath, string workTreePath, System.Threading.CancellationToken cancellationToken = default);
        Task<string> ShowFileContentAsync(string repositoryPath, string revisionAndFilePath);
    }
}
