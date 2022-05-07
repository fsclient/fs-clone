namespace FSClient.Shared.Models
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    public class HistoryItemRange : HistoryItem
    {
        public IReadOnlyList<HistoryItem> Items { get; }

        public HistoryItem? LatestItem => Items.OrderByDescending(i => i.AddTime).FirstOrDefault();

        public float Position { get; }

        public HistoryItemRange(IEnumerable<HistoryItem> historyItems, bool showLatestWatchedEpisode = false)
            : base(historyItems.FirstOrDefault()?.ItemInfo!, historyItems.LastOrDefault()?.Node!)
        {
            if (historyItems == null)
            {
                throw new ArgumentNullException(nameof(historyItems));
            }

            Items = historyItems
                .SelectMany(item => item is HistoryItemRange range
                    ? range.Items
                    : new[] { item })
                .OrderBy(e => e.Episode?.Start.Value)
                .ToList();

            if (Items.Count == 0)
            {
                return;
            }

            var first = Items.First();
            var last = Items.Last();
            var latestWached = Items.OrderByDescending(i => i.AddTime).First();
            IsTorrent = Items.All(e => e.IsTorrent);
            AddTime = Items.Select(e => e.AddTime).Max();
            Position = Items.Sum(e => e.Node?.Position ?? 0) / Items.Count;
            Season = showLatestWatchedEpisode ? latestWached.Season : last.Season;
            Episode = showLatestWatchedEpisode ? latestWached.Episode
                : Items.All(e => e.Episode.HasValue) ? new Range(first.Episode!.Value.Start, last.Episode!.Value.End)
                : (Range?)null;
        }
    }
}
