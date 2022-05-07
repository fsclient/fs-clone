namespace FSClient.Shared.Repositories
{
    using System;
    using System.Collections.Generic;
    using System.Linq.Expressions;
    using System.Threading.Tasks;

    public interface IRepository<TKey, T>
        where T : class
    {
        IAsyncEnumerable<T> GetAll();
        ValueTask<T?> GetAsync(TKey id);
        ValueTask<T?> FindAsync(Expression<Func<T, bool>> predicate);
        ValueTask<int> UpsertManyAsync(IEnumerable<T> items);
        ValueTask<int> DeleteManyAsync(IEnumerable<T> items);
        ValueTask<bool> DeleteAsync(TKey id);
    }
}
