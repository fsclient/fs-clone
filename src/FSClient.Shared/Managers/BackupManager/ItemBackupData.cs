namespace FSClient.Shared.Models
{
    using System;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Backuped item data
    /// </summary>
    public class ItemBackupData
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
        /// DateTime, when item was added to application as history or favorite
        /// </summary>
        public DateTimeOffset? AddDateTime { get; set; }

        /// <summary>
        /// Item title (translated)
        /// </summary>
        public string? Title { get; set; }

        /// <summary>
        /// Relative link to item
        /// </summary>
        public Uri? Link { get; set; }

        /// <summary>
        /// Item preview-poster
        /// </summary>
        [JsonConverter(typeof(WebImageJsonConverter))]
        public WebImage Poster { get; set; }

        /// <summary>
        /// Item section
        /// </summary>
        [JsonConverter(typeof(SectionJsonConverter))]
        public Section Section { get; set; }
    }
}
