namespace FSClient.Shared.Managers
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    using FSClient.Shared.Models;
    using FSClient.Shared.Providers;

    public interface IItemManager
    {
        Task<ItemInfo?> OpenFromLinkAsync(Uri link, CancellationToken cancellationToken);

        bool CanPreload(ItemInfo item);

        ValueTask<bool> PreloadItemAsync(ItemInfo item, PreloadItemStrategy preloadItemStrategy, CancellationToken cancellationToken);

        ValueTask<bool> IsNotItemBlockedAsync(ItemInfo item, bool checkFullBlock, CancellationToken cancellationToken);

        bool HasProviderHomePage(Site site);

        Task<HomePageModel?> GetHomePageModelAsync(Site site, CancellationToken cancellationToken);

        IAsyncEnumerable<ItemInfo> GetSectionPage(SectionPageFilter filter);

        Task<IEnumerable<SectionPageFilter>> GetSectionPageFiltersAsync(Site site, CancellationToken cancellationToken);

        Task<IEnumerable<SectionPageFilter>> GetSectionPageFiltersForTagAsync(Site site, TitledTag titledTag, CancellationToken cancellationToken);

        IAsyncEnumerable<ItemInfo> GetSearchPage(SearchPageFilter filter);

        Task<IEnumerable<SearchPageFilter>> GetSearchPageFiltersAsync(Site site, CancellationToken cancellationToken);

        IAsyncEnumerable<ItemInfo> GetShortSearchResult(string request, Site site, Section section);

        IEnumerable<ISiteProvider> GetSearchProviders();
    }
}
