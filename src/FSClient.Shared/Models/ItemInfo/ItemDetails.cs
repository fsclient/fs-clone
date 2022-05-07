namespace FSClient.Shared.Models
{
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;

    using FSClient.Shared.Providers;

    /// <summary>
    /// Item info additional details.
    /// </summary>
    public class ItemDetails
    {
        public ItemDetails()
        {
            Titles = Enumerable.Empty<string>();

            Tags = Enumerable.Empty<TagsContainer>();
            Similar = Enumerable.Empty<ItemInfo>();
            Franchise = Enumerable.Empty<ItemInfo>();
            Images = Enumerable.Empty<WebImage>();
            LinkedIds = new ConcurrentDictionary<Site, string>();
        }

        /// <summary>
        /// Enumerable of localized titles.
        /// </summary>
        public IEnumerable<string> Titles { get; set; }

        /// <summary>
        /// Title on original language.
        /// </summary>
        public string? TitleOrigin { get; set; }

        /// <summary>
        /// Item description.
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// Item tag line.
        /// </summary>
        public string? TagLine { get; set; }

        /// <summary>
        /// Item year. Usualy year of first season or first show in cinema.
        /// </summary>
        public int? Year { get; set; }

        /// <summary>
        /// Item end year. Usualy year of last season.
        /// </summary>
        public int? YearEnd { get; set; }

        /// <summary>
        /// Item quality.
        /// </summary>
        public string? Quality { get; set; }

        /// <summary>
        /// Item rating implementation.
        /// </summary>
        public IRating? Rating { get; set; }

        /// <summary>
        /// Serial airing status.
        /// </summary>
        public Status Status { get; set; }

        /// <summary>
        /// Episodes lazy calendar.
        /// </summary>
        public IAsyncEnumerable<EpisodeInfo>? EpisodesCalendar { get; set; }

        /// <summary>
        /// Genres, producers, actors, countries and other possible tags.
        /// </summary>
        public IEnumerable<TagsContainer> Tags { get; set; }

        /// <summary>
        /// Enanumerable of similar items.
        /// </summary>
        public IEnumerable<ItemInfo> Similar { get; set; }

        /// <summary>
        /// Enanumerable items of franchise.
        /// </summary>
        public IEnumerable<ItemInfo> Franchise { get; set; }

        /// <summary>
        /// Enumerable of item images.
        /// </summary>
        public IEnumerable<WebImage> Images { get; set; }

        /// <summary>
        /// Dicionary of linked Site and Id pairs.
        /// </summary>
        public IDictionary<Site, string> LinkedIds { get; }
    }
}
