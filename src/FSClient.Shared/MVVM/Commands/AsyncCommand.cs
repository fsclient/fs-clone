namespace FSClient.Shared.Mvvm
{
    using System;
    using System.Runtime.CompilerServices;
    using System.Threading;
    using System.Threading.Tasks;

    public class AsyncCommand : AsyncCommand<object>
    {
        public AsyncCommand(
            Func<CancellationToken, Task> executeCommand,
            AsyncCommandConflictBehaviour behaviour = AsyncCommandConflictBehaviour.RunAndForget,
            [CallerMemberName] string memberName = null!,
            [CallerFilePath] string filePath = null!,
            [CallerLineNumber] int lineNumber = 0)
            : this(executeCommand, null, behaviour, memberName, filePath, lineNumber)
        {

        }

        public AsyncCommand(
            Func<CancellationToken, Task> executeCommand, Func<bool>? canExecuteCommand,
            AsyncCommandConflictBehaviour behaviour = AsyncCommandConflictBehaviour.RunAndForget,
            [CallerMemberName] string memberName = null!,
            [CallerFilePath] string filePath = null!,
            [CallerLineNumber] int lineNumber = 0)
            : base((p, ct) => executeCommand(ct),
                  canExecuteCommand == null
                      ? null
                      : new Func<object, bool>(p => canExecuteCommand()),
                  behaviour, memberName, filePath, lineNumber)
        {
        }

        public Task ExecuteAsync(CancellationToken cancellationToken = default)
        {
            return ExecuteAsync(default!, cancellationToken);
        }
    }
}
