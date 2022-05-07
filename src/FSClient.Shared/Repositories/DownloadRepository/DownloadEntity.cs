namespace FSClient.Shared.Repositories
{
    using System;

    public class DownloadEntity
    {
        public DownloadEntity()
        {

        }

        public DownloadEntity(
            Guid operationId,
            string? fileId,
            int? videoQuality,
            string filePath,
            ulong totalBytesToReceive,
            DateTimeOffset addTime)
        {
            OperationId = operationId;
            FileId = fileId;
            VideoQuality = videoQuality;
            FilePath = filePath;
            TotalBytesToReceive = totalBytesToReceive;
            AddTime = addTime;
        }

        public Guid OperationId { get; set; }

        public string? FileId { get; set; }

        public int? VideoQuality { get; set; }

        public string? FilePath { get; set; }

        public ulong TotalBytesToReceive { get; set; }

        public DateTimeOffset AddTime { get; set; }
    }
}
