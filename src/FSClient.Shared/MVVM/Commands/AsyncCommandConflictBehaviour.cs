namespace FSClient.Shared.Mvvm
{
    public enum AsyncCommandConflictBehaviour
    {
        RunAndForget = 1,
        CancelPrevious,
        WaitPrevious,
        Skip
    }
}
