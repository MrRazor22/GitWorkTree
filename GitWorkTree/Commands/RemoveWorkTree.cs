using GitWorkTree.Commands;

namespace GitWorkTree
{
    [Command(PackageGuids.guidGitWorkTreePackageCmdSetString, PackageIds.RemoveWorkTreeCommand)]
    internal sealed class RemoveWorkTree : BaseCommand<RemoveWorkTree>
    {
        protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            CommandHelper commandExecution = null;
            try
            {
                commandExecution = new CommandHelper(Package, CommandType.Remove);

                if (!commandExecution.PreRequisite()) return;
                if (!commandExecution.GetDataRequired()) return;

                await commandExecution.RunGitCommandAsync();

            }
            catch (Exception ex)
            {
                commandExecution.outputWindow?.WriteToOutputWindowAsync(ex.Message);
            }
        }
    }
}
