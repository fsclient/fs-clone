namespace FSClient.ViewModels
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    using FSClient.Localization.Resources;
    using FSClient.Shared;
    using FSClient.Shared.Helpers;
    using FSClient.Shared.Managers;
    using FSClient.Shared.Models;
    using FSClient.Shared.Mvvm;
    using FSClient.Shared.Providers;
    using FSClient.Shared.Services;
    using FSClient.ViewModels.Items;

    using Humanizer;

    using Microsoft.Extensions.Logging;

    public class ItemViewModel : ViewModelBase
    {
        private readonly ITileService tileService;
        private readonly ILauncherService launcherService;
        private readonly IIncrementalCollectionFactory collectionFactory;
        private readonly INotificationService notificationService;
        private readonly ILogger logger;
        private readonly IFavoriteManager favoriteManager;
        private readonly IItemManager itemManager;
        private readonly IHistoryManager historyManager;
        private readonly IProviderManager providerManager;
        private readonly IReviewManager reviewManager;
        private readonly ISettingService settingService;

        public ItemViewModel(
            IItemManager itemManager,
            IFavoriteManager favoriteManager,
            IHistoryManager historyManager,
            IProviderManager providerManager,
            IReviewManager reviewManager,
            ITileService tileService,
            ILauncherService launcherService,
            IShareService shareService,
            INotificationService notificationService,
            ISettingService settingService,
            IIncrementalCollectionFactory collectionFactory,
            ILogger logger)
        {
            this.tileService = tileService;
            this.launcherService = launcherService;
            this.notificationService = notificationService;
            this.historyManager = historyManager;
            this.providerManager = providerManager;
            this.reviewManager = reviewManager;
            this.settingService = settingService;
            this.logger = logger;
            this.collectionFactory = collectionFactory;

            this.itemManager = itemManager;

            this.favoriteManager = favoriteManager;
            this.favoriteManager.FavoritesChanged += (_, __) => EnsureFavoriteItem();

            OpenItemInBrowserCommand = new AsyncCommand(
                _ => this.launcherService.LaunchUriAsync(CurrentItem!.Link!),
                () => CurrentItem?.Link != null,
                AsyncCommandConflictBehaviour.WaitPrevious);

            PinToStartCommand = new AsyncCommand(
                async ct =>
                {
                    if (!await this.tileService.PinItemTileAsync(CurrentItem!, ct).ConfigureAwait(false))
                    {
                        await this.notificationService.ShowAsync(Strings.ItemViewModel_UnableToPinTile, NotificationType.Error).ConfigureAwait(false);
                    }
                },
                () => CurrentItem?.Link != null,
                AsyncCommandConflictBehaviour.WaitPrevious);

            ShareItemCommand = new AsyncCommand(
                _ => shareService.ShareItemAsync(CurrentItem!),
                () => shareService.IsSupported && CurrentItem?.Link != null,
                AsyncCommandConflictBehaviour.WaitPrevious);

            RefreshCurrentItemCommand = new AsyncCommand<ItemInfo?>(
                RefreshCurrentItemAsync,
                AsyncCommandConflictBehaviour.CancelPrevious);

            VoteCommand = new AsyncCommand<IRatingVote>(
                VoteAsync,
                _ => CurrentItem != null && (Rating?.CanVote ?? false),
                AsyncCommandConflictBehaviour.Skip);
        }

        public bool ShowVideoAttention
        {
            get => settingService.GetSetting(Settings.StateSettingsContainer, nameof(ShowVideoAttention), true, SettingStrategy.Roaming);
            set => settingService.SetSetting(Settings.StateSettingsContainer, nameof(ShowVideoAttention), value, SettingStrategy.Roaming);
        }

        public bool ShowCalendarHelpInfo
        {
            get => settingService.GetSetting(Settings.StateSettingsContainer, nameof(ShowCalendarHelpInfo), true, SettingStrategy.Roaming);
            set => settingService.SetSetting(Settings.StateSettingsContainer, nameof(ShowCalendarHelpInfo), value, SettingStrategy.Roaming);
        }

        public ItemInfo? CurrentItem
        {
            get => Get<ItemInfo>();
            private set
            {
                if (Set(value))
                {
                    ShareItemCommand.RaiseCanExecuteChanged();
                    OpenItemInBrowserCommand.RaiseCanExecuteChanged();
                    PinToStartCommand.RaiseCanExecuteChanged();
                    VoteCommand.RaiseCanExecuteChanged();
                    OnPropertyChanged(nameof(SimilarItems));
                    EnsureFavoriteItem();
                }
            }
        }

        public IRating? Rating
        {
            get => Get<IRating?>();
            private set
            {
                if (Set(value))
                {
                    VoteCommand.RaiseCanExecuteChanged();
                }
            }
        }

        public IIncrementalCollection<EpisodeInfo, SeasonInfo>? CurrentItemCalendar
        {
            get => Get<IIncrementalCollection<EpisodeInfo, SeasonInfo>>();
            private set => Set(value);
        }

        public IEnumerable<ItemsListItemViewModel> SimilarItems => CurrentItem?.Details.Similar
            .Select(i => new ItemsListItemViewModel(i, DisplayItemMode.Normal, itemManager))
            ?? Enumerable.Empty<ItemsListItemViewModel>();

        public IEnumerable<ItemsListItemViewModel> FranchiseItems => CurrentItem?.Details.Franchise
            .Select(i => new ItemsListItemViewModel(i, DisplayItemMode.Normal, itemManager))
            ?? Enumerable.Empty<ItemsListItemViewModel>();

        public bool IsPreloaded { get; private set; }

        public bool IsInAnyList
        {
            get => Get(false);
            private set => Set(value);
        }

        public bool IsInHistory { get; private set; }

        public bool IsListsSupported => CurrentItem != null && favoriteManager.IsSupportedByProvider(CurrentItem);

        public AsyncCommand ShareItemCommand { get; }
        public AsyncCommand PinToStartCommand { get; }
        public AsyncCommand OpenItemInBrowserCommand { get; }
        public AsyncCommand<ItemInfo?> RefreshCurrentItemCommand { get; }
        public AsyncCommand<IRatingVote> VoteCommand { get; }

        private async void EnsureFavoriteItem()
        {
            if (CurrentItem == null)
            {
                IsInAnyList = false;
            }
            else
            {
                IsInAnyList = await favoriteManager.GetFavoritesByItems(new[] { CurrentItem }).AnyAsync(default);
            }
            OnPropertyChanged(nameof(IsListsSupported));
        }

        private async Task RefreshCurrentItemAsync(ItemInfo? currentItem, CancellationToken token)
        {
            try
            {
                var item = currentItem;
                var isInHistory = false;
                if (item == null
                    || item.Site == Site.Any)
                {
                    if (item?.Link is Uri link)
                    {
                        item = await itemManager.OpenFromLinkAsync(link, token).ConfigureAwait(false);
                    }
                    else
                    {
                        item = await historyManager
                            .GetItemsHistory()
                            .FirstOrDefaultAsync(token)
                            .ConfigureAwait(false);
                        isInHistory = item != null;
                    }
                }

                if (item == null)
                {
                    item = CurrentItem;
                    await notificationService
                        .ShowAsync(
                            Strings.ItemViewModel_UnableToGetFullItemInfo,
                            NotificationType.Error)
                        .ConfigureAwait(false);
                    return;
                }

                if (CurrentItem != null
                    && item.SiteId == CurrentItem.SiteId
                    && item.Site == CurrentItem.Site)
                {
                    return;
                }

                if (!isInHistory)
                {
                    isInHistory = await historyManager
                        .GetItemsHistory()
                        .Where(i => i.Key == item.Key)
                        .FirstOrDefaultAsync(token)
                        .ConfigureAwait(false) != null;
                }
                IsInHistory = isInHistory;
                IsPreloaded = false;

                CurrentItem = item;

                if (!itemManager.CanPreload(item))
                {
                    return;
                }

                var success = await itemManager.PreloadItemAsync(item, PreloadItemStrategy.Full, token).ConfigureAwait(false);
                IsPreloaded = success;

                OnPropertyChanged(nameof(CurrentItem));

                if (token.IsCancellationRequested)
                {
                    return;
                }

                if (!success)
                {
                    await notificationService
                        .ShowAsync(
                            Strings.ItemViewModel_UnableToGetFullItemInfo,
                            NotificationType.Error)
                        .ConfigureAwait(false);
                    if (!await providerManager.IsSiteAvailable(item.Site, token).ConfigureAwait(false)
                        && !token.IsCancellationRequested)
                    {
                        await notificationService
                            .ShowAsync(
                                string.Format(Strings.ItemViewModel_SiteIsNotAvailableNow, item.Site.Title),
                                NotificationType.Warning)
                            .ConfigureAwait(false);
                    }
                }

                CurrentItemCalendar = item.Details.EpisodesCalendar == null ? null
                    : collectionFactory.CreateGrouped<EpisodeInfo, int, SeasonInfo>(
                    e => e.Season,
                    item.Details.EpisodesCalendar);
                Rating = item.Details.Rating;
            }
            catch (OperationCanceledException)
            {

            }
            catch (Exception ex)
            {
                ex.Data["Item"] = currentItem;
                logger?.LogError(ex);
            }
        }

        private async Task VoteAsync(IRatingVote ratingVote, CancellationToken cancellationToken)
        {
            if (Rating is not IRating rating
                || CurrentItem is not ItemInfo itemInfo)
            {
                return;
            }

            var (newRating, result) = await reviewManager.VoteItemAsync(itemInfo!, rating, ratingVote, cancellationToken).ConfigureAwait(false);
            if (newRating != null)
            {
                Rating = itemInfo!.Details.Rating = newRating;
                VoteCommand.RaiseCanExecuteChanged();
            }

            if (result != ProviderResult.Success
                && EnumHelper.GetDisplayDescription(result).FormatWith(itemInfo.Site.Title) is string errorMessage)
            {
                await notificationService.ShowAsync(errorMessage, NotificationType.Warning).ConfigureAwait(false);
            }
        }
    }
}
