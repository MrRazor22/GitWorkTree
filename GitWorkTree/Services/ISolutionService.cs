using System.Threading.Tasks;

namespace GitWorkTree.Services
{
    public interface ISolutionService
    {
        Task<bool> OpenSolution(string newSolutionPath, bool openInCurrentInstance);
    }
}
