namespace FSClient.Shared.Repositories
{
    using System;
    using System.Collections.Generic;

    public interface IDownloadRepository : IRepository<Guid, DownloadEntity>
    {
        IReadOnlyCollection<DownloadEntity> GetAllByFileId(string fileId);
    }
}
