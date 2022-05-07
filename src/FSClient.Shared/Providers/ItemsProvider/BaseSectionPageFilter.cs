namespace FSClient.Shared.Providers
{
    using System;
    using System.Collections.Generic;

    using FSClient.Shared.Models;

    public abstract record BaseSectionPageFilter<TSectionPageParams>(TSectionPageParams PageParams)
        where TSectionPageParams : SectionPageParams
    {
        public Range? Year { get; init; }

        public bool FilterByFavorites { get; init; }

        public bool FilterByInHistory { get; init; }

        public IReadOnlyCollection<TitledTag> SelectedTags { get; init; } = Array.Empty<TitledTag>();

        public SortType? CurrentSortType { get; init; }
    }
}
