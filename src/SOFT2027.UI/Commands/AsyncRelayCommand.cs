using System;
using System.Threading.Tasks;
using System.Windows.Input;

namespace SOFT2027.Presentation.Commands
{
    public class AsyncRelayCommand : ICommand
    {
        private readonly Func<object?, Task> _executeAsync;
        private readonly Predicate<object?>? _canExecute;
        private readonly Action<Exception>? _onException;
        private bool _isExecuting;

        public event EventHandler? CanExecuteChanged;

        public bool IsExecuting
        {
            get => _isExecuting;
            private set
            {
                _isExecuting = value;
                RaiseCanExecuteChanged();
            }
        }

        public AsyncRelayCommand(Func<object?, Task> executeAsync, Predicate<object?>? canExecute = null, Action<Exception>? onException = null)
        {
            _executeAsync = executeAsync ?? throw new ArgumentNullException(nameof(executeAsync));
            _canExecute = canExecute;
            _onException = onException;
        }

        public AsyncRelayCommand(Func<Task> executeAsync, Func<bool>? canExecute = null, Action<Exception>? onException = null)
            : this(_ => executeAsync(), canExecute != null ? _ => canExecute() : null, onException)
        {
        }

        public bool CanExecute(object? parameter) => !_isExecuting && (_canExecute?.Invoke(parameter) ?? true);

        public async void Execute(object? parameter)
        {
            try
            {
                await ExecuteAsync(parameter);
            }
            catch (Exception ex)
            {
                _onException?.Invoke(ex);
            }
        }

        public async Task ExecuteAsync(object? parameter)
        {
            if (!CanExecute(parameter))
            {
                return;
            }

            try
            {
                IsExecuting = true;
                await _executeAsync(parameter);
            }
            catch (Exception ex)
            {
                if (_onException != null)
                {
                    _onException(ex);
                }
                else
                {
                    throw;
                }
            }
            finally
            {
                IsExecuting = false;
            }
        }

        public void RaiseCanExecuteChanged()
        {
            var handler = CanExecuteChanged;
            if (handler == null) return;

            var app = System.Windows.Application.Current;
            if (app != null && !app.Dispatcher.CheckAccess())
            {
                app.Dispatcher.BeginInvoke(() => handler.Invoke(this, EventArgs.Empty));
            }
            else
            {
                handler.Invoke(this, EventArgs.Empty);
            }
        }
    }
}
