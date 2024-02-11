using System;
using System.Threading;
using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace GitWorkTree
{
    public class VsOutputWindow
    {
        private static readonly Lazy<VsOutputWindow> lazyInstance = new Lazy<VsOutputWindow>(() => new VsOutputWindow(), LazyThreadSafetyMode.ExecutionAndPublication);

        private IVsOutputWindowPane outputPane;
        private DTE2 dte;

        public static VsOutputWindow Instance => lazyInstance.Value;

        public VsOutputWindow()
        {
            dte = Package.GetGlobalService(typeof(SDTE)) as EnvDTE80.DTE2;
        }

        public async Task WriteToOutputWindowAsync(string message, bool isError = false)
        {
            await ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                if (outputPane == null)
                {
                    LazyInitializer.EnsureInitialized(ref outputPane, () => CreatePane());
                }

                // Ensure the output pane is visible
                outputPane?.Activate();

                // Check for null before writing to the output pane
                string formattedMessage = $"{GetLogPrefix()} {message}\r";

                await ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
                {
                    await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                    //outputPane?.OutputStringThreadSafe(isError ? $"\x1B[31m{formattedMessage}\x1B[0m" : formattedMessage);
                    outputPane?.OutputTaskItemString(formattedMessage, VSTASKPRIORITY.TP_HIGH, VSTASKCATEGORY.CAT_BUILDCOMPILE, null, 0, null, 0, null);

                    dte.Windows.Item(EnvDTE.Constants.vsWindowKindOutput).Visible = true;
                });
            });
        }

        public void UpdateStatusBar(string message)
        {
            ThreadHelper.JoinableTaskFactory.Run(async () =>
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                if (dte != null) dte.StatusBar.Text = message;
            });
        }

        private string GetLogPrefix()
        {
            return $">";
        }

        private IVsOutputWindowPane CreatePane()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var outputWindow = Package.GetGlobalService(typeof(SVsOutputWindow)) as IVsOutputWindow;
            var paneGuid = Guid.NewGuid();
            outputWindow.CreatePane(ref paneGuid, "GitWorkTree", 1, 1);
            outputWindow.GetPane(ref paneGuid, out var pane);
            return pane;
        }
    }


}
