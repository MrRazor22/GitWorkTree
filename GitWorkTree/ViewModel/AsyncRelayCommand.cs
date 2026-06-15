using GitWorkTree.Services;
using System;
using System.Threading.Tasks;
using System.Windows.Input;

namespace GitWorkTree.ViewModel
{
    public class AsyncRelayCommand : IAsyncCommand
    {
        private readonly string _commandName;
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

        public AsyncRelayCommand(string commandName, Func<object, Task> execute, Predicate<object> canExecute = null, ILoggingService loggingService = null)
        {
            _commandName = commandName;
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
            _loggingService = loggingService;
        }

        public AsyncRelayCommand(Func<object, Task> execute, Predicate<object> canExecute = null, ILoggingService loggingService = null)
            : this("GitWorkTree Action", execute, canExecute, loggingService)
        {
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

            bool result = false;
            using (var session = _loggingService?.StartSession(_commandName))
            {
                try
                {
                    IsExecuting = true;
                    await _execute(parameter);
                    result = true;
                    if (session != null)
                    {
                        await session.CompleteAsync(true);
                    }
                }
                catch (Exception ex)
                {
                    if (session != null)
                    {
                        await session.LogAsync($"Command execution failed: {ex.Message}", true);
                        await session.CompleteAsync(false);
                    }
                    throw;
                }
                finally
                {
                    IsExecuting = false;
                }
            }
        }

        public async void Execute(object parameter)
        {
            try
            {
                await ExecuteAsync(parameter);
            }
            catch (Exception)
            {
                // Already logged in ExecuteAsync, swallow to prevent crashing the host environment
            }
        }

        public void RaiseCanExecuteChanged()
        {
            _canExecuteChanged?.Invoke(this, EventArgs.Empty);
            CommandManager.InvalidateRequerySuggested();
        }
    }
}
