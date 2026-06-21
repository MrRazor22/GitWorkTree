using System;
using System.Threading.Tasks;

namespace GitWorkTree.Services
{
    public interface IGitCommandExecutor
    {
        Task<bool> ExecuteAsync(string gitPath, string arguments, string workingDirectory, Action<string> outputHandler, System.Threading.CancellationToken cancellationToken = default);
        Task<GitCommandExecutionResult> ExecuteWithResultAsync(string gitPath, string arguments, string workingDirectory, Action<string> outputHandler = null, System.Threading.CancellationToken cancellationToken = default);
    }
}
