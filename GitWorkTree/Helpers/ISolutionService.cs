using System.Threading.Tasks;

namespace GitWorkTree.Helpers
{
    public interface ISolutionService
    {
        string GetRepositoryPath(string solutionPath);
        Task<bool> OpenSolution(string newSolutionPath, bool openInCurrentInstance);
    }
}
