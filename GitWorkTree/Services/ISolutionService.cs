using System.Threading.Tasks;

namespace GitWorkTree.Services
{
    public interface ISolutionService
    {
        string GetRepositoryPath(string solutionPath);
        Task<bool> OpenSolution(string newSolutionPath, bool openInCurrentInstance);
    }
}
