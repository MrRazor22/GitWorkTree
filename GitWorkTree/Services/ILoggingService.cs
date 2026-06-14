using System.Threading.Tasks;

namespace GitWorkTree.Services
{
    public interface ILoggingService
    {
        Task WriteToOutputWindowAsync(string message, bool showOutputPane = false);
        Task UpdateStatusBar(string newMessage);
        Task SetCommandStatusBusy(bool isBusy = true);
        Task SetCommandCompletionStatus(bool isCompleted);
    }
}
