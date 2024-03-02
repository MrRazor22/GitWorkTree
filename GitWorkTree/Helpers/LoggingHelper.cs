using System.Threading;
using EnvDTE80;
using Microsoft.VisualStudio.Shell.Interop;

namespace GitWorkTree.Helpers
{
    public class LoggingHelper
    {
        private static readonly Lazy<LoggingHelper> lazyInstance = new Lazy<LoggingHelper>(() => new LoggingHelper(), LazyThreadSafetyMode.ExecutionAndPublication);

        private IVsOutputWindowPane outputPane;
        private DTE2 dte;
        private const string busyMessage = $"{Vsix.Name} command in progress...";

        public bool SetStatusBusy
        {
            set
            {
                if (value) UpdateStatusBar(busyMessage);
                else UpdateStatusBar("", busyMessage);
            }
        }


        public static LoggingHelper Instance => lazyInstance.Value;

        private LoggingHelper()
        {
            try
            {
                ThreadHelper.ThrowIfNotOnUIThread();
                dte = Package.GetGlobalService(typeof(SDTE)) as EnvDTE80.DTE2;
            }
            catch (Exception ex)
            {
                // Handle initialization error, log or throw as appropriate
                Console.WriteLine($"Error initializing LoggingHelper: {ex.Message}");
            }
        }



        public async Task WriteToOutputWindowAsync(string message, bool ShowOutputPane = false)
        {
            await Task.Run(() =>
            {
                try
                {
                    ThreadHelper.JoinableTaskFactory.Run(async () =>
                    {
                        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                        if (outputPane == null) outputPane = CreatePane();

                        // Check for null before writing to the output pane
                        string formattedMessage = $">{message}\r";

                        if (ShowOutputPane)
                        {
                            outputPane?.Activate();
                            dte.Windows.Item(EnvDTE.Constants.vsWindowKindOutput).Visible = true;
                        }

                        outputPane?.OutputStringThreadSafe(formattedMessage);
                    });
                }
                catch (Exception ex)
                {
                    // Handle or log the exception during logging
                    Console.WriteLine($"Error: {ex.Message} while logging: {message}");
                }
            });
        }

        public void UpdateStatusBar(string newMessage, string oldMessage = null)
        {
            ThreadHelper.JoinableTaskFactory.Run(async () =>
            {
                try
                {
                    await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                    if (dte != null)
                    {
                        if ((oldMessage == null) || (oldMessage != null && dte.StatusBar.Text == oldMessage))
                            dte.StatusBar.Text = newMessage;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error: {ex.Message} while updating: {newMessage}");
                }
            });
        }

        private IVsOutputWindowPane CreatePane()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var outputWindow = Package.GetGlobalService(typeof(SVsOutputWindow)) as IVsOutputWindow;
            if (outputWindow == null)
            {
                throw new InvalidOperationException("Unable to obtain IVsOutputWindow service.");
            }

            var paneGuid = PackageGuids.guidGitWorkTreeOutpane;
            outputWindow.CreatePane(ref paneGuid, Vsix.Name, 1, 1);
            outputWindow.GetPane(ref paneGuid, out var pane);

            if (pane == null)
            {
                throw new InvalidOperationException("Unable to create or retrieve the output pane.");
            }

            return pane;
        }
    }
}
