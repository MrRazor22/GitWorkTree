using GitWorkTree.Commands;

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
                commandExecution = new CommandHelper(Package, CommandType.Add);

                if (!commandExecution.PreRequisite()) return;
                if (!commandExecution.GetDataRequired()) return;
                if (await commandExecution.RunGitCommandAsync())
                {
                    if (CommandHelper.optionsSaved.IsLoadSolution)
                        await commandExecution.CloseAndOpenSolutionAsync();
                }

            }
            catch (Exception ex)
            {
                commandExecution.outputWindow?.WriteToOutputWindowAsync(ex.Message);
            }
        }
    }
}
