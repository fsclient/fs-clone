namespace FSClient.Shared.Managers
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    using FSClient.Shared.Models;
    using FSClient.Shared.Providers;

    public interface IFavoriteManager
    {
        event EventHandler<FavoriteChangedEventArgs>? FavoritesChanged;

        FavoriteProviderType ProviderType { get; set; }

        IEnumerable<FavoriteListKind> AvailableListKinds { get; }

        IAsyncEnumerable<FavoriteItem> GetFavoritesByItems(IEnumerable<ItemInfo> items);

        IAsyncEnumerable<FavoriteItem> GetFavorites(FavoriteListKind listKind);

        ValueTask<bool> AddToListAsync(ItemInfo item, FavoriteListKind listKind, CancellationToken cancellationToken);

        ValueTask<bool> RemoveFromListAsync(ItemInfo item, FavoriteListKind listKind, CancellationToken cancellationToken);

        bool IsSupportedByProvider(ItemInfo item);
    }
}
