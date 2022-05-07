namespace FSClient.Shared.Services
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    using FSClient.Shared.Models;

    /// <summary>
    /// TorrServer service
    /// </summary>
    public interface ITorrServerService
    {
        /// <summary>
        /// Is TorrServer address valid URL and server available
        /// </summary>
        Task<bool> IsTorrServerAvailableAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Adds torrent file to TorrServier or update, if it was added before
        /// </summary>
        /// <param name="torrent">Torrent file</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Torrent hash id</returns>
        Task<string> AddOrUpdateTorrentAsync(
            TorrentFolder torrent,
            CancellationToken cancellationToken);

        /// <summary>
        /// Stops torrent from preloading
        /// </summary>
        /// <param name="hashId">Torrent hash id</param>
        /// <param name="cancellationToken">Cancellation token</param>
        Task StopTorrentAsync(
            string hashId,
            CancellationToken cancellationToken);

        /// <summary>
        /// Stops torrent started by application
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        Task StopActiveTorrentsAsync(
            CancellationToken cancellationToken);

        /// <summary>
        /// Removes torrent started by application
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        Task StopAndRemoveActiveTorrentsAsync(
            CancellationToken cancellationToken);

        /// <summary>
        /// Returns torrent content nodes
        /// </summary>
        /// <param name="torrentFile">Torrent file to associate with</param>
        /// <param name="hashId">Torrent hash id</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Collection of nodes in torrent</returns>
        Task<IReadOnlyCollection<ITreeNode>> GetTorrentNodesAsync(
            TorrentFolder torrentFile,
            string hashId,
            CancellationToken cancellationToken);
    }
}
