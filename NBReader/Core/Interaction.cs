using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace NBReader.Core
{
    public sealed class Interaction<TInput, TOutput> : IDisposable, ICommand
    {
        private Func<TInput, Task<TOutput>>? _handler;

        public Task<TOutput> HandleAsync(TInput input)
        {
            if (_handler == null)
            {
                throw new InvalidOperationException("Handler wasn't registered");
            }
            return _handler(input);
        }
        public IDisposable RegisterHandler(Func<TInput, Task<TOutput>> handler)
        {
            if (_handler is not null)
            {
                throw new InvalidOperationException("Handler was already registered");
            }
            _handler = handler;
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
            return this;
        }

        public void Dispose()
        {
            _handler = null;
        }

        public bool CanExecute(object? parameter)
        {
            return _handler is not null;
        }

        public void Execute(object? parameter)
        {
            HandleAsync((TInput?)parameter!);
        }
        public event EventHandler? CanExecuteChanged;
    }
}
