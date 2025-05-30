﻿using GitWorkTree.Commands;
using GitWorkTree.Helpers;

namespace GitWorkTree
{
    [Command(PackageGuids.guidGitWorkTreePackageCmdSetString, PackageIds.ManageWorkTreesCommand)]
    internal sealed class ManageWorkTrees : BaseCommand<ManageWorkTrees>
    {
        protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            CommandExecutor commandExecution = null;
            try
            {
                commandExecution = new CommandExecutor(CommandType.Manage);

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
