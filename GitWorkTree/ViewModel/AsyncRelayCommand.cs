using GitWorkTree.Services;
using System;
using System.Threading.Tasks;
using System.Windows.Input;

namespace GitWorkTree.ViewModel
{
    public class AsyncRelayCommand : IAsyncCommand
    {
        private readonly Func<object, Task> _execute;
        private readonly Predicate<object> _canExecute;
        private readonly ILoggingService _loggingService;
        private bool _isExecuting;

        private EventHandler _canExecuteChanged;

        public event EventHandler CanExecuteChanged
        {
            add
            {
                CommandManager.RequerySuggested += value;
                _canExecuteChanged += value;
            }
            remove
            {
                CommandManager.RequerySuggested -= value;
                _canExecuteChanged -= value;
            }
        }

        public bool IsExecuting
        {
            get => _isExecuting;
            private set
            {
                if (_isExecuting != value)
                {
                    _isExecuting = value;
                    RaiseCanExecuteChanged();
                }
            }
        }

        public AsyncRelayCommand(Func<object, Task> execute, Predicate<object> canExecute = null, ILoggingService loggingService = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
            _loggingService = loggingService;
        }

        public bool CanExecute(object parameter)
        {
            return !IsExecuting && (_canExecute == null || _canExecute(parameter));
        }

        public async Task ExecuteAsync(object parameter)
        {
            if (!CanExecute(parameter))
            {
                return;
            }

            try
            {
                IsExecuting = true;
                _loggingService?.SetCommandStatusBusy();

                await _execute(parameter);
            }
            finally
            {
                IsExecuting = false;
                _loggingService?.SetCommandStatusBusy(false);
            }
        }

        public async void Execute(object parameter)
        {
            await ExecuteAsync(parameter);
        }

        public void RaiseCanExecuteChanged()
        {
            _canExecuteChanged?.Invoke(this, EventArgs.Empty);
            CommandManager.InvalidateRequerySuggested();
        }
    }
}
