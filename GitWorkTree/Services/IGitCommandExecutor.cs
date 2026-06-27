using System;
using System.Threading.Tasks;

namespace GitWorkTree.Services
{
    public interface IGitCommandExecutor
    {
        Task<GitCommandExecutionResult> ExecuteAsync(string arguments, string workingDirectory, Action<string> outputHandler = null, System.Threading.CancellationToken cancellationToken = default);
    }
}
