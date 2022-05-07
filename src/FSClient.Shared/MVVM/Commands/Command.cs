namespace FSClient.Shared.Mvvm
{
    using System;
    using System.Diagnostics;

    using FSClient.Shared.Services;

    [DebuggerStepThrough]
    public class Command : CommandBase
    {
        private readonly Func<bool>? canExecute;
        private readonly Action execute;

        public Command(Action execute, Func<bool>? canexecute = null)
        {
            this.execute = execute ?? throw new ArgumentNullException(nameof(execute));
            canExecute = canexecute;
        }

        public override bool CanExecute(object? p = null)
        {
            return canExecute?.Invoke() ?? true;
        }

        public override void Execute(object? p = null)
        {
            if (!CanExecute(p))
            {
                return;
            }

            try
            {
                execute();
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
