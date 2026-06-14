using System;
using System.Threading.Tasks;

namespace GitWorkTree.Services
{
    public interface IGitCommandExecutor
    {
        Task<bool> ExecuteAsync(string gitPath, string arguments, string workingDirectory, Action<string> outputHandler);
    }
}
