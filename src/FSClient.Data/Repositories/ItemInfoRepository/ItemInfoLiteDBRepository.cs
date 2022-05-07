namespace FSClient.Data.Repositories
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    using FSClient.Data.Context;
    using FSClient.Shared.Models;
    using FSClient.Shared.Repositories;

    public class ItemInfoLiteDBRepository
        : BaseLiteDatabaseRepository<string, ItemInfo>, IItemInfoRepository
    {
        public ItemInfoLiteDBRepository(LiteDatabaseContext wrapper) : base(wrapper)
        {
        }

        public override ValueTask<int> DeleteManyAsync(IEnumerable<ItemInfo> items)
        {
            var itemsIds = items.Select(i => i.Key).ToArray();
            var deletedCount = Collection.DeleteMany(item => itemsIds.Contains(item.Key));
            return new ValueTask<int>(deletedCount);
        }
    }
}
