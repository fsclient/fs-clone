namespace FSClient.Shared.Services
{
    public enum DownloadResult
    {
        Unknown = 0,
        Completed = 1,
        InProgress,
        Canceled,
        NotSupported,
        NotSupportedMultiSource,
        NotSupportedHls,
        NotSupportedMagnet,
        FailedFolderOpen,
        FailedFileCreate,
        FailedUnknown
    }
}
