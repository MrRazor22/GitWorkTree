using GitWorkTree.Helpers;
using System;
using System.Threading.Tasks;
using System.Windows.Input;

namespace GitWorkTree.ViewModel
{
    public class RelayCommand : ICommand
    {
        private readonly Func<object, bool> _execute;
        private readonly Func<object, Task<bool>> _executeAsync;
        private readonly Predicate<object> _canExecute;
        private static bool _isExecuting;
        private LoggingHelper outputWindow = LoggingHelper.Instance;

        // Event to handle command result
        public event EventHandler<bool> CommandExecuted;

        // Constructor for synchronous command
        public RelayCommand(Func<object, bool> execute, Predicate<object> canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        // Constructor for asynchronous command
        public RelayCommand(Func<object, Task<bool>> executeAsync, Predicate<object> canExecute = null)
        {
            _executeAsync = executeAsync ?? throw new ArgumentNullException(nameof(executeAsync));
            _canExecute = canExecute;
        }

        public event EventHandler CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        public bool CanExecute(object parameter)
        {
            if (!_isExecuting && (_canExecute == null || _canExecute(parameter))) return true;
            outputWindow.SetCommandStatusBusy();
            return false;
        }

        public async void Execute(object parameter)
        {
            if (!CanExecute(parameter))
            {
                return;
            }
            bool result = false;
            try
            {
                _isExecuting = true;

                if (_execute != null)
                {
                    result = _execute(parameter);
                }
                else if (_executeAsync != null)
                {
                    result = await _executeAsync(parameter);
                }

                // Raise the CommandExecuted event with the result
                CommandExecuted?.Invoke(this, result);
            }
            catch (Exception ex)
            {
                outputWindow.WriteToOutputWindowAsync($"{Vsix.Name} Command execution failed: {ex.Message}", result = false);
            }
            finally
            {
                outputWindow.SetCommandCompletionStatus(result);
                _isExecuting = false;
                CommandManager.InvalidateRequerySuggested();
            }
        }


    }
}
