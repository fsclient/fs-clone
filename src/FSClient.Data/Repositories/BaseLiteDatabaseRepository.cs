namespace FSClient.Data.Repositories
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Threading.Tasks;

    using FSClient.Data.Context;
    using FSClient.Shared.Repositories;

    using LiteDB;

    public abstract class BaseLiteDatabaseRepository<TKey, TEntity> : IRepository<TKey, TEntity>
        where TEntity : class
    {
        protected BaseLiteDatabaseRepository(LiteDatabaseContext liteDatabaseWrapper)
        {
            Database = liteDatabaseWrapper.Database;
            Collection = liteDatabaseWrapper.Database.GetCollection<TEntity>();
        }

        protected ILiteDatabase Database { get; }

        protected ILiteCollection<TEntity> Collection { get; }

        public abstract ValueTask<int> DeleteManyAsync(IEnumerable<TEntity> items);

        public virtual ValueTask<bool> DeleteAsync(TKey id)
        {
            return new ValueTask<bool>(Collection.Delete(new BsonValue(id)));
        }

        public virtual ValueTask<TEntity?> GetAsync(TKey id)
        {
            var result = Collection.FindById(new BsonValue(id));
            return new ValueTask<TEntity?>(result);
        }

        public virtual ValueTask<TEntity?> FindAsync(Expression<Func<TEntity, bool>> predicate)
        {
            var founded = Collection.FindOne(predicate);
            return new ValueTask<TEntity?>(founded);
        }

        public virtual IAsyncEnumerable<TEntity> GetAll()
        {
            var result = Collection.FindAll().ToList();
            return result.ToAsyncEnumerable();
        }

        public virtual ValueTask<int> UpsertManyAsync(IEnumerable<TEntity> items)
        {
            var upsertedCount = Collection.Upsert(items);
            return new ValueTask<int>(upsertedCount);
        }
    }
}
