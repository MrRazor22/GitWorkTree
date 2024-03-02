using EnvDTE;
using Microsoft;
using EnvDTE80;
using GitWorkTree.ViewModel;
using System.Windows.Interop;
using GitWorkTree.Helpers;
using GitWorkTree.View;

namespace GitWorkTree.Commands
{
    public enum CommandType { Create, Manage };
    internal class CommandExecutor
    {
        private CommandType _commandType;
        private string ActiveRepositoryPath;

        private DTE2 dte;
        private WorkTreeDialogViewModel dialogViewModel;
        private LoggingHelper outputWindow = LoggingHelper.Instance;

        public static General optionsSaved { get; set; }
        public static Action<General> OnSettingsSaved = general => optionsSaved = general;

        public CommandExecutor(CommandType commandType)
        {
            _commandType = commandType;
        }

        public bool PreRequisite()
        {
            try
            {
                dte = Package.GetGlobalService(typeof(DTE)) as DTE2;
                Assumes.Present(dte);

                string solutionPath = dte.Solution?.FullName;
                ActiveRepositoryPath = SolutionHelper.GetRepositoryPath(solutionPath);

                if (string.IsNullOrEmpty(ActiveRepositoryPath))
                {
                    outputWindow?.UpdateStatusBar("No Repository loaded!");
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                outputWindow?.WriteToOutputWindowAsync("Pre-requisite check failed - " + ex.Message, true);
                return false;
            }
        }

        public bool Execute()
        {
            try
            {
                ThreadHelper.ThrowIfNotOnUIThread();

                dialogViewModel = new WorkTreeDialogViewModel(ActiveRepositoryPath, _commandType, optionsSaved);
                WorkTreeDialogWindow dialog = new WorkTreeDialogWindow { DataContext = dialogViewModel };

                dialog.ShowModal();

                return true;
            }
            catch (Exception ex)
            {
                outputWindow?.WriteToOutputWindowAsync("Command execution failed - " + ex.Message, true);
                return false;
            }
        }
    }
}
