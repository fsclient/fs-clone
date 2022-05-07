namespace FSClient.Shared.Models
{
    using System;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Backuped favorite item data
    /// </summary>
    public class FavoriteBackupData
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
        /// Favorite group identity (Planned, InProgress, etc, formerly depended on <see cref="FavoriteListKind"/>)
        /// </summary>
        public string? KindNameId { get; set; }

        /// <summary>
        /// DateTime, when item was added to application favorites with specific <see cref="KindNameId"/>
        /// </summary>
        public DateTimeOffset? AddDateTime { get; set; }
    }
}
