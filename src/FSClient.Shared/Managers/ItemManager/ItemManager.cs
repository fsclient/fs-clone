namespace FSClient.Shared.Managers
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Threading.Tasks;

    using FSClient.Shared.Helpers;
    using FSClient.Shared.Models;
    using FSClient.Shared.Providers;
    using FSClient.Shared.Repositories;
    using FSClient.Shared.Services;

    using Microsoft.Extensions.Logging;

    public sealed class ItemManager : IItemManager
    {
        private const string TagsStateKey = "FilterTags";
        private const string SortTypeStateKey = "SortType";
        private const string TagsEmptyTypeKey = "null";

        private readonly Dictionary<Site, ISiteProvider> siteProviders;
        private readonly Dictionary<Site, IItemProvider> itemProviders;
        private readonly Dictionary<Site, IItemInfoProvider> itemInfoProviders;
        private readonly Dictionary<Site, ISearchProvider> searchProviders;

        private readonly IApplicationService applicationService;
        private readonly ISettingService settingService;
        private readonly IFavoriteRepository favoriteRepository;
        private readonly IHistoryManager historyManager;
        private readonly IUserManager userManager;
        private readonly IHistoryRepository historyRepository;
        private readonly IItemInfoRepository itemInfoRepository;
        private readonly ILogger logger;

        public ItemManager(
            IEnumerable<ISiteProvider> siteProviders,
            IEnumerable<IItemProvider> itemProviders,
            IEnumerable<IItemInfoProvider> itemInfoProviders,
            IEnumerable<ISearchProvider> searchProviders,
            IApplicationService applicationService,
            IHistoryManager historyManager,
            IUserManager userManager,
            ISettingService settingService,
            IHistoryRepository historyRepository,
            IFavoriteRepository favoriteRepository,
            IItemInfoRepository itemInfoRepository,
            ILogger logger)
        {
            this.siteProviders = siteProviders.ToDictionary(k => k.Site, v => v);
            this.itemProviders = itemProviders.ToDictionary(k => k.Site, v => v);
            this.itemInfoProviders = itemInfoProviders.ToDictionary(k => k.Site, v => v);
            this.searchProviders = searchProviders.ToDictionary(k => k.Site, v => v);

            this.favoriteRepository = favoriteRepository;
            this.historyManager = historyManager;
            this.settingService = settingService;
            this.applicationService = applicationService;
            this.userManager = userManager;
            this.historyRepository = historyRepository;
            this.itemInfoRepository = itemInfoRepository;
            this.logger = logger;
        }

        public async Task<ItemInfo?> OpenFromLinkAsync(Uri link, CancellationToken cancellationToken)
        {
            var provider = itemInfoProviders.Values.FirstOrDefault(p => p.CanOpenFromLink(link));
            if (provider != null)
            {
                var isAllowed = await userManager.CheckRequirementsAsync(provider.Site, provider.ReadRequirements, cancellationToken).ConfigureAwait(false);
                if (isAllowed)
                {
                    return await provider.OpenFromLinkAsync(link, cancellationToken).ConfigureAwait(false);
                }
            }
            return null;
        }

        public bool CanPreload(ItemInfo item)
        {
            return item != null && itemInfoProviders.TryGetValue(item.Site, out var provider) && provider.CanPreload;
        }

        public ValueTask<bool> PreloadItemAsync(ItemInfo item, PreloadItemStrategy preloadItemStrategy, CancellationToken cancellationToken)
        {
            if (item is null)
            {
                return new ValueTask<bool>(false);
            }
            if (!CanPreload(item))
            {
                return new ValueTask<bool>(false);
            }

            return PreloadItemAsyncInternal();

            async ValueTask<bool> PreloadItemAsyncInternal()
            {
                try
                {
                    var provider = itemInfoProviders[item.Site];
                    var isAllowed = await userManager.CheckRequirementsAsync(provider.Site, provider.ReadRequirements, cancellationToken).ConfigureAwait(false);
                    if (!isAllowed)
                    {
                        return false;
                    }

                    var oldPoster = item.Poster;
                    var preloaded = await provider.PreloadItemAsync(item, preloadItemStrategy, cancellationToken).ConfigureAwait(false);


                    if (oldPoster[ImageSize.Preview] != item.Poster[ImageSize.Preview])
                    {
                        _ = EnsureActualItemInfoInRepositoriesAsync(item);
                    }

                    return preloaded;
                }
                catch (OperationCanceledException) { }
                return false;
            }
        }

        public bool HasProviderHomePage(Site site)
        {
            return itemProviders.TryGetValue(site, out var itemProvider)
                && itemProvider.HasHomePage;
        }

        public async Task<HomePageModel?> GetHomePageModelAsync(Site site, CancellationToken cancellationToken)
        {
            if (!itemProviders.TryGetValue(site, out var itemProvider))
            {
                return null;
            }
            try
            {
                var isAllowed = await userManager.CheckRequirementsAsync(itemProvider.Site, itemProvider.ReadRequirements, cancellationToken).ConfigureAwait(false);
                if (!isAllowed)
                {
                    return null;
                }

                var homePage = await itemProvider.GetHomePageModelAsync(cancellationToken).ConfigureAwait(false);
                if (homePage == null)
                {
                    return null;
                }

                var settings = await applicationService.GetBlockListSettingsAsync(cancellationToken).ConfigureAwait(false);
                var blockList = settings.FullBlockedIds ?? Enumerable.Empty<string>();
                var filterRegex = Settings.Instance.IncludeAdult ? null
                    : settings.FilterRegexes?.ItemsByTitleAdultFilter;

                homePage.TopItems = homePage.TopItems
                    .Where(item => !blockList.Contains(item.Key)
                        && (filterRegex == null || !filterRegex.IsMatch(item.Title)));

                homePage.HomeItems = homePage.HomeItems
                    .SelectMany(g => g.Select(item => (g.Key, item)))
                    .Where(t => !blockList.Contains(t.item.Key)
                        && (filterRegex == null || !filterRegex.IsMatch(t.item.Title)))
                    .GroupBy(t => t.Key, t => t.item);

                return homePage;
            }
            catch (OperationCanceledException) { }

            return null;
        }

        public async Task<IEnumerable<SectionPageFilter>> GetSectionPageFiltersAsync(Site site, CancellationToken cancellationToken)
        {
            if (!itemProviders.TryGetValue(site, out var itemProvider))
            {
                return Enumerable.Empty<SectionPageFilter>();
            }
            var isAllowed = await userManager.CheckRequirementsAsync(itemProvider.Site, itemProvider.ReadRequirements, cancellationToken).ConfigureAwait(false);
            if (!isAllowed)
            {
                return Enumerable.Empty<SectionPageFilter>();
            }

            return await itemProvider.Sections
                .ToAsyncEnumerable()
                .SelectAwaitWithCancellation((s, ct) => itemProvider.GetSectionPageParamsAsync(s, ct))
                .Where(pageParams => pageParams != null)
                .Select(pageParams => new SectionPageFilter(pageParams!)
                {
                    CurrentSortType = ReadSortTypeFromCache(pageParams!),
                    SelectedTags = ReadTitledTagsFromCache(pageParams!).ToArray(),
                    FilterByFavorites = ReadBoolValueFromCache(pageParams!, nameof(SectionPageFilter.FilterByFavorites)),
                    FilterByInHistory = ReadBoolValueFromCache(pageParams!, nameof(SectionPageFilter.FilterByInHistory)),
                    Year = null
                })
                .ToArrayAsync(cancellationToken)
                .ConfigureAwait(false);
        }

        public async Task<IEnumerable<SectionPageFilter>> GetSectionPageFiltersForTagAsync(Site site, TitledTag titledTag, CancellationToken cancellationToken)
        {
            if (!itemProviders.TryGetValue(site, out var itemProvider))
            {
                return Enumerable.Empty<SectionPageFilter>();
            }
            var isAllowed = await userManager.CheckRequirementsAsync(itemProvider.Site, itemProvider.ReadRequirements, cancellationToken).ConfigureAwait(false);
            if (!isAllowed)
            {
                return Enumerable.Empty<SectionPageFilter>();
            }

            return await itemProvider.Sections
                .ToAsyncEnumerable()
                .SelectAwaitWithCancellation((s, ct) => itemProvider.GetSectionPageParamsForTagAsync(s, titledTag, ct))
                .Where(pageParams => pageParams != null)
                // We don't read cache for section pages with tags.
                .Select(pageParams => new SectionPageFilter(pageParams!)
                {
                    SelectedTags = new[] { titledTag }
                })
                .ToArrayAsync(cancellationToken)
                .ConfigureAwait(false);
        }

        public IAsyncEnumerable<ItemInfo> GetSectionPage(SectionPageFilter filter)
        {
            if (!itemProviders.TryGetValue(filter.PageParams.Site, out var itemProvider))
            {
                return AsyncEnumerable.Empty<ItemInfo>();
            }
            SavePageFilterState(filter);

            var enumerable = itemProvider.GetFullResult(filter);

            if (filter.FilterByFavorites)
            {
                enumerable = enumerable.WhereAwaitWithCancellation(async (item, ct) =>
                    !await favoriteRepository.GetFavoritesByItems(new[] { item.Key }).AnyAsync(ct).ConfigureAwait(false));
            }
            if (filter.FilterByInHistory)
            {
                enumerable = enumerable.WhereAwait(async item =>
                    !await historyManager.IsInHistoryAsync(item).ConfigureAwait(false));
            }

            return enumerable
                .WhereAwaitWithCancellation((i, ct) => IsNotItemBlockedAsync(i, true, ct));
        }

        public async Task<IEnumerable<SearchPageFilter>> GetSearchPageFiltersAsync(Site site, CancellationToken cancellationToken)
        {
            if (!searchProviders.TryGetValue(site, out var searchProvider))
            {
                return Enumerable.Empty<SearchPageFilter>();
            }
            var isAllowed = await userManager.CheckRequirementsAsync(searchProvider.Site, searchProvider.ReadRequirements, cancellationToken).ConfigureAwait(false);
            if (!isAllowed)
            {
                return Enumerable.Empty<SearchPageFilter>();
            }

            return await searchProvider.Sections
                .ToAsyncEnumerable()
                .SelectAwaitWithCancellation((s, ct) => searchProvider.GetSearchPageParamsAsync(s, ct))
                .Where(pageParams => pageParams != null)
                .Select(pageParams => new SearchPageFilter(pageParams!, string.Empty)
                {
                    CurrentSortType = ReadSortTypeFromCache(pageParams!),
                    SelectedTags = ReadTitledTagsFromCache(pageParams!).ToArray(),
                    FilterByFavorites = ReadBoolValueFromCache(pageParams!, nameof(SectionPageFilter.FilterByFavorites)),
                    FilterByInHistory = ReadBoolValueFromCache(pageParams!, nameof(SectionPageFilter.FilterByInHistory)),
                    Year = null
                })
                .ToArrayAsync(cancellationToken)
                .ConfigureAwait(false);
        }

        public IAsyncEnumerable<ItemInfo> GetSearchPage(SearchPageFilter filter)
        {
            if (!searchProviders.TryGetValue(filter.PageParams.Site, out var searchProvider))
            {
                return AsyncEnumerable.Empty<ItemInfo>();
            }
            SavePageFilterState(filter);

            var enumerable = searchProvider.GetFullResult(filter);

            if (filter.FilterByFavorites)
            {
                enumerable = enumerable.WhereAwaitWithCancellation(async (item, ct) =>
                    !await favoriteRepository.GetFavoritesByItems(new[] { item.Key }).AnyAsync(ct).ConfigureAwait(false));
            }
            if (filter.FilterByInHistory)
            {
                enumerable = enumerable.WhereAwait(async item =>
                    !await historyManager.IsInHistoryAsync(item).ConfigureAwait(false));
            }

            return enumerable
                .Take(60)
                .WhereAwaitWithCancellation((i, ct) => IsNotItemBlockedNorSearchInputAsync(i, true, filter.SearchRequest, ct));
        }

        public IAsyncEnumerable<ItemInfo> GetShortSearchResult(string request, Site site, Section section)
        {
            return searchProviders.TryGetValue(site, out var searchProvider)
                ? searchProvider.GetShortResult(request, section)
                    .Take(20)
                    .TakeWhileAwaitWithCancellation((_, ct) => userManager.CheckRequirementsAsync(searchProvider.Site, searchProvider.ReadRequirements, ct))
                    .WhereAwaitWithCancellation((i, ct) => IsNotItemBlockedNorSearchInputAsync(i, true, request, ct))
                    .Take(10)
                : AsyncEnumerable.Empty<ItemInfo>();
        }

        public IEnumerable<ISiteProvider> GetSearchProviders()
        {
            return siteProviders
                .Where(p => searchProviders.TryGetValue(p.Key, out var searchProvider)
                    && searchProvider.Sections.Count > 0)
                .Select(p => p.Value);
        }

        public ValueTask<bool> IsNotItemBlockedAsync(ItemInfo item, bool checkFullBlock, CancellationToken cancellationToken)
        {
            return IsNotItemBlockedNorSearchInputAsync(item, checkFullBlock, null, cancellationToken);
        }

        private ValueTask<bool> IsNotItemBlockedNorSearchInputAsync(
            ItemInfo item, bool checkFullBlock, string? searchString, CancellationToken cancellationToken)
        {
            var settingsTask = applicationService.GetBlockListSettingsAsync(cancellationToken);
            if (settingsTask.IsCompleted && !settingsTask.IsFaulted && !settingsTask.IsCanceled)
            {
                return new ValueTask<bool>(CheckItem(settingsTask.Result));
            }
            return IsNotItemBlockedNorSearchInputInternalAsync();

            async ValueTask<bool> IsNotItemBlockedNorSearchInputInternalAsync()
            {
                var settings = await settingsTask.ConfigureAwait(false);
                return CheckItem(settings);
            }

            bool CheckItem(BlockListSettings? settings)
            {
                if (settings == null)
                {
                    return true;
                }
                if (checkFullBlock)
                {
                    if (!Settings.Instance.IncludeAdult)
                    {
                        if (settings.FilterRegexes?.ItemsByTitleAdultFilter is Regex itemsByTitleFilter
                            && item.Title is string title
                            && itemsByTitleFilter.IsMatch(title.GetLetters()))
                        {
                            return false;
                        }
                        if (settings.FilterRegexes?.SearchInputAdultFilter is Regex searchInputFilter
                            && searchString != null
                            && searchInputFilter.IsMatch(searchString.GetLetters()))
                        {
                            throw new OperationCanceledException();
                        }
                    }

                    return !settings.FullBlockedIds.Contains(item.Key);
                }
                else
                {
                    return !settings.BlockedIds.Contains(item.Key);
                }
            }
        }

        private static string GetFilterStateKey(SectionPageParams filter, string field)
        {
            return $"{field}_{filter.Site.Value}_{filter.Section.Value}"
                + (filter.PageType == SectionPageType.Home ? "" : "_" + filter.PageType.ToString().ToLower());
        }

        private void SavePageFilterState<TSectionPageParams>(BaseSectionPageFilter<TSectionPageParams> filter)
            where TSectionPageParams : SectionPageParams
        {
            if (filter.PageParams.PageType == SectionPageType.Tags)
            {
                return;
            }

            if (filter.SelectedTags.Any())
            {
                var tagsSetting = QueryStringHelper.CreateQueryString(filter.SelectedTags.Select(t => new KeyValuePair<string, string?>(t.Type ?? TagsEmptyTypeKey, t.Value)));
                settingService.SetSetting(Settings.StateSettingsContainer,
                    GetFilterStateKey(filter.PageParams, TagsStateKey), tagsSetting);
            }
            else
            {
                settingService.DeleteSetting(Settings.StateSettingsContainer, GetFilterStateKey(filter.PageParams, TagsStateKey));
            }

            if (filter.FilterByFavorites)
            {
                settingService.SetSetting(Settings.StateSettingsContainer,
                    GetFilterStateKey(filter.PageParams, nameof(SectionPageFilter.FilterByFavorites)), filter.FilterByFavorites);
            }
            else
            {
                settingService.DeleteSetting(Settings.StateSettingsContainer,
                    GetFilterStateKey(filter.PageParams, nameof(SectionPageFilter.FilterByFavorites)));
            }

            if (filter.FilterByInHistory)
            {
                settingService.SetSetting(Settings.StateSettingsContainer,
                    GetFilterStateKey(filter.PageParams, nameof(SectionPageFilter.FilterByInHistory)), filter.FilterByInHistory);
            }
            else
            {
                settingService.DeleteSetting(Settings.StateSettingsContainer,
                    GetFilterStateKey(filter.PageParams, nameof(SectionPageFilter.FilterByInHistory)));
            }

            if (filter.CurrentSortType is SortType sortType)
            {
                settingService.SetSetting(Settings.StateSettingsContainer,
                    GetFilterStateKey(filter.PageParams, SortTypeStateKey), (int)sortType);
            }
            else
            {
                settingService.DeleteSetting(Settings.StateSettingsContainer,
                    GetFilterStateKey(filter.PageParams, SortTypeStateKey));
            }
        }

        private IEnumerable<TitledTag> ReadTitledTagsFromCache(SectionPageParams pageParams)
        {
            var selectedTagValues = QueryStringHelper.ParseQuery(settingService
               .GetSetting(Settings.StateSettingsContainer, GetFilterStateKey(pageParams, TagsStateKey), string.Empty))
               .ToArray();
            return pageParams.TagsContainers.SelectMany(container => container.Items)
                .Where(tag => selectedTagValues.Any(p => p.Key == (tag.Type ?? TagsEmptyTypeKey) && p.Value == tag.Value))
                .Distinct()
                .ToArray();
        }

        private SortType? ReadSortTypeFromCache(SectionPageParams pageParams)
        {
            var sortTypeNum = settingService.GetSetting(Settings.StateSettingsContainer,
                GetFilterStateKey(pageParams, SortTypeStateKey), -1);
            return sortTypeNum > 0 && pageParams.SortTypes.IndexOf((SortType)sortTypeNum) >= 0
                ? (SortType)sortTypeNum
                : (SortType?)null;
        }

        private bool ReadBoolValueFromCache(SectionPageParams pageParams, string fieldName)
        {
            return settingService.GetSetting(Settings.StateSettingsContainer,
                GetFilterStateKey(pageParams, fieldName), false);
        }

        private async Task EnsureActualItemInfoInRepositoriesAsync(ItemInfo itemInfo)
        {
            try
            {
                var historyItems = await historyRepository.GetOrderedHistory(itemInfo.Key).ToListAsync().ConfigureAwait(false);
                var historyToUpdate = new List<HistoryItem>();
                foreach (var historyItem in historyItems)
                {
                    historyItem.ItemInfo.Poster = itemInfo.Poster;
                    historyToUpdate.Add(historyItem);
                }
                await historyRepository.UpsertManyAsync(historyItems).ConfigureAwait(false);

                var favoriteItems = await favoriteRepository.GetFavoritesByItems(new[] { itemInfo.Key }).ToListAsync().ConfigureAwait(false);
                var favoritesToUpdate = new List<FavoriteItem>();
                foreach (var favoriteItem in favoriteItems)
                {
                    favoriteItem.ItemInfo.Poster = itemInfo.Poster;
                    favoritesToUpdate.Add(favoriteItem);
                }
                await favoriteRepository.UpsertManyAsync(favoritesToUpdate).ConfigureAwait(false);


                var item = await itemInfoRepository.GetAsync(itemInfo.Key).ConfigureAwait(false);
                if (item != null)
                {
                    item.Poster = itemInfo.Poster;
                    await itemInfoRepository.UpsertManyAsync(new[] { item }).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex);
            }
        }
    }
}
