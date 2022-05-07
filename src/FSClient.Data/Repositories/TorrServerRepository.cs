namespace FSClient.Data.Repositories
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    using FSClient.Data.Context;
    using FSClient.Shared.Repositories;

    public class TorrServerRepository
        : BaseLiteDatabaseRepository<string, TorrServerEntity>, ITorrServerRepository
    {
        public TorrServerRepository(LiteDatabaseContext wrapper) : base(wrapper)
        {
        }

        public override ValueTask<int> DeleteManyAsync(IEnumerable<TorrServerEntity> items)
        {
            var itemsIds = items.Select(i => i.TorrServerHash).ToArray();
            var deletedCount = Collection.DeleteMany(e => itemsIds.Contains(e.TorrServerHash));
            return new ValueTask<int>(deletedCount);
        }
    }
}
