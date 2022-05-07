namespace FSClient.Shared.Models
{
    using System;
    using System.Collections.Generic;

    using FSClient.Shared.Helpers;
    using FSClient.Shared.Services;

    public class HistoryItem : IEquatable<HistoryItem>, ILogState
    {
#nullable disable
        private HistoryItem()
#nullable restore
        {

        }

        public HistoryItem(ItemInfo itemInfo, HistoryNode? node)
        {
            ItemInfo = itemInfo ?? throw new ArgumentNullException(nameof(itemInfo));
            Node = node;
            AddTime = DateTimeOffset.Now;
        }

        public ItemInfo ItemInfo { get; private set; }

        public HistoryNode? Node { get; private set; }

        public string Key => $"{ItemInfo.Key}-{Season}-{Episode?.ToFormattedString()}";

        public DateTimeOffset AddTime { get; set; }

        public int? Season { get; set; }

        public virtual Range? Episode { get; set; }

        public bool AutoStart { get; set; }

        public bool IsTorrent { get; set; }

        public bool IsSimilar(HistoryItem historyItem)
        {
            return historyItem != null
                && historyItem.ItemInfo?.SiteId == ItemInfo?.SiteId
                && historyItem.ItemInfo?.Site == ItemInfo?.Site
                && historyItem.Season == Season
                && ((historyItem.Episode.HasValue
                    && Episode.HasValue
                    && historyItem.Episode.Value.IsIntersected(Episode.Value))
                    || (!historyItem.Episode.HasValue
                        && !Episode.HasValue));
        }

        public override string ToString()
        {
            return $"{Key}: {ItemInfo?.Title}";
        }

        public override bool Equals(object obj)
        {
            return obj is HistoryItem historyItem && Equals(historyItem);
        }

        public bool Equals(HistoryItem? historyItem)
        {
            return historyItem?.ItemInfo?.Equals(ItemInfo) == true
                && historyItem.Season == Season
                && historyItem.Episode.Equals(Episode)
                && historyItem.Node?.Key == Node?.Key;
        }

        public override int GetHashCode()
        {
            return (ItemInfo, Episode, Season, Node?.Key).GetHashCode();
        }

        public IDictionary<string, string> GetLogProperties(bool verbose)
        {
            return ItemInfo.GetLogProperties(verbose);
        }

        public static bool operator ==(HistoryItem? left, HistoryItem? right)
        {
            return left?.Equals(right) ?? right is null;
        }

        public static bool operator !=(HistoryItem? left, HistoryItem? right)
        {
            return !(left == right);
        }
    }
}
