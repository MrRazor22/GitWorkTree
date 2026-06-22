using GitWorkTree.Services;
using System;
using System.Windows.Input;

namespace GitWorkTree.ViewModel
{
    public class RelayCommand : ICommand
    {
        private readonly string _commandName;
        private readonly Func<object, bool> _execute;
        private readonly Predicate<object> _canExecute;
        private readonly ILoggingService _loggingService;

        public event EventHandler CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        public RelayCommand(string commandName, Func<object, bool> execute, Predicate<object> canExecute = null, ILoggingService loggingService = null)
        {
            _commandName = commandName;
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
            _loggingService = loggingService;
        }

        public RelayCommand(Func<object, bool> execute, Predicate<object> canExecute = null, ILoggingService loggingService = null)
            : this("GitWorkTree Action", execute, canExecute, loggingService)
        {
        }

        public bool CanExecute(object parameter)
        {
            return _canExecute == null || _canExecute(parameter);
        }

        public void Execute(object parameter)
        {
            if (!CanExecute(parameter)) return;

            bool result = false;
            using (var session = _loggingService?.StartSession(_commandName))
            {
                try
                {
                    result = _execute(parameter);
                    if (session != null)
                    {
                        Microsoft.VisualStudio.Shell.ThreadHelper.JoinableTaskFactory.Run(() => session.CompleteAsync(result));
                    }
                }
                catch (Exception ex)
                {
                    if (session != null)
                    {
                        Microsoft.VisualStudio.Shell.ThreadHelper.JoinableTaskFactory.Run(async () =>
                        {
                            await session.LogAsync($"Command execution failed: {ex.Message}", true);
                            await session.CompleteAsync(false);
                        });
                    }
                }
            }
        }
    }
}
