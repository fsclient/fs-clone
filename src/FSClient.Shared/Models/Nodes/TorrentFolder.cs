namespace FSClient.Shared.Models
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Torrent tree node
    /// </summary>
    public class TorrentFolder : Folder, ITorrentTreeNode, IEquatable<TorrentFolder>
    {
        private readonly Func<CancellationToken, ValueTask<Uri?>> linkFactory;

        public TorrentFolder(Site site, string id, Func<CancellationToken, ValueTask<Uri?>> linkFactory)
            : base(site, id, FolderType.Unknown, PositionBehavior.Average)
        {
            this.linkFactory = linkFactory;
            IsTorrent = true;
        }

        public TorrentFolder(Site site, string id, Uri link)
            : base(site, id, FolderType.Unknown, PositionBehavior.Average)
        {
            linkFactory = _ => new ValueTask<Uri?>(link);
            Link = link;
            IsTorrent = true;
        }

        /// <inheridoc/>
        public Uri? Link { get; private set; }

        /// <inheridoc/>
        public bool IsMagnet => Link?.Scheme?.Contains("magnet") ?? false;

        /// <inheridoc/>
        public string? TorrentHash { get; set; }

        /// <inheridoc/>
        public string? Size { get; set; }

        /// <inheridoc/>
        public int? Seeds { get; set; }

        /// <inheridoc/>
        public int? Peers { get; set; }

        /// <inheridoc/>
        public int? Leeches { get; set; }

        /// <inheridoc/>
        public Quality? Quality { get; set; }

        /// <inheritdoc/>
        public bool IsPreloaded => Link != null;

        /// <inheridoc/>
        public bool IsFailed
        {
            get => Get(false);
            private set => Set(value);
        }

        public ValueTask<bool> PreloadAsync(CancellationToken cancellationToken)
        {
            if (Link != null)
            {
                return new ValueTask<bool>(true);
            }

            if (linkFactory == null)
            {
                return new ValueTask<bool>(false);
            }

            return PreloadInternalAsync(cancellationToken);

            async ValueTask<bool> PreloadInternalAsync(CancellationToken inCancellationToken)
            {
                IsLoading = true;
                try
                {
                    var link = await linkFactory(cancellationToken).ConfigureAwait(false);
                    Link = link;
                }
                finally
                {
                    IsLoading = false;
                    IsFailed = Link == null;
                }
                return Link != null;
            }
        }

        /// <inheridoc/>
        public bool Equals(TorrentFolder other)
        {
            return base.Equals(other);
        }

        /// <inheridoc/>
        public override bool Equals(object obj)
        {
            return obj is Folder another && Equals(another);
        }

        /// <inheridoc/>
        public override int GetHashCode()
        {
            if (string.IsNullOrEmpty(Id))
            {
                return base.GetHashCode();
            }

            return (Site, Id).GetHashCode();
        }

        /// <inheridoc/>
        public override string ToString()
        {
            return $"{base.ToString()}"
                + (Seeds.HasValue ? $" S:{Seeds}" : "")
                + (Peers.HasValue ? $" P:{Peers}" : "")
                + (Leeches.HasValue ? $" L:{Leeches}" : "");
        }

        public override IDictionary<string, string> GetLogProperties(bool verbose)
        {
            var props = base.GetLogProperties(verbose);

            props.Add(nameof(IsMagnet), IsMagnet.ToString());

            return props;
        }
    }
}
