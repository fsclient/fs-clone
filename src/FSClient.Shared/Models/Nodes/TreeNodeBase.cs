namespace FSClient.Shared.Models
{
    using System;
    using System.Collections.Generic;

    using FSClient.Shared.Mvvm;
    using FSClient.Shared.Services;

    public abstract class TreeNodeBase : BindableBase, ITreeNode, ILogState
    {
        protected TreeNodeBase(Site site, string id)
        {
            Site = site;
            Id = id;
        }

        public Site Site { get; }

        public string Id { get; protected set; }

        /// <inheridoc/>
        public IFolderTreeNode? Parent { get; set; }

        /// <inheridoc/>
        public string? Group { get; set; }

        /// <inheridoc/>
        public ItemInfo? ItemInfo { get; set; }

        /// <inheridoc/>
        public string? Title { get; set; }

        /// <inheridoc/>
        public int? Season { get; set; }

        /// <inheridoc/>
        public Range? Episode { get; set; }

        /// <inheridoc/>
        public bool IsWatched
        {
            get => Position >= 0.98;
            set
            {
                if (Set(value))
                {
                    Position = value ? 1 : 0;
                }
            }
        }

        /// <inheridoc/>
        public float Position
        {
            get => Get<float>();
            set
            {
                if (value < 0
                    || float.IsNaN(value)
                    || float.IsNegativeInfinity(value))
                {
                    value = 0;
                }
                else if (value > 1
                    || float.IsPositiveInfinity(value))
                {
                    value = 1;
                }

                if (Set(value))
                {
                    OnPropertyChanged(nameof(IsWatched));
                }
            }
        }

        /// <inheridoc/>
        public bool IsTorrent { get; set; }

        public virtual IDictionary<string, string> GetLogProperties(bool verbose)
        {
            var props = new Dictionary<string, string>
            {
                [$"Node{nameof(Site)}"] = Site.Value,
                [$"Item{nameof(Site)}"] = ItemInfo?.Site.Value ?? "Unknown",
                [nameof(IsTorrent)] = IsTorrent.ToString()
            };

            if (verbose)
            {
                props.Add(nameof(Group), Group ?? "Null");
            }

            return props;
        }
    }
}
