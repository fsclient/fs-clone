namespace FSClient.ViewModels
{
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    using FSClient.Localization.Resources;
    using FSClient.Shared.Managers;
    using FSClient.Shared.Models;
    using FSClient.Shared.Mvvm;
    using FSClient.Shared.Providers;
    using FSClient.Shared.Services;

    public class FavoriteMenuViewModel : ViewModelBase
    {
        private IEnumerable<ItemInfo>? items;

        private readonly IFavoriteManager favoriteManager;
        private readonly INotificationService notificationService;
        private readonly SafeObservableCollection<FavoriteListKind> checkedKinds;
        private readonly SafeObservableCollection<FavoriteListKind> availableKinds;

        public FavoriteMenuViewModel(
            IFavoriteManager favoriteManager,
            INotificationService notificationService)
        {
            this.favoriteManager = favoriteManager;
            this.notificationService = notificationService;

            checkedKinds = new SafeObservableCollection<FavoriteListKind>();
            availableKinds = new SafeObservableCollection<FavoriteListKind>();
            availableKinds.AddRange(this.favoriteManager.AvailableListKinds);

            ShowProgress = false;

            this.favoriteManager.FavoritesChanged += FavoriteManager_FavoritesChanged;
            CheckKindCommand = new AsyncCommand<FavoriteListKind>(
                (kind, ct) => SetupTypeAsync(kind, true, ct),
                _ => !ShowProgress);

            UncheckKindCommand = new AsyncCommand<FavoriteListKind>(
                (kind, ct) => SetupTypeAsync(kind, false, ct),
                _ => !ShowProgress);
        }

        public IEnumerable<ItemInfo> Items
        {
            get => items ?? Enumerable.Empty<ItemInfo>();
            set
            {
                if (items != value)
                {
                    items = value;
                    _ = RefreshCheckedAsync();
                }
            }
        }

        public ObservableCollection<FavoriteListKind> AvailableKinds => availableKinds;
        public ObservableCollection<FavoriteListKind> CheckedKinds => checkedKinds;

        public AsyncCommand<FavoriteListKind> CheckKindCommand { get; }
        public AsyncCommand<FavoriteListKind> UncheckKindCommand { get; }

        private async void FavoriteManager_FavoritesChanged(object sender, FavoriteChangedEventArgs e)
        {
            ShowProgress = true;
            CheckKindCommand.RaiseCanExecuteChanged();
            UncheckKindCommand.RaiseCanExecuteChanged();

            if (e.Reason == FavoriteItemChangedReason.Reset)
            {
                RefreshAvailable();
            }
            await RefreshCheckedAsync();

            ShowProgress = false;
            CheckKindCommand.RaiseCanExecuteChanged();
            UncheckKindCommand.RaiseCanExecuteChanged();
        }

        private void RefreshAvailable()
        {
            availableKinds.Clear();
            availableKinds.AddRange(favoriteManager.AvailableListKinds);
        }

        private async Task RefreshCheckedAsync()
        {
            checkedKinds.Clear();

            var itemsFavTypes = await favoriteManager.GetFavoritesByItems(Items).Select(f => f.ListKind).ToArrayAsync().ConfigureAwait(false);

            checkedKinds.AddRange(availableKinds.Intersect(itemsFavTypes));
        }

        private Task SetupTypeAsync(FavoriteListKind kind, bool value, CancellationToken cancellationToken)
        {
            return Task.WhenAll(Items
                .Where(item => favoriteManager.IsSupportedByProvider(item))
                .Select(async item =>
                {
                    if (!favoriteManager.IsSupportedByProvider(item))
                    {
                        await notificationService.ShowAsync(Strings.Favorites_ItemIsNotSupportedByProviderMessage, NotificationType.Warning).ConfigureAwait(false);
                        return;
                    }
                    if (!favoriteManager.AvailableListKinds.Contains(kind))
                    {
                        await notificationService.ShowAsync(Strings.Favorites_NoListTypeMessage, NotificationType.Warning).ConfigureAwait(false);
                        return;
                    }

                    var result = value
                        ? await favoriteManager.AddToListAsync(item, kind, cancellationToken).ConfigureAwait(false)
                        : await favoriteManager.RemoveFromListAsync(item, kind, cancellationToken).ConfigureAwait(false);

                    if (!result)
                    {
                        await notificationService.ShowAsync(Strings.Favorites_UnknownError, NotificationType.Error).ConfigureAwait(false);
                    }
                }));
        }
    }
}
