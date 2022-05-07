namespace FSClient.Shared.Models
{
    using System;

    /// <summary>
    /// Files tree node
    /// </summary>
    public interface ITreeNode
    {
        /// <summary>
        /// Site owned this node
        /// </summary>
        Site Site { get; }

        /// <summary>
        /// Related item from main provider. Can be with different site from <see cref="Site"/>
        /// </summary>
        ItemInfo? ItemInfo { get; set; }

        /// <summary>
        /// Parent node
        /// </summary>
        IFolderTreeNode? Parent { get; set; }

        /// <summary>
        /// Node id
        /// </summary>
        string Id { get; }

        /// <summary>
        /// Node title
        /// </summary>
        string? Title { get; }

        /// <summary>
        /// Node group. Can be used for grouping on files tree
        /// </summary>
        string? Group { get; set; }

        /// <summary>
        /// Related season
        /// </summary>
        int? Season { get; }

        /// <summary>
        /// Related episode
        /// </summary>
        Range? Episode { get; }

        /// <summary>
        /// Is node watched (Position = 1)
        /// </summary>
        bool IsWatched { get; set; }

        /// <summary>
        /// Node position
        /// </summary>
        float Position { get; set; }

        /// <summary>
        /// Is node from torrent file or related to torrent provider
        /// </summary>
        bool IsTorrent { get; set; }
    }
}
