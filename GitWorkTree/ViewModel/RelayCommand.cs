using GitWorkTree.Services;
using System;
using System.Windows.Input;

namespace GitWorkTree.ViewModel
{
    public class RelayCommand : ICommand
    {
        private readonly Func<object, bool> _execute;
        private readonly Predicate<object> _canExecute;
        private readonly ILoggingService _loggingService;

        public event EventHandler CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        public RelayCommand(Func<object, bool> execute, Predicate<object> canExecute = null, ILoggingService loggingService = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
            _loggingService = loggingService;
        }

        public bool CanExecute(object parameter)
        {
            return _canExecute == null || _canExecute(parameter);
        }

        public void Execute(object parameter)
        {
            if (!CanExecute(parameter)) return;

            bool result = false;
            try
            {
                _loggingService?.SetCommandStatusBusy();
                result = _execute(parameter);
            }
            catch (Exception ex)
            {
                _loggingService?.WriteToOutputWindowAsync($"{Vsix.Name} Command execution failed: {ex.Message}");
            }
            finally
            {
                _loggingService?.SetCommandCompletionStatus(result);
            }
        }
    }
}
