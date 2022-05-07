namespace FSClient.Shared.Providers
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    using FSClient.Shared.Models;

    public interface ISiteProvider : IProvider
    {
        ProviderDetails Details { get; }

        bool CanBeMain { get; }

        bool EnforceDisabled { get; }

        bool IsEnabledByDefault { get; }

        bool IsVisibleToUser { get; }

        IReadOnlyList<Uri> Mirrors { get; }

        User? CurrentUser { get; set; }

        Task<bool> IsAvailableAsync(CancellationToken cancellationToken);
        ValueTask<Uri> GetMirrorAsync(CancellationToken cancellationToken);
        ValueTask<ItemInfo> EnsureItemAsync(ItemInfo itemInfo, CancellationToken cancellationToken);
    }
}
