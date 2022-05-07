namespace FSClient.Shared.Managers
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    using FSClient.Shared.Models;

    /// <summary>
    /// Files manager.
    /// </summary>
    public interface IFileManager
    {
        /// <summary>
        /// Event, which is firied, when <see cref="File"/> with specific <see cref="NodeOpenWay"/> was opened.
        /// </summary>
        event Action<Video, NodeOpenWay> VideoOpened;

        /// <summary>
        /// Last opened video.
        /// </summary>
        Video? LastVideo { get; }

        /// <summary>
        /// Gets enumerable of trailer files for specific item.
        /// </summary>
        /// <param name="item">Item, which trailers must be returned.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Enumerable of nodes.</returns>
        Task<IEnumerable<ITreeNode>> GetTrailersAsync(
            ItemInfo item, CancellationToken cancellationToken);

        /// <summary>
        /// Is <see cref="NodeOpenWay"/> available for specific node.
        /// </summary>
        /// <param name="node">Node to check.</param>
        /// <param name="way">Node open way.</param>
        /// <returns>True, if available.</returns>
        bool IsOpenWayAvailableForNode(
            ITreeNode node, NodeOpenWay way);

        /// <summary>
        /// Preload and open file.
        /// </summary>
        /// <param name="file">File to open.</param>
        /// <param name="way">Node open way.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>True, if successful.</returns>
        Task<bool> OpenFileAsync(
            File file, NodeOpenWay way, CancellationToken cancellationToken);

        /// <summary>
        /// Open video.
        /// </summary>
        /// <param name="video">Video to open.</param>
        /// <param name="way">Node open way.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>True, if successful.</returns>
        Task<bool> OpenVideoAsync(
            Video video, NodeOpenWay way, CancellationToken cancellationToken);

        /// <summary>
        /// Preloads files in order from last viewed.
        /// </summary>
        /// <param name="nodes">Nodes to preload.</param>
        /// <param name="preloadEpisodes">Should episode files be prealoaded.</param>
        /// <param name="historyItem">History item to detect last viewed file. If null, history data will be queried.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Is all files preloaded successfully.</returns>
        Task<bool> PreloadNodesAsync(
            IEnumerable<IPreloadableNode> nodes, bool preloadEpisodes, HistoryItem? historyItem, CancellationToken cancellationToken);

        /// <summary>
        /// Disposes possible resources depended on video.
        /// </summary>
        /// <param name="video">Stoped video.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task HandleVideoStopedAsync(
            Video video, CancellationToken cancellationToken);
    }
}
