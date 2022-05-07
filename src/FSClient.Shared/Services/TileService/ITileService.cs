namespace FSClient.Shared.Services
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    using FSClient.Shared.Models;

    public interface ITileService
    {
        Task UpdateTimelineAsync(IEnumerable<ItemInfo> updatedItems, bool isRemoving, CancellationToken cancellationToken);

        Task<bool> SetRecentItemsToJumpListAsync(IAsyncEnumerable<ItemInfo> items, CancellationToken cancellationToken);

        Task<bool> PinItemTileAsync(ItemInfo item, CancellationToken cancellationToken);

        Task CheckSecondatyTilesAsync(CancellationToken cancellationToken);
    }
}
