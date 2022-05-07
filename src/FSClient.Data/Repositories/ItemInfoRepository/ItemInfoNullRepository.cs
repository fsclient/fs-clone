namespace FSClient.Data.Repositories
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Threading.Tasks;

    using FSClient.Shared.Models;
    using FSClient.Shared.Repositories;

    public class ItemInfoNullRepository : IItemInfoRepository
    {
        public ValueTask<bool> DeleteAsync(string id)
        {
            return default;
        }

        public ValueTask<int> DeleteManyAsync(IEnumerable<ItemInfo> items)
        {
            return default;
        }

        public ValueTask<ItemInfo?> FindAsync(Expression<Func<ItemInfo, bool>> predicate)
        {
            return default;
        }

        public IAsyncEnumerable<ItemInfo> GetAll()
        {
            return AsyncEnumerable.Empty<ItemInfo>();
        }

        public ValueTask<ItemInfo?> GetAsync(string id)
        {
            return default;
        }

        public ValueTask<int> UpsertManyAsync(IEnumerable<ItemInfo> items)
        {
            return default;
        }
    }
}
