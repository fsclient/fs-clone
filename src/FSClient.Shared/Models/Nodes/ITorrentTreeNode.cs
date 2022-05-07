namespace FSClient.Shared.Models
{
    using System;

    /// <summary>
    /// Torrent tree node
    /// </summary>
    public interface ITorrentTreeNode : IPreloadableNode
    {
        /// <summary>
        /// Torrent file link or magnet link
        /// </summary>
        Uri? Link { get; }

        /// <summary>
        /// Is <see cref="Link"/> magnet link
        /// </summary>
        bool IsMagnet { get; }

        /// <summary>
        /// Torrent hash
        /// </summary>
        string? TorrentHash { get; set; }

        /// <summary>
        /// Torrent folder size
        /// </summary>
        string? Size { get; }

        /// <summary>
        /// Seeds count 
        /// </summary>
        int? Seeds { get; }

        /// <summary>
        /// Peers count
        /// </summary>
        int? Peers { get; }

        /// <summary>
        /// Leeches count
        /// </summary>
        int? Leeches { get; set; }

        /// <summary>
        /// Torrent quality, if single file
        /// </summary>
        Quality? Quality { get; }
    }
}
