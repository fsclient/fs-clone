namespace FSClient.Shared.Mvvm
{
    using System;
    using System.Diagnostics;

    using FSClient.Shared.Services;

    [DebuggerStepThrough]
    public class Command<T> : CommandBase
    {
        private readonly Func<T, bool>? canExecute;
        private readonly Action<T> execute;

        public Command(Action<T> execute, Func<T, bool>? canexecute = null)
        {
            this.execute = execute ?? throw new ArgumentNullException(nameof(execute));
            canExecute = canexecute;
        }

        public override bool CanExecute(object? p = null)
        {
            return canExecute == null
                || (p is T param ? canExecute(param) : p == null && canExecute(default!));
        }

        public override void Execute(object? p = null)
        {
            if (!CanExecute(p))
            {
                return;
            }

            try
            {
                execute(p is T parameter ? parameter : default!);
            }
            catch (OperationCanceledException)
            {
                // Ignore cancelation.
            }
            catch (Exception ex)
            {
                // Top level exceptions in Command shouldn't crash application, but should be logged.
                Logger.Instance?.LogError(ex);
            }
        }
    }
}
