namespace FSClient.Data.Repositories
{
    using System;
    using System.Text.Json.Serialization;
    using System.Collections.Generic;

    using FSClient.Shared.Models;

    public class HistoryJsonEntity
    {
        [JsonConverter(typeof(SiteJsonConverter))]
        public Site Site { get; set; }

        [JsonPropertyName("Id")]
        public string? SiteId { get; set; }

        public string? FileId { get; set; }

        [JsonPropertyName("Date")]
        [JsonConverter(typeof(JsonMicrosoftDateTimeOffsetConverter))]
        public DateTimeOffset UpdateTime { get; set; }

        [JsonConverter(typeof(JsonMicrosoftDateTimeOffsetConverter))]
        public DateTimeOffset AddTime { get; set; }

        [JsonPropertyName("Pos")]
        public float Position { get; set; }

        [JsonPropertyName("S")]
        public int? Season { get; set; }

        [JsonConverter(typeof(RangeJsonConverter))]
        [JsonPropertyName("Ep")]
        public virtual Range? Episode { get; set; }

        public string? Title { get; set; }

        [JsonConverter(typeof(WebImageJsonConverter))]
        public WebImage Poster { get; set; }

        public Uri? Link { get; set; }

        public bool IsTorrent { get; set; }

        public IEnumerable<string>? Folders { get; set; }

        public override string ToString()
        {
            return $"{Site.Value}-{SiteId}-{Season}-{Episode}: {Title}";
        }
    }
}
