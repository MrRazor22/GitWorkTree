using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using EnvDTE80;
using Microsoft.VisualStudio.Shell.Interop;

namespace GitWorkTree.Services
{
    public class LoggingHelper : ILoggingService
    {
        private static readonly Lazy<LoggingHelper> lazyInstance = new Lazy<LoggingHelper>(() => new LoggingHelper(), LazyThreadSafetyMode.ExecutionAndPublication);

        private IVsOutputWindowPane outputPane;
        private DTE2 dte;

        public string GetCallingMethodName([CallerMemberName] string caller = "")
        {
            return string.IsNullOrEmpty(caller) ? Vsix.Name : caller;
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

            try
            {
                await ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
                {
                    await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                    if (outputPane == null) outputPane = CreatePane();

                    // Check for null before writing to the output pane
                    string formattedMessage = $">{message + Environment.NewLine}";

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
        }

        public static readonly System.Threading.AsyncLocal<ILoggingSession> _ambientSession = new System.Threading.AsyncLocal<ILoggingSession>();
        public static ILoggingSession AmbientSession
        {
            get => _ambientSession.Value;
            set => _ambientSession.Value = value;
        }

        private readonly object _sessionLock = new object();
        private readonly List<string> _activeSessions = new List<string>();

        public ILoggingSession StartSession(string commandName)
        {
            var session = new LoggingSession(this, commandName);
            AmbientSession = session;

            lock (_sessionLock)
            {
                _activeSessions.Add(commandName);
            }

            _ = UpdateSessionStatusAsync();
            return session;
        }

        private async Task UpdateSessionStatusAsync()
        {
            string status;
            lock (_sessionLock)
            {
                if (_activeSessions.Count > 0)
                {
                    status = $"{Vsix.Name} - Running: {string.Join(", ", _activeSessions)}...";
                }
                else
                {
                    status = "";
                }
            }
            await UpdateStatusBarInternal(status);
        }

        private async Task CompleteSessionAsync(string commandName, bool success)
        {
            lock (_sessionLock)
            {
                _activeSessions.Remove(commandName);
            }

            lock (_sessionLock)
            {
                if (_activeSessions.Count > 0)
                {
                    _ = UpdateSessionStatusAsync();
                    return;
                }
            }

            string status = $"{Vsix.Name} command '{commandName}' {(success ? "completed" : "failed")}";
            await UpdateStatusBarInternal(status);
        }

        private class LoggingSession : ILoggingSession
        {
            private readonly LoggingHelper _parent;
            private bool _completed;

            public string CommandName { get; }

            public LoggingSession(LoggingHelper parent, string commandName)
            {
                _parent = parent;
                CommandName = commandName;
            }

            public async Task LogAsync(string message, bool showOutputPane = false)
            {
                await _parent.WriteToOutputWindowAsync($"[{CommandName}] {message}", showOutputPane);
            }

            public async Task CompleteAsync(bool success)
            {
                if (!_completed)
                {
                    _completed = true;
                    await _parent.CompleteSessionAsync(CommandName, success);
                }
            }

            public void Dispose()
            {
                if (!_completed)
                {
                    _ = CompleteAsync(false);
                }
                if (AmbientSession == this)
                {
                    AmbientSession = null;
                }
            }
        }

        public async Task UpdateStatusBar(string newMessage) => await UpdateStatusBarInternal(newMessage, null);
        public async Task SetCommandStatusBusy(bool IsBusy = true)
        {
            string status = $"{Vsix.Name} command in progress...";
            if (IsBusy) await UpdateStatusBarInternal(status);
            else await UpdateStatusBarInternal("", status);
        }
        public async Task SetCommandCompletionStatus(bool isCompleted) => await UpdateStatusBarInternal($"{$"{Vsix.Name} command"} {(isCompleted ? "completed" : "failed")}");

        private async Task UpdateStatusBarInternal(string newMessage, string oldMessage = null)
        {
            try
            {
                await ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
                {
                    await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                    if (dte != null)
                    {
                        if ((oldMessage == null) || (oldMessage != null && dte.StatusBar.Text == oldMessage))
                            dte.StatusBar.Text = newMessage;
                    }
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message} while updating: {newMessage}");
            }
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
