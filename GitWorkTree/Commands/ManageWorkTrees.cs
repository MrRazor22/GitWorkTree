using GitWorkTree.Commands;

namespace GitWorkTree
{
    [Command(PackageGuids.guidGitWorkTreePackageCmdSetString, PackageIds.ManageWorkTreesCommand)]
    internal sealed class ManageWorkTrees : BaseCommand<ManageWorkTrees>
    {
        protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            CommandHelper commandExecution = null;
            try
            {
                commandExecution = new CommandHelper(Package, CommandType.Manage);

                if (!commandExecution.PreRequisite()) return;
                if (!commandExecution.GetDataRequired()) return;
            }
            catch (Exception ex)
            {
                commandExecution.outputWindow?.WriteToOutputWindowAsync(ex.Message);
            }
        }
    }
}
