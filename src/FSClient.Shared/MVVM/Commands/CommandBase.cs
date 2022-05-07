namespace FSClient.Shared.Mvvm
{
    using System;
    using System.Windows.Input;

    public abstract class CommandBase : BindableBase, ICommand
    {
        private readonly ContextSafeEvent<EventHandler> canExecuteChangedEvent;

        protected CommandBase()
        {
            canExecuteChangedEvent = new ContextSafeEvent<EventHandler>();
        }

        public event EventHandler CanExecuteChanged
        {
            add => canExecuteChangedEvent.Register(value);
            remove => canExecuteChangedEvent.Unregister(value);
        }

        public abstract bool CanExecute(object? parameter = null);

        public abstract void Execute(object? parameter = null);

        public void RaiseCanExecuteChanged()
        {
            canExecuteChangedEvent.Invoke(handler => handler.Invoke(this, EventArgs.Empty));
        }
    }
}
