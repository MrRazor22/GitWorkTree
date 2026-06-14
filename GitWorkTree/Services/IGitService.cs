using System.Collections.Generic;
using System.Threading.Tasks;

namespace GitWorkTree.Services
{
    public interface IGitService
    {
        Task<List<string>> GetWorkTreePathsAsync(string repositoryPath);
        Task<List<string>> GetBranchesAsync(string repositoryPath);
        Task<bool> CreateBranchAsync(string repositoryPath, string newBranchName, string sourceBranchName);
        Task<bool> DeleteBranchAsync(string repositoryPath, string branchName);
        Task<bool> CreateWorkTreeAsync(string repositoryPath, string branchName, string workTreePath, bool shouldForceCreate);
        Task<bool> RemoveWorkTreeAsync(string repositoryPath, string workTreePath, bool shouldForceCreate);
        Task<bool> PruneAsync(string repositoryPath);
        Task<string> GetGitFolderDirectoryAsync(string currentSolutionPath);
    }
}
