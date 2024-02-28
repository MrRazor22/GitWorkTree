using GitWorkTree.Commands;
using GitWorkTree.Helpers;

namespace GitWorkTree
{
    [Command(PackageGuids.guidGitWorkTreePackageCmdSetString, PackageIds.CreateWorkTreeCommand)]
    internal sealed class CreateWorkTree : BaseCommand<CreateWorkTree>
    {
        protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            CommandHelper commandExecution = null;
            try
            {
                commandExecution = new CommandHelper(CommandType.Add);

                if (commandExecution.PreRequisite())
                    commandExecution.Execute();
            }
            catch (Exception ex)
            {
                LoggingHelper.Instance.WriteToOutputWindowAsync(ex.Message);
            }
        }
    }
}
