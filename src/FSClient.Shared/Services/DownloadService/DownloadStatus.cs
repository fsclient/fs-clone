namespace FSClient.Shared.Services
{
    public enum DownloadStatus
    {
        Unknown,
        Idle = 1,
        Paused,
        Resuming,
        Running,
        Completed,
        NoNetwork,
        Error,
        FileMissed,
        Canceled
    }
}
