namespace FSClient.Shared.Managers
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    using FSClient.Shared.Models;

    /// <summary>
    /// Manager for parsing pages with player.
    /// </summary>
    public interface IPlayerParseManager
    {
        /// <summary>
        /// Checks if provider is supported or if provider can parse link.
        /// </summary>
        /// <param name="httpUri">Page link with player.</param>
        /// <param name="knownSite">Known site key.</param>
        /// <returns>True if provider support link or represents known hosting name.</returns>
        bool CanOpenFromLinkOrHostingName(
            Uri httpUri,
            Site knownSite);

        /// <summary>
        /// Parse page with player to get file.
        /// </summary>
        /// <param name="httpUri">Page link with player. Could be embed iframe.</param>
        /// <param name="knownSite">Known site key.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Parsed and preloaded file or null if failed.</returns>
        Task<File?> ParseFromUriAsync(
            Uri httpUri,
            Site knownSite,
            CancellationToken cancellationToken);
    }
}
