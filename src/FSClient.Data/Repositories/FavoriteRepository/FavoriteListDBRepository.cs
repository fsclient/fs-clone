namespace FSClient.Data.Repositories
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    using FSClient.Data.Context;
    using FSClient.Shared.Helpers;
    using FSClient.Shared.Models;
    using FSClient.Shared.Providers;
    using FSClient.Shared.Repositories;
    using FSClient.Shared.Services;

    using LiteDB;

    using Microsoft.Extensions.Logging;

    using Nito.AsyncEx;

    public class FavoriteListDBRepository : BaseLiteDatabaseRepository<string, FavoriteItem>, IFavoriteRepository
    {
        private readonly AsyncLazy<bool> migrateFavoritesTask;
        private readonly IItemInfoRepository itemInfoRepository;
        private readonly ILogger logger;

        public FavoriteListDBRepository(
            LiteDatabaseContext liteDatabaseWrapper,
            IItemInfoRepository itemInfoRepository,
            ISettingService settingService,
            ILogger logger)
            : base(liteDatabaseWrapper)
        {
            this.itemInfoRepository = itemInfoRepository;
            this.logger = logger;

            migrateFavoritesTask = new AsyncLazy<bool>(() => MigrateFavoritesAsync(new LocalSettingFavoriteRepository(settingService)));
        }

        public override async ValueTask<bool> DeleteAsync(string id)
        {
            await migrateFavoritesTask.ConfigureAwait(false);

            return await base.DeleteAsync(id);
        }

        public override async ValueTask<int> DeleteManyAsync(IEnumerable<FavoriteItem> items)
        {
            await migrateFavoritesTask.ConfigureAwait(false);

            var itemsIds = items.Select(f => f.Key).ToArray();
            return Collection.DeleteMany(item => itemsIds.Contains(item.Key));
        }

        public override async ValueTask<FavoriteItem?> GetAsync(string id)
        {
            await migrateFavoritesTask.ConfigureAwait(false);

            var result = Collection.Include(f => f.ItemInfo).FindById(id);
            return result;
        }

        public IAsyncEnumerable<FavoriteItem> GetAllByFavoriteListKind(FavoriteListKind listKind)
        {
            return migrateFavoritesTask.Task.ToEmptyAsyncEnumerable<FavoriteItem>()
                .Concat(Collection.Include(f => f.ItemInfo)
                .Find(f => f.ListKind == listKind)
                .ToArray()
                .ToAsyncEnumerable());
        }

        public IAsyncEnumerable<FavoriteItem> GetFavoritesByItems(IEnumerable<string> itemKeys)
        {
            var itemKeysArray = itemKeys.ToArray();

            return migrateFavoritesTask.Task.ToEmptyAsyncEnumerable<FavoriteItem>()
                .Concat(Collection.Include(f => f.ItemInfo)
                .Find(f => itemKeysArray.Contains(f.ItemInfo.Key))
                .ToArray()
                .ToAsyncEnumerable());
        }

        private async Task<bool> MigrateFavoritesAsync(LocalSettingFavoriteRepository localSettingFavoriteRepository)
        {
            if (Collection.Count() > 0)
            {
                return true;
            }

            try
            {
                var favorites = localSettingFavoriteRepository.cacheLazy.Value;
                var items = favorites.SelectMany(t => t.Value);

                await itemInfoRepository.UpsertManyAsync(items.Select(h => h.ItemInfo).Distinct()).ConfigureAwait(false);
                await base.UpsertManyAsync(items).ConfigureAwait(false);

                Database.Checkpoint();

                return true;
            }
            catch (Exception ex)
            {
                logger.LogError(ex);
                return false;
            }
        }
    }
}
