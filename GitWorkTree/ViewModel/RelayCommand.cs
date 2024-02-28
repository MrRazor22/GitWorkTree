using EnvDTE;
using EnvDTE80;
using GitWorkTree.Helpers;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.Windows.Input;

namespace GitWorkTree.ViewModel
{
    public class RelayCommand : ICommand
    {
        private readonly Action<object> _execute;
        private readonly Func<object, Task> _executeAsync;
        private readonly Predicate<object> _canExecute;
        private bool _isExecuting;
        private LoggingHelper outputWindow = LoggingHelper.Instance;

        // Constructor for synchronous command
        public RelayCommand(Action<object> execute, Predicate<object> canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        // Constructor for asynchronous command
        public RelayCommand(Func<object, Task> executeAsync, Predicate<object> canExecute = null)
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
            var busyMessage = "Another command is in progress...";
            if (!_isExecuting && (_canExecute == null || _canExecute(parameter)))
            {
                outputWindow.UpdateStatusBar("", busyMessage);
                return true;
            }
            else outputWindow.UpdateStatusBar(busyMessage);
            return false;
        }

        public async void Execute(object parameter)
        {
            if (!CanExecute(parameter))
            {
                return;
            }

            try
            {
                _isExecuting = true;

                if (_execute != null)
                {
                    _execute(parameter);
                }
                else if (_executeAsync != null)
                {
                    await _executeAsync(parameter);
                }
            }
            catch (Exception ex)
            {
                outputWindow.WriteToOutputWindowAsync($"Command execution failed: {ex.Message}", true);
            }
            finally
            {
                _isExecuting = false;
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }
}
