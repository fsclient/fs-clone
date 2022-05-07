namespace FSClient.Shared.Providers
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    using FSClient.Shared.Models;

    public interface IItemInfoProvider : IProviderWithRequirements
    {
        bool CanPreload { get; }

        bool CanOpenFromLink(Uri link);

        Task<ItemInfo?> OpenFromLinkAsync(Uri link, CancellationToken cancellationToken);

        Task<bool> PreloadItemAsync(ItemInfo item, PreloadItemStrategy preloadItemStrategy, CancellationToken cancellationToken);
    }
}
