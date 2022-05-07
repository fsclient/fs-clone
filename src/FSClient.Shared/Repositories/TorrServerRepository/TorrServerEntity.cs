namespace FSClient.Shared.Repositories
{
    using FSClient.Shared.Models;

    /// <summary>
    /// TorrServer entity
    /// </summary>
    public class TorrServerEntity
    {
        /// <summary>
        /// Internal <see cref="TorrentFolder"/> id from external provider
        /// </summary>
        public string? TorrentId { get; set; }

        /// <summary>
        /// TorrServer hash
        /// </summary>
        public string? TorrServerHash { get; set; }
    }
}
