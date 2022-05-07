namespace FSClient.Data.Repositories
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    using FSClient.Data.Context;
    using FSClient.Shared.Repositories;

    /// <inheritdoc/>
    public class DownloadRepository
        : BaseLiteDatabaseRepository<Guid, DownloadEntity>, IDownloadRepository
    {
        public DownloadRepository(LiteDatabaseContext wrapper) : base(wrapper)
        {
        }

        public override ValueTask<int> DeleteManyAsync(IEnumerable<DownloadEntity> items)
        {
            var itemsIds = items.Select(i => i.OperationId).ToArray();
            var deletedCount = Collection.DeleteMany(e => itemsIds.Contains(e.OperationId));
            return new ValueTask<int>(deletedCount);
        }

        public IReadOnlyCollection<DownloadEntity> GetAllByFileId(string fileId)
        {
            return Collection.Find(entity => entity.FileId == fileId).ToList();
        }
    }
}
