namespace FSClient.Shared.Providers
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    using FSClient.Shared.Models;

    public record SearchPageParams(Site Site, Section Section) : SectionPageParams(Site, SectionPageType.Search, Section)
    {
        public SearchPageParams(
            Site site, Section section,
            DisplayItemMode displayItemMode = DisplayItemMode.Normal, int minimumRequestLength = 2,
            bool allowMultiTag = false, bool allowYearsRange = false,
            Range? yearLimit = null,
            IEnumerable<TagsContainer>? tagsContainers = null,
            IEnumerable<SortType>? sortTypes = null)
            : this(site, section)
        {
            AllowMultiTag = allowMultiTag;
            AllowYearsRange = allowYearsRange;
            YearLimit = yearLimit;
            TagsContainers = tagsContainers?.ToArray() ?? Array.Empty<TagsContainer>();
            SortTypes = sortTypes?.ToArray() ?? Array.Empty<SortType>();
            DisplayItemMode = displayItemMode;
            MinimumRequestLength = minimumRequestLength;
        }

        public DisplayItemMode DisplayItemMode { get; init; } = DisplayItemMode.Normal;

        public int MinimumRequestLength { get; init; } = 2;
    }

    public record SearchPageFilter(SearchPageParams PageParams, string SearchRequest) : BaseSectionPageFilter<SearchPageParams>(PageParams);
}
