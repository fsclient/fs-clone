namespace FSClient.Shared.Models
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Backuped history item data
    /// </summary>
    public class HistoryBackupData
    {
        /// <summary>
        /// Id from site
        /// </summary>
        public string? Id { get; set; }

        /// <summary>
        /// Item site source
        /// </summary>
        [JsonConverter(typeof(SiteJsonConverter))]
        public Site Site { get; set; }

        /// <summary>
        /// DateTime, when item was added to application history with specific <see cref="Episode"/> and <see cref="Season"/>
        /// </summary>
        public DateTimeOffset AddDateTime { get; set; }

        /// <summary>
        /// History node episode
        /// </summary>
        [JsonConverter(typeof(RangeJsonConverter))]
        public Range? Episode { get; set; }

        /// <summary>
        /// History node season
        /// </summary>
        public int? Season { get; set; }

        /// <summary>
        /// Is torrent node history
        /// </summary>
        public bool IsTorrent { get; set; }

        /// <summary>
        /// Nodes hierarchy
        /// </summary>
        public ICollection<NodeBackupData>? Nodes { get; set; }
    }
}
