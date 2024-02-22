using System.Threading;
using EnvDTE80;
using Microsoft.VisualStudio.Shell.Interop;

namespace GitWorkTree
{
    public class VsOutputWindow
    {
        private static readonly Lazy<VsOutputWindow> lazyInstance = new Lazy<VsOutputWindow>(() => new VsOutputWindow(), LazyThreadSafetyMode.ExecutionAndPublication);

        private IVsOutputWindowPane outputPane;
        private DTE2 dte;

        public static VsOutputWindow Instance => lazyInstance.Value;

        public bool ShowOutputPane { get; set; } = false;

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

                // Check for null before writing to the output pane
                string formattedMessage = $">{message}\r";

                if (ShowOutputPane)
                {
                    outputPane?.Activate();
                    dte.Windows.Item(EnvDTE.Constants.vsWindowKindOutput).Visible = true;
                    ShowOutputPane = false;
                }

                outputPane?.OutputStringThreadSafe(formattedMessage);
                //outputPane?.OutputTaskItemString(isOutputPaneVisible, VSTASKPRIORITY.TP_HIGH, VSTASKCATEGORY.CAT_BUILDCOMPILE, null, 0, null, 0, null);

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

        private IVsOutputWindowPane CreatePane()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var outputWindow = Package.GetGlobalService(typeof(SVsOutputWindow)) as IVsOutputWindow;
            var paneGuid = PackageGuids.guidGitWorkTreeOutpane;
            outputWindow.CreatePane(ref paneGuid, Vsix.Name, 1, 1);
            outputWindow.GetPane(ref paneGuid, out var pane);
            return pane;
        }
    }


}
