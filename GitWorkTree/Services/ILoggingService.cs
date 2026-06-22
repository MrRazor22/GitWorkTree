using System;
using System.Threading.Tasks;

namespace GitWorkTree.Services
{
    public interface ILoggingSession : IDisposable
    {
        string CommandName { get; }
        Task LogAsync(string message, bool showOutputPane = false);
        Task CompleteAsync(bool success);
    }

    public interface ILoggingService
    {
        Task WriteToOutputWindowAsync(string message, bool showOutputPane = false);
        Task UpdateStatusBar(string newMessage);
        Task SetCommandStatusBusy(bool isBusy = true);
        Task SetCommandCompletionStatus(bool isCompleted);
        ILoggingSession StartSession(string commandName);
    }
}
