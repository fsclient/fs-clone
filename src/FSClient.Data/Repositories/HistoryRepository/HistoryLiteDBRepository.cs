namespace FSClient.Data.Repositories
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Threading.Tasks;

    using FSClient.Data.Context;
    using FSClient.Shared.Helpers;
    using FSClient.Shared.Models;
    using FSClient.Shared.Repositories;
    using FSClient.Shared.Services;

    using LiteDB;

    using Microsoft.Extensions.Logging;

    using Nito.AsyncEx;

    public class HistoryLiteDBRepository
        : BaseLiteDatabaseRepository<string, HistoryItem>, IHistoryRepository
    {
        private readonly ISettingService settingService;
        private readonly IItemInfoRepository itemInfoRepository;
        private readonly AsyncLazy<bool> migrateHistoryTask;
        private readonly ILogger logger;

        public HistoryLiteDBRepository(
            IStorageService storageService,
            ISettingService settingService,
            IItemInfoRepository itemInfoRepository,
            ILogger logger,
            LiteDatabaseContext liteDatabaseWrapper) : base(liteDatabaseWrapper)
        {
            this.settingService = settingService;
            this.itemInfoRepository = itemInfoRepository;
            this.logger = logger;

            migrateHistoryTask = new AsyncLazy<bool>(() => MigrateHistoryAsync(new HistoryJsonRepository(storageService, settingService, logger)));
        }

        public override async ValueTask<bool> DeleteAsync(string id)
        {
            await migrateHistoryTask.ConfigureAwait(false);

            return await base.DeleteAsync(id);
        }

        public override async ValueTask<int> DeleteManyAsync(IEnumerable<HistoryItem> items)
        {
            await migrateHistoryTask.ConfigureAwait(false);

            SaveLegacyPosition(items);

            var itemsIds = items.Select(i => i.Key).ToArray();
            return Collection.DeleteMany(item => itemsIds.Contains(item.Key));
        }

        public override async ValueTask<HistoryItem?> GetAsync(string id)
        {
            await migrateHistoryTask.ConfigureAwait(false);

            var result = Collection.Include(h => h.ItemInfo).FindById(id);
            return result;
        }

        public override async ValueTask<HistoryItem?> FindAsync(Expression<Func<HistoryItem, bool>> predicate)
        {
            await migrateHistoryTask.ConfigureAwait(false);

            var founded = Collection.Include(h => h.ItemInfo).FindOne(predicate);
            return founded;
        }

        public override IAsyncEnumerable<HistoryItem> GetAll()
        {
            return migrateHistoryTask.Task
                .ToEmptyAsyncEnumerable<HistoryItem>()
                .Concat(Collection.Include(h => h.ItemInfo)
                .FindAll()
                .ToList()
                .ToAsyncEnumerable());
        }

        public async IAsyncEnumerable<HistoryItem> GetOrderedHistory(string? itemInfoKey = null, int? season = null, Range? episode = null)
        {
            var query = Query.All(nameof(HistoryItem.AddTime), Query.Descending);

            if (itemInfoKey != null)
            {
                query.Where.Add(
                    Query.EQ($"$.{nameof(HistoryItem.ItemInfo)}.$id", new BsonValue(itemInfoKey)));
            }
            if (season.HasValue)
            {
                query.Where.Add(
                    Query.EQ($"{nameof(HistoryItem.Season)}", new BsonValue(season.Value)));
            }
            if (episode.HasValue)
            {
                query.Where.Add(
                    Query.EQ($"{nameof(HistoryItem.Episode)}", Database.Mapper.Serialize(episode.Value)));
            }

            await migrateHistoryTask.Task.ConfigureAwait(false);

            foreach (var item in Collection.Include(i => i.ItemInfo).Find(query).ToArray())
            {
                yield return item;
            }
        }

        public override async ValueTask<int> UpsertManyAsync(IEnumerable<HistoryItem> items)
        {
            await migrateHistoryTask.ConfigureAwait(false);

            SaveLegacyPosition(items);

            await itemInfoRepository.UpsertManyAsync(items.Select(h => h.ItemInfo).Distinct()).ConfigureAwait(false);
            return await base.UpsertManyAsync(items).ConfigureAwait(false);
        }

        public ValueTask<float> GetNodePositionById(string id)
        {
            var position = Collection.FindOne(i => i.Node != null && i.Node.Key == id)?.Node?.Position
                ?? settingService.GetSetting(settingService.RootContainer, id, (float?)null, SettingStrategy.Local)
                ?? settingService.GetSetting(settingService.RootContainer, $"f{id}", (float?)null, SettingStrategy.Local)
                ?? settingService.GetSetting(settingService.RootContainer, id, (float?)null, SettingStrategy.Roaming)
                ?? settingService.GetSetting(settingService.RootContainer, $"f{id}", (float?)null, SettingStrategy.Roaming);

            return new ValueTask<float>(position ?? 0);
        }

        private void SaveLegacyPosition(IEnumerable<HistoryItem> historyItems)
        {
            var allSettings = (settingService
                .GetAllRawSettings(settingService.RootContainer, SettingStrategy.Local))
                .Concat(settingService
                .GetAllRawSettings(settingService.RootContainer, SettingStrategy.Roaming));

            foreach (var historyItem in historyItems)
            {
                if (historyItem.Node == null)
                {
                    continue;
                }

                var specialIds = new[] { historyItem.ItemInfo.Site.Value, Site.Any.Value, Site.All.Value };
                var historyNodes = historyItem.Node.Flatten()
                    .Where(node => Array.IndexOf(specialIds, node.Key) < 0);

                foreach (var node in historyNodes)
                {
                    if (node.Position < float.Epsilon)
                    {
                        var pairsToDelete = allSettings
                            .Where(pair => pair.Key.StartsWith(node.Key, StringComparison.OrdinalIgnoreCase)
                            || pair.Key.StartsWith($"f{node.Key}", StringComparison.OrdinalIgnoreCase));
                        foreach (var pairToDelete in pairsToDelete)
                        {
                            settingService.DeleteSetting(settingService.RootContainer, pairToDelete.Key, SettingStrategy.Local);
                            settingService.DeleteSetting(settingService.RootContainer, pairToDelete.Key, SettingStrategy.Roaming);
                        }
                    }
                    else
                    {
                        settingService.SetSetting(settingService.RootContainer, node.Key, node.Position, SettingStrategy.Local);
                    }
                }
            }
        }

        private async Task<bool> MigrateHistoryAsync(HistoryJsonRepository historyJsonRepository)
        {
            if (Collection.Count() > 0)
            {
                return true;
            }

            try
            {
                var history = await historyJsonRepository.historyAsyncLazy.ConfigureAwait(false);
                var items = history.Select(t => t.Value.item);

                await itemInfoRepository.UpsertManyAsync(items.Select(h => h.ItemInfo).Distinct()).ConfigureAwait(false);
                await base.UpsertManyAsync(items).ConfigureAwait(false);

                Database.Checkpoint();

                await Task.Yield();

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
