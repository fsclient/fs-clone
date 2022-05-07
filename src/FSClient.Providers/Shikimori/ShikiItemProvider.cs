namespace FSClient.Providers
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    using FSClient.Shared.Models;
    using FSClient.Shared.Providers;
    using FSClient.Shared.Services;

    public class ShikiItemProvider : ShikiBaseSearchProvider, IItemProvider
    {
        private readonly ICacheService cacheService;

        public ShikiItemProvider(
            ShikiSiteProvider siteProvider,
            ICacheService cacheService)
            : base(siteProvider)
        {
            this.cacheService = cacheService;
        }

        public bool HasHomePage => false;

        IAsyncEnumerable<ItemInfo> IItemProvider.GetFullResult(SectionPageFilter filter)
        {
            return GetFullResultInternal(null, filter.PageParams.Section, filter.Year, filter.SelectedTags.ToList(), filter.CurrentSortType);
        }

        public Task<HomePageModel?> GetHomePageModelAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult<HomePageModel?>(null);
        }

        public async ValueTask<SectionPageParams?> GetSectionPageParamsAsync(Section section, CancellationToken cancellationToken)
        {
            var genres = await cacheService.GetOrAddAsync($"{Site.Value}_Genres", FetchGenresFromApiAsync, TimeSpan.FromDays(30), cancellationToken).ConfigureAwait(false);
            return new SectionPageParams(Site, SectionPageType.Home, section, true, true, new Range(1917, DateTime.Today.Year + 1), GetTagsContainers(genres, section), GetSortTypes(section));
        }

        public async ValueTask<SectionPageParams?> GetSectionPageParamsForTagAsync(Section section, TitledTag titledTag, CancellationToken cancellationToken)
        {
            var genres = await cacheService.GetOrAddAsync($"{Site.Value}_Genres", FetchGenresFromApiAsync, TimeSpan.FromDays(30), cancellationToken).ConfigureAwait(false);
            return new SectionPageParams(Site, SectionPageType.Tags, section, true, true, new Range(1917, DateTime.Today.Year + 1), GetTagsContainers(genres, section), GetSortTypes(section));
        }
    }
}
