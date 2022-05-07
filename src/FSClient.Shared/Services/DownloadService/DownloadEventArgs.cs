namespace FSClient.Shared.Services
{
    using System;

    public class DownloadEventArgs : EventArgs
    {
        public DownloadEventArgs(DownloadFile downloadFile,
            DownloadStatus status)
        {
            File = downloadFile;
            Status = status;
        }

        public DownloadFile File { get; }

        public DownloadStatus Status { get; }
    }
}
