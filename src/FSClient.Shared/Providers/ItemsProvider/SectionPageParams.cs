namespace FSClient.Shared.Providers
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    using FSClient.Shared.Models;

    public record SectionPageParams(Site Site, SectionPageType PageType, Section Section)
    {
        public SectionPageParams(
            Site site, SectionPageType pageType, Section section,
            bool allowMultiTag = false, bool allowYearsRange = false,
            Range? yearLimit = null,
            IEnumerable<TagsContainer>? tagsContainers = null,
            IEnumerable<SortType>? sortTypes = null)
            : this(site, pageType, section)
        {
            AllowMultiTag = allowMultiTag;
            AllowYearsRange = allowYearsRange;
            YearLimit = yearLimit;
            TagsContainers = tagsContainers?.ToArray() ?? Array.Empty<TagsContainer>();
            SortTypes = sortTypes?.ToArray() ?? Array.Empty<SortType>();
        }

        public bool AllowMultiTag { get; init; }
        public bool AllowYearsRange { get; init; }

        public Range? YearLimit { get; init; }

        public IReadOnlyCollection<TagsContainer> TagsContainers { get; init; } = Array.Empty<TagsContainer>();

        public IReadOnlyCollection<SortType> SortTypes { get; init; } = Array.Empty<SortType>();
    }

    public record SectionPageFilter(SectionPageParams PageParams) : BaseSectionPageFilter<SectionPageParams>(PageParams);
}
