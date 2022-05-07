namespace FSClient.Shared.Models
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    using FSClient.Shared.Services;

    /// <summary>
    /// Serial or film info
    /// </summary>
    public class ItemInfo : IEquatable<ItemInfo?>, ILogState
    {
        public ItemInfo()
        {
            Details = new ItemDetails();
        }

        public ItemInfo(Site site, string? id)
            : this()
        {
            Site = site;
            SiteId = id;
        }

        /// <summary>
        /// Unique key based on provider unique code and item id from that provider.
        /// </summary>
        public string Key => $"{Site.Value}-{SiteId}";

        /// <summary>
        /// Item details.
        /// </summary>
        public ItemDetails Details { get; }

        /// <summary>
        /// Moment of item adding to application repository for history or favorites.
        /// </summary>
        public DateTimeOffset? AddTime { get; set; }

        /// <summary>
        /// Provider code.
        /// </summary>
        public Site Site { get; set; }

        /// <summary>
        /// Unique id from provider.
        /// </summary>
        public string? SiteId { get; set; }

        /// <summary>
        /// Link from provider.
        /// </summary>
        public Uri? Link { get; set; }

        /// <summary>
        /// Poster image.
        /// </summary>
        public WebImage Poster { get; set; }

        /// <summary>
        /// Section with <see cref="SectionModifiers"/>.
        /// </summary>
        public Section Section { get; set; }

        /// <summary>
        /// Localized title.
        /// </summary>
        public string? Title
        {
            get => Details.Titles.FirstOrDefault();
            set => Details.Titles = !string.IsNullOrWhiteSpace(value) ? new[] { value! } : Enumerable.Empty<string>();
        }

        public bool Equals(ItemInfo? other)
        {
            if (SiteId == null
                || other?.SiteId == null)
            {
                return false;
            }

            return Site == other.Site && SiteId == other.SiteId;
        }

        public override bool Equals(object? obj)
        {
            return obj is ItemInfo a && Equals(a);
        }

        public override int GetHashCode()
        {
            return (Site, SiteId).GetHashCode();
        }

        public IDictionary<string, string> GetLogProperties(bool verbose)
        {
            var props = new Dictionary<string, string>
            {
                [nameof(Site)] = Site.Value,
                [nameof(Section)] = Section.Modifier.ToString()
            };

            if (verbose)
            {
                props.Add(nameof(SiteId), SiteId ?? "Null");
            }

            return props;
        }

        public override string ToString()
        {
            return $"{Key}: {Title}";
        }

        public static bool operator ==(ItemInfo? left, ItemInfo? right)
        {
            return left?.Equals(right) ?? right is null;
        }

        public static bool operator !=(ItemInfo? left, ItemInfo? right)
        {
            return !(left == right);
        }
    }
}
