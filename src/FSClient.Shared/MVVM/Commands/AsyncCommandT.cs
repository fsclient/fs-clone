namespace FSClient.Shared.Mvvm
{
    using System;
    using System.Diagnostics.CodeAnalysis;
    using System.Runtime.CompilerServices;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Windows.Input;

    using FSClient.Shared.Helpers;
    using FSClient.Shared.Services;

    public class AsyncCommand<T> : CommandBase, ICancelableCommand, IAsyncCommand
    {
        protected readonly Func<T, bool>? canExecute;
        protected readonly Func<T, CancellationToken, Task> execute;

        private readonly SemaphoreSlim? waitPreviousSemaphore;
        private readonly (string memberName, string filePath, int lineNumber) callerMember;
        private CancellationTokenSource? previousCts;

        public AsyncCommand(
            Func<T, CancellationToken, Task> executeCommand,
            AsyncCommandConflictBehaviour behaviour = AsyncCommandConflictBehaviour.RunAndForget,
            [CallerMemberName] string memberName = null!,
            [CallerFilePath] string filePath = null!,
            [CallerLineNumber] int lineNumber = 0)
            : this(executeCommand, null, behaviour, memberName, filePath, lineNumber)
        {

        }

        public AsyncCommand(
            Func<T, CancellationToken, Task> executeCommand,
            Func<T, bool>? canExecuteCommand,
            AsyncCommandConflictBehaviour behaviour = AsyncCommandConflictBehaviour.RunAndForget,
            [CallerMemberName] string memberName = null!,
            [CallerFilePath] string filePath = null!,
            [CallerLineNumber] int lineNumber = 0)
        {
            execute = executeCommand ?? throw new ArgumentNullException(nameof(executeCommand));
            canExecute = canExecuteCommand;
            callerMember = (memberName, filePath, lineNumber);

            Behaviour = behaviour;
            if (behaviour == AsyncCommandConflictBehaviour.WaitPrevious)
            {
                waitPreviousSemaphore = new SemaphoreSlim(1);
            }

            CancelCommand = new Command(Cancel);
        }

        public ICommand CancelCommand { get; }

        public AsyncCommandConflictBehaviour Behaviour { get; }

        public bool IsExecuting { get; private set; }

        public bool IsCanExecuteWithoutParameter => canExecute?.Invoke(default!) ?? true;

        public void Cancel()
        {
            previousCts?.Cancel();
        }

        public override bool CanExecute(object? parameter = default)
        {
            if ((Behaviour == AsyncCommandConflictBehaviour.Skip
                || Behaviour == AsyncCommandConflictBehaviour.WaitPrevious)
                && IsExecuting)
            {
                return false;
            }

            return canExecute == null || (TryGetParameter<T>(parameter, out var param) && canExecute(param!));
        }

        public override async void Execute(object? parameter = default)
        {
            await ((IAsyncCommand)this).ExecuteAsync(parameter, CancellationToken.None).ConfigureAwait(true);
        }

        Task IAsyncCommand.ExecuteAsync(object? parameter, CancellationToken cancellationToken)
        {
            if (!TryGetParameter<T>(parameter, out var validParam))
            {
                if (Logger.Initialized)
                {
                    Logger.Instance.LogError(new InvalidCastException($"Can't cast {parameter?.GetType().Name} to {typeof(T).Name} in ({callerMember.memberName} {callerMember.filePath} {callerMember.lineNumber})'s command"));
                }
                validParam = default;
            }
            return ExecuteAsync(validParam!, cancellationToken);
        }

        public async Task ExecuteAsync(
            T param,
            CancellationToken cancellationToken = default)
        {
            if (!CanExecute(param))
            {
                return;
            }

            var localCts = new CancellationTokenSource();
            using (var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            {
                var token = linkedCts.Token;
                switch (Behaviour)
                {
                    case AsyncCommandConflictBehaviour.Skip
                    when IsExecuting:
                        return;
                    case AsyncCommandConflictBehaviour.CancelPrevious:
                        previousCts?.Cancel();
                        break;
                    case AsyncCommandConflictBehaviour.WaitPrevious:
                        await waitPreviousSemaphore!.WaitAsync(token).ConfigureAwait(false);
                        break;
                    case AsyncCommandConflictBehaviour.RunAndForget:
                        break;
                    case AsyncCommandConflictBehaviour.Skip:
                        break;
                }

                previousCts = localCts;

                try
                {
                    IsExecuting = true;
                    RaiseCanExecuteChanged();
                    OnPropertyChanged(nameof(IsExecuting));
                    await execute(param, token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    // Ignore cancelation.
                }
                catch (Exception ex) when (Logger.Initialized)
                {
                    ex.Data["CommandCallerMemberName"] = $"{callerMember.memberName} {callerMember.filePath} {callerMember.lineNumber}";
                    Logger.Instance.LogError(ex);
                }
                finally
                {
                    if (Behaviour == AsyncCommandConflictBehaviour.WaitPrevious)
                    {
                        waitPreviousSemaphore!.Release();
                    }

                    IsExecuting = false;
                    previousCts = null;
                    OnPropertyChanged(nameof(IsExecuting));
                    RaiseCanExecuteChanged();
                }
            }
        }

        private static bool TryGetParameter<TParam>(object? parameter, [MaybeNull] out TParam value)
        {
            if (parameter is TParam exactNotNullType)
            {
                value = exactNotNullType;
                return true;
            }
            if (parameter == null)
            {
                value = default;
                return true;
            }

            if (ObjectHelper.TrySafeCast<TParam>(parameter, out var casted))
            {
                value = casted;
                return true;
            }

            value = default;
            return false;
        }
    }
}
