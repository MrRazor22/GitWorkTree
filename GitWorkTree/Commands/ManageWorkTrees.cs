using GitWorkTree.Commands;
using GitWorkTree.Services;

namespace GitWorkTree
{
    [Command(PackageGuids.guidGitWorkTreePackageCmdSetString, PackageIds.ManageWorkTreesCommand)]
    internal sealed class ManageWorkTrees : BaseCommand<ManageWorkTrees>
    {
        protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            try
            {
                ToolWindowPane window = await Package.FindToolWindowAsync(
                    typeof(GitWorkTree.View.ManageWorktreesToolWindow),
                    0,
                    create: true,
                    cancellationToken: Package.DisposalToken);

                if (window?.Frame is Microsoft.VisualStudio.Shell.Interop.IVsWindowFrame frame)
                {
                    Microsoft.VisualStudio.ErrorHandler.ThrowOnFailure(frame.Show());
                }
            }
            catch (Exception ex)
            {
                LoggingHelper.Instance.WriteToOutputWindowAsync(ex.Message);
            }
        }
    }
}
