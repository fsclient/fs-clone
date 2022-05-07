namespace FSClient.Shared.Managers
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    using FSClient.Shared.Helpers;
    using FSClient.Shared.Models;
    using FSClient.Shared.Repositories;
    using FSClient.Shared.Services;

    using Microsoft.Extensions.Logging;

    /// <inheritdoc/>
    public sealed class HistoryManager : IHistoryManager
    {
        // 1 second from 1 hour
        private const float MinFilePositionToUpdate = 100f / ((float)TimeSpan.TicksPerHour / TimeSpan.TicksPerSecond);

        private readonly IHistoryRepository historyRepository;
        private readonly IProviderManager providerManager;
        private readonly ILogger logger;

        public HistoryManager(
            IHistoryRepository historyRepository,
            IProviderManager providerManager,
            ILogger logger)
        {
            this.historyRepository = historyRepository;
            this.logger = logger;
            this.providerManager = providerManager;
        }

        /// <inheritdoc/>
        public event EventHandler<HistoryChangedEventArgs>? FilesHistoryChanged;

        /// <inheritdoc/>
        public event EventHandler<ItemHistoryChangedEventArgs>? ItemsHistoryChanged;

        /// <inheritdoc/>
        public async Task<int> UpsertAsync(IEnumerable<HistoryItem> items)
        {
            var toRemove = new List<HistoryItem>();
            var toUpdate = new List<HistoryItem>();
            var toInsert = new List<HistoryItem>();

            var lastItem = await historyRepository
                .GetOrderedHistory()
                .FirstOrDefaultAsync(CancellationToken.None)
                .ConfigureAwait(false);
            foreach (var item in items)
            {
                if (item.Node != null
                    && item.Node.Position < float.Epsilon)
                {
                    toRemove.Add(item);
                }
                else
                {
                    var similarItem = await historyRepository.GetAsync(item.Key).ConfigureAwait(false);

                    if (similarItem != null)
                    {
                        if (similarItem == lastItem)
                        {
                            if (lastItem.Node?.Position < MinFilePositionToUpdate
                                && item.Node?.Position < MinFilePositionToUpdate)
                            {
                                continue;
                            }

                            toUpdate.Add(item);

                            continue;
                        }

                        toRemove.Add(similarItem);
                    }

                    if (lastItem?.Key != item.Key)
                    {
                        toInsert.Add(item);
                    }
                    else
                    {
                        toUpdate.Add(item);
                    }
                }
            }

            if (toRemove.Count > 0)
            {
                await RemoveAsync(toRemove).ConfigureAwait(false);
            }
            if (toUpdate.Count > 0 || toInsert.Count > 0)
            {
                var toUpsert = toUpdate.Union(toInsert).ToList();
                await historyRepository.UpsertManyAsync(toUpsert).ConfigureAwait(false);

                var reason = toInsert.Count > 0 ? HistoryItemChangedReason.Added : HistoryItemChangedReason.Update;
                OnItemsHistoryChanged(reason, toUpsert.Select(h => h.ItemInfo).Distinct());
                OnFilesHistoryChanged(reason);
            }

            return toUpdate.Count + toInsert.Count + toRemove.Count;
        }

        /// <inheritdoc/>
        public async Task<int> RemoveAsync(IEnumerable<HistoryItem> items)
        {
            var removedDirectly = items
                .SelectMany(i => i is HistoryItemRange range
                    ? range.Items
                    : new[] { i })
                .Where(e => e != null)
                .ToArray();

            if (removedDirectly.Length == 0)
            {
                return 0;
            }

            await historyRepository.DeleteManyAsync(removedDirectly).ConfigureAwait(false);

            var reason = HistoryItemChangedReason.Removed;
            OnItemsHistoryChanged(reason, removedDirectly.Select(h => h.ItemInfo).Distinct());
            OnFilesHistoryChanged(reason);

            return removedDirectly.Length;
        }

        /// <inheritdoc/>
        public IAsyncEnumerable<HistoryItem> GetHistory(bool ensureItems)
        {
            if (!ensureItems)
            {
                return historyRepository.GetOrderedHistory();
            }
            else
            {
                return historyRepository.GetOrderedHistory()
                    .SelectBatchAwait(10, async (item, ct) =>
                    {
                        try
                        {
                            var itemInfo = await providerManager.EnsureItemAsync(item.ItemInfo, ct).ConfigureAwait(false);
                            if (itemInfo == null)
                            {
                                return null;
                            }
                            return new HistoryItem(itemInfo, item.Node)
                            {
                                AddTime = item.AddTime,
                                Season = item.Season,
                                Episode = item.Episode,
                                IsTorrent = item.IsTorrent
                            };
                        }
                        catch (OperationCanceledException)
                        {
                            return item;
                        }
                    })
                    .Where(item => item != null)!;
            }
        }

        /// <inheritdoc/>
        public async Task LoadPositionToNodeAsync(ITreeNode node)
        {
            if (Settings.Instance.PositionByEpisode)
            {
                if (node is File file
                    && file.ItemInfo != null)
                {
                    node.Position = (await historyRepository
                        .GetOrderedHistory(file.ItemInfo.Key, file.Season, file.Episode)
                        .FirstOrDefaultAsync(CancellationToken.None)
                        .ConfigureAwait(false))?
                        .Node?.Position ?? 0;
                }

                return;
            }

            if (string.IsNullOrWhiteSpace(node.Id))
            {
                return;
            }

            if (node.Parent is IFolderTreeNode parent
                && parent.Position < float.Epsilon
                && parent.PositionBehavior == PositionBehavior.Average)
            {
                node.Position = parent.Position;
                return;
            }

            node.Position = await historyRepository.GetNodePositionById(node.Id).ConfigureAwait(false);
            if (node.Position < float.Epsilon && node is IFolderTreeNode folder)
            {
                await Task.WhenAll(folder.ItemsSource
                    .Select(LoadPositionToNodeAsync))
                    .ConfigureAwait(false);

                node.Position = folder.CalculatePosition();
            }
        }

        /// <inheritdoc/>
        public ValueTask<bool> IsInHistoryAsync(ItemInfo item)
        {
            return historyRepository.GetOrderedHistory(item.Key, null, null).AnyAsync();
        }

        /// <inheritdoc/>
        public async Task<TTreeNode?> GetLastViewedFolderChildAsync<TTreeNode>(IFolderTreeNode folder, HistoryItem? item)
            where TTreeNode : class, ITreeNode
        {
            if (folder == null)
            {
                return null;
            }

            var lastNode = folder.ItemsSource.OfType<TTreeNode>().LastOrDefault(n => n.Position > 0);
            try
            {
                var providerId = item?.ItemInfo.Site.Value;
                var ids = folder.GetIDsStack().ToArray();
                if (ids.FirstOrDefault() == providerId)
                {
                    ids = ids.Skip(1).ToArray();
                }

                var nodeId = GetValidNode(item?.Node?.Flatten(), ids, providerId)?.Key;
                if (nodeId == null
                    && folder.ItemInfo?.Key is string folderItemInfoKey)
                {
                    nodeId = await historyRepository
                        .GetOrderedHistory(folderItemInfoKey)
                        .Select(e => GetValidNode(e.Node!.Flatten(), ids, providerId)?.Key)
                        .Where(id => id != null)
                        .FirstOrDefaultAsync(CancellationToken.None)
                        .ConfigureAwait(false);
                }

                if (nodeId != null)
                {
                    lastNode = folder.ItemsSource
                        .OfType<TTreeNode>()
                        .FirstOrDefault(node => node.Id == nodeId);
                }
            }
            catch (Exception ex)
            {
                logger?.LogError(ex);
            }
            return lastNode;

            static HistoryNode? GetValidNode(IEnumerable<HistoryNode>? e, string[] folderIds, string? providerId)
            {
                if (e?.Any() != true)
                {
                    return null;
                }

                var itemIds = e.First()?.Key == providerId
                    ? e.Skip(1).ToArray()
                    : e.ToArray();

                if (folderIds.Length > itemIds.Length)
                {
                    return null;
                }

                for (var index = 0; index < itemIds.Length; index++)
                {
                    if (folderIds.Length <= index)
                    {
                        return itemIds[index];
                    }

                    if (itemIds[index].Key != folderIds[index])
                    {
                        return null;
                    }
                }
                return e.First();
            }
        }

        private void OnItemsHistoryChanged(HistoryItemChangedReason reason, IEnumerable<ItemInfo> items)
        {
            ItemsHistoryChanged?.Invoke(this, new ItemHistoryChangedEventArgs(reason, items));
        }

        private void OnFilesHistoryChanged(HistoryItemChangedReason reason)
        {
            FilesHistoryChanged?.Invoke(this, new HistoryChangedEventArgs(reason));
        }
    }
}
