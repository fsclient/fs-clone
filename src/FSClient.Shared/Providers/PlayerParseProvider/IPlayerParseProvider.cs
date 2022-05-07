namespace FSClient.Shared.Providers
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    using FSClient.Shared.Models;

    /// <summary>
    /// Provider for parsing pages with player.
    /// </summary>
    public interface IPlayerParseProvider : IProviderWithRequirements
    {
        /// <summary>
        /// Checks if provider can parse link.
        /// </summary>
        /// <param name="link">Page link with player.</param>
        /// <returns>True if provider support link or represents known hosting name.</returns>
        bool CanOpenFromLinkOrHostingName(Uri link);

        /// <summary>
        /// Parse page with player to get file.
        /// </summary>
        /// <param name="link">Page link with player. Could be embed iframe.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Parsed file or null if failed.</returns>
        Task<File?> ParseFromUriAsync(Uri link, CancellationToken cancellationToken);
    }
}
