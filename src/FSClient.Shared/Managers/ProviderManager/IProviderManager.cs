namespace FSClient.Shared.Managers
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    using FSClient.Shared.Models;
    using FSClient.Shared.Providers;

    /// <summary>
    /// Site providers manager.
    /// </summary>
    public interface IProviderManager
    {
        /// <summary>
        /// Ensures current main provider.
        /// Sets, if it isn't setted.
        /// Updates, if it is out of dated.
        /// </summary>
        void EnsureCurrentMainProvider();

        /// <summary>
        /// Gets is provider enabled.
        /// </summary>
        /// <param name="site">Provider identifier.</param>
        /// <returns>True, if provider is enabled.</returns>
        bool IsProviderEnabled(Site site);

        /// <summary>
        /// Checks and updates item information, including posters and links.
        /// </summary>
        /// <param name="itemInfo">Item to ensure.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Ensured item. Null, if failed.</returns>
        ValueTask<ItemInfo?> EnsureItemAsync(ItemInfo itemInfo, CancellationToken cancellationToken);

        /// <summary>
        /// Checks is site available.
        /// </summary>
        /// <param name="site">Site to check.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>True, if available.</returns>
        ValueTask<bool> IsSiteAvailable(Site site, CancellationToken cancellationToken);

        /// <summary>
        /// Returns enumerable of supported main provider sites.
        /// </summary>
        /// <returns>IEnumerable of <see cref="ISiteProvider.Site"/> which can be main provider.</returns>
        IEnumerable<Site> GetMainProviders();

        /// <summary>
        /// Returns enumerable of supported files providers
        /// </summary>
        /// <returns>IEnumerable of <see cref="IFileProvider.Site"./></returns>
        IEnumerable<Site> GetFileProviders(FileProviderTypes fileProviderTypes = FileProviderTypes.Online | FileProviderTypes.Torrent);

        /// <summary>
        /// Save providers global order.
        /// </summary>
        /// <param name="collection">Ordered provider keys.</param>
        void SetProvidersOrder(IEnumerable<Site> collection);

        /// <summary>
        /// Gets providers global order.
        /// </summary>
        /// <returns>Ordered provider keys.</returns>
        IEnumerable<Site> GetOrderedProviders();
    }
}
