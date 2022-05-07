namespace FSClient.Shared.Providers
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    using FSClient.Shared.Models;

    public interface IFavoriteProvider : IProvider
    {
        IEnumerable<FavoriteListKind> AvailableListKinds { get; }

        Task<IReadOnlyList<ItemInfo>> GetItemsAsync(FavoriteListKind listKind, CancellationToken cancellationToken);
        Task<bool> AddAsync(ItemInfo item, FavoriteListKind listKind, CancellationToken cancellationToken);
        Task<bool> RemoveAsync(ItemInfo item, FavoriteListKind listKind, CancellationToken cancellationToken);

        bool IsItemSupported(ItemInfo item);
    }
}
