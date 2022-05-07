namespace FSClient.ViewModels
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    using FSClient.Shared;
    using FSClient.Shared.Helpers;
    using FSClient.Shared.Managers;
    using FSClient.Shared.Models;
    using FSClient.Shared.Mvvm;
    using FSClient.Shared.Services;
    using FSClient.Shared.Services.Specifications;
    using FSClient.ViewModels.Abstract;
    using FSClient.ViewModels.Items;

    public class HistoryViewModel : SelectionViewModel
    {
        private bool savedUnionEpisodesValue;
        private bool historyUpdated = true;
        private ItemInfoTreeNodeSpecification? itemInfoTreeNodeSpecification;
        private ICollection<HistoryListItemViewModel>? flatItems;

        private readonly IHistoryManager historyManager;
        private readonly IItemManager itemManager;
        private readonly ISettingService settingService;

        public HistoryViewModel(
            IHistoryManager historyManager,
            IItemManager itemManager,
            ISettingService settingService)
        {
            this.itemManager = itemManager;
            this.historyManager = historyManager;
            this.settingService = settingService;

            savedUnionEpisodesValue = UnionEpisodes;

            this.historyManager.FilesHistoryChanged += (s, a) =>
            {
                if (a.Reason != HistoryItemChangedReason.Update)
                {
                    historyUpdated = true;
                }
            };

            UpdateSourceCommand = new AsyncCommand(UpdateSourceAsync);

            DeleteItemCommand = new AsyncCommand<HistoryItem>(
                async (e, ct) =>
                {
                    await historyManager.RemoveAsync(new[] { e }).ConfigureAwait(false);
                    await UpdateSourceAsync(ct).ConfigureAwait(false);
                },
                e => e != null);

            DeleteSelectedCommand = new AsyncCommand(
                async ct =>
                {
                    var itemsToRemove = SelectedItems.OfType<HistoryListItemViewModel>()
                        .Select(vm => vm.HistoryItem)
                        .ToArray();
                    if (itemsToRemove.Length > 0)
                    {
                        await historyManager.RemoveAsync(itemsToRemove).ConfigureAwait(false);
                        await UpdateSourceAsync(ct).ConfigureAwait(false);
                    }
                },
                () => IsAnySelected
            );
        }

        protected override IEnumerable<object> Items => flatItems ?? Enumerable.Empty<object>();

        public IEnumerable<object> HistorySource
        {
            get => Get<IEnumerable<object>>();
            private set => Set(value);
        }

        public string SearchRequest
        {
            get => Get(string.Empty);
            set
            {
                if (Set(value))
                {
                    historyUpdated = true;
                    itemInfoTreeNodeSpecification = new ItemInfoTreeNodeSpecification(value);
                }
            }
        }

        public bool IsSourceGrouped
        {
            get => Get(false);
            private set => Set(value);
        }

        public bool GroupItems
        {
            get => Get(() => settingService.GetSetting(Settings.StateSettingsContainer, nameof(GroupItems), true));
            set
            {
                if (Set(value))
                {
                    settingService.SetSetting(Settings.StateSettingsContainer, nameof(GroupItems), value);
                    historyUpdated = true;
                }
            }
        }

        public bool DistinctItems
        {
            get => Get(() => settingService.GetSetting(Settings.StateSettingsContainer, nameof(DistinctItems), true));
            set
            {
                if (Set(value))
                {
                    settingService.SetSetting(Settings.StateSettingsContainer, nameof(DistinctItems), value);
                    historyUpdated = true;
                }
            }
        }

        public bool UnionEpisodes
        {
            get => Get(() => settingService.GetSetting(Settings.StateSettingsContainer, nameof(UnionEpisodes), true));
            set
            {
                if (Set(value))
                {
                    settingService.SetSetting(Settings.StateSettingsContainer, nameof(UnionEpisodes), value);
                    historyUpdated = true;
                }
            }
        }

        public AsyncCommand UpdateSourceCommand { get; }
        public AsyncCommand<HistoryItem> DeleteItemCommand { get; }
        public AsyncCommand DeleteSelectedCommand { get; }

        protected override void OnIsInSelectionModeChanged(bool newValue)
        {
            if (IsInSelectionMode)
            {
                savedUnionEpisodesValue = UnionEpisodes;
                UnionEpisodes = false;
            }
            else
            {
                UnionEpisodes = savedUnionEpisodesValue;
            }
        }

        protected override void OnIsAnySelectedChanged(bool newValue)
        {
            DeleteSelectedCommand.RaiseCanExecuteChanged();
        }

        private async Task UpdateSourceAsync(CancellationToken cancellationToken)
        {
            if (!historyUpdated)
            {
                return;
            }

            ShowProgress = true;

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cts.Token, cancellationToken);

            var items = (await historyManager.GetHistory(true)
                .ToListAsync(linkedCts.Token)
                .ConfigureAwait(false))
                .AsEnumerable();

            if (itemInfoTreeNodeSpecification != null)
            {
                items = items
                    .Where(item => itemInfoTreeNodeSpecification.IsSatisfiedBy(item.ItemInfo));
            }

            IEnumerable<object> source = Enumerable.Empty<HistoryListItemViewModel>();

            try
            {
                HistorySource = source;
                if (GroupItems)
                {
                    IsSourceGrouped = true;
                    if (DistinctItems)
                    {
                        source = UnionEpisodeEntries(items, true)
                            .GroupBy(GetHeaderTitle, MapToViewModel)
                            .ToList();
                    }
                    else if (UnionEpisodes)
                    {
                        source = items
                            .GroupBy(
                                GetHeaderTitle,
                                (g, i) => UnionEpisodeEntries(i, false)
                                    .GroupBy(_ => g, MapToViewModel)
                                    .FirstOrDefault())
                            .ToList();
                    }
                    else
                    {
                        source = items.GroupBy(GetHeaderTitle, MapToViewModel).ToList();
                    }
                }
                else
                {
                    IsSourceGrouped = false;
                    if (UnionEpisodes)
                    {
                        source = UnionEpisodeEntries(items, DistinctItems).Select(MapToViewModel);
                    }
                    else
                    {
                        source = items.Select(MapToViewModel);
                    }
                }
            }
            catch
            {
                source = items.Select(MapToViewModel);
                throw;
            }
            finally
            {
                flatItems = source.SelectMany(g => g is IGrouping<string, HistoryListItemViewModel> group
                    ? group.OfType<HistoryListItemViewModel>()
                    : new[] { (HistoryListItemViewModel)g }).ToList();
                HistorySource = source;

                ShowProgress = false;
                historyUpdated = false;
            }

            HistoryListItemViewModel MapToViewModel(HistoryItem historyItem)
            {
                return new HistoryListItemViewModel(historyItem, itemManager);
            }
        }

        private IEnumerable<HistoryItem> UnionEpisodeEntries(IEnumerable<HistoryItem> inputList, bool distinctItems)
        {
            var outputList = new List<HistoryItem>();
            List<HistoryItem>? tempRange = null;
            foreach (var historyItem in inputList.OrderBy(item => item.ItemInfo.GetHashCode()).ThenBy(item => item.Season).ThenBy(item => item.Episode?.Start.Value))
            {
                if (tempRange == null)
                {
                    if (!historyItem.Episode.HasValue)
                    {
                        outputList.Add(historyItem);
                        continue;
                    }

                    tempRange = new List<HistoryItem>
                    {
                        historyItem
                    };
                    continue;
                }
                var inRange = tempRange.Any(tempItem => tempItem != null
                    && tempItem.ItemInfo.Site == historyItem.ItemInfo.Site
                    && tempItem.ItemInfo.SiteId == historyItem.ItemInfo.SiteId
                    && (distinctItems
                        || (tempItem.Season == historyItem.Season
                        && tempItem.Episode.HasValue
                        && historyItem.Episode.HasValue
                        && tempItem.Episode.Value.IsNear(historyItem.Episode.Value))));
                if (inRange)
                {
                    tempRange.Add(historyItem);
                }
                else
                {
                    if (tempRange.Count > 0)
                    {
                        outputList.Add(tempRange.Count > 1
                            ? new HistoryItemRange(tempRange, distinctItems)
                            : tempRange[0]);
                    }

                    tempRange = new List<HistoryItem>
                    {
                        historyItem
                    };
                }
            }
            if (tempRange?.Count > 0)
            {
                outputList.Add(tempRange.Count > 1
                    ? new HistoryItemRange(tempRange, distinctItems)
                    : tempRange[0]);
            }

            return outputList.OrderByDescending(item => item.AddTime)
                .ThenByDescending(item => item.Season)
                .ThenByDescending(item => item.Episode?.Start.Value);
        }

        private static string GetHeaderTitle(HistoryItem model)
        {
            return model.AddTime.GetElapsedTimeString();
        }
    }
}
