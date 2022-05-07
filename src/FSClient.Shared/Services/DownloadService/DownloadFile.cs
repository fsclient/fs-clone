namespace FSClient.Shared.Services
{
    using System;

    public class DownloadFile : IEquatable<DownloadFile>
    {
        public DownloadFile(Guid operationId, IStorageFile file, DateTimeOffset addTime)
            : this(operationId, file.Title, addTime)
        {
            File = file;
        }

        public DownloadFile(Guid operationId, string fileName, DateTimeOffset addTime)
        {
            OperationId = operationId;
            FileName = fileName;
            AddTime = addTime;
        }

        public IStorageFile? File { get; }

        public DateTimeOffset AddTime { get; }

        public string FileName { get; }

        public Guid OperationId { get; }

        public ulong TotalBytesToReceive { get; set; }

        public ulong BytesReceived { get; set; }

        public DownloadStatus Status { get; set; }

        public virtual bool PauseSupported => true;

        public bool Equals(DownloadFile other)
        {
            return other.OperationId == OperationId;
        }

        public override bool Equals(object obj)
        {
            return obj is DownloadFile file && Equals(file);
        }

        public override int GetHashCode()
        {
            return OperationId.GetHashCode();
        }

        public override string ToString()
        {
            return $"{FileName} - {OperationId}";
        }
    }
}
