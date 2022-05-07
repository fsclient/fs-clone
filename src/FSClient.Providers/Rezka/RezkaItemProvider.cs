namespace FSClient.Providers
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Runtime.CompilerServices;
    using System.Threading;
    using System.Threading.Tasks;

    using FSClient.Localization.Resources;
    using FSClient.Shared.Helpers;
    using FSClient.Shared.Helpers.Web;
    using FSClient.Shared.Models;
    using FSClient.Shared.Providers;

    public class RezkaItemProvider : IItemProvider
    {
        private readonly RezkaSiteProvider siteProvider;

        public RezkaItemProvider(RezkaSiteProvider rezkaSiteProvider)
        {
            siteProvider = rezkaSiteProvider;
        }

        public IReadOnlyList<Section> Sections { get; } = new List<Section>
        {
            Section.Any,
            Section.CreateDefault(SectionModifiers.Film),
            Section.CreateDefault(SectionModifiers.Serial),
            Section.CreateDefault(SectionModifiers.Cartoon),
            Section.CreateDefault(SectionModifiers.Anime),
        };

        public Site Site => siteProvider.Site;

        public bool HasHomePage => true;

        public ProviderRequirements ReadRequirements => ProviderRequirements.None;

        public async Task<HomePageModel?> GetHomePageModelAsync(CancellationToken cancellationToken)
        {
            var domain = await siteProvider.GetMirrorAsync(cancellationToken).ConfigureAwait(false);
            var html = await siteProvider.HttpClient.GetBuilder(domain).SendAsync(cancellationToken).AsHtml(cancellationToken).ConfigureAwait(false);
            if (html == null)
            {
                return null;
            }

            var model = new HomePageModel(Site, Strings.HomePageModel_Caption);

            model.TopItemsCaption = Strings.HomePageModel_Popular;
            model.TopItems = html.QuerySelectorAll(".b-newest_slider__list .b-content__inline_item[data-id][data-url]")
                .Select(htmlItem => RezkaItemInfoProvider.ParseItemInfoFromTileHtml(Site, domain, htmlItem))
                .Where(item => !string.IsNullOrEmpty(item?.SiteId));

            model.HomeItems = html.QuerySelectorAll(".b-content__inline .b-content__inline_item[data-id][data-url]")
                .Select(htmlItem => RezkaItemInfoProvider.ParseItemInfoFromTileHtml(Site, domain, htmlItem))
                .Where(item => !string.IsNullOrEmpty(item?.SiteId))
                .GroupBy(_ => Strings.HomePageModel_New);

            return model;
        }

        public ValueTask<SectionPageParams?> GetSectionPageParamsAsync(Section section, CancellationToken cancellationToken)
        {
            return new ValueTask<SectionPageParams?>(new SectionPageParams(Site, SectionPageType.Home, section));
        }

        public ValueTask<SectionPageParams?> GetSectionPageParamsForTagAsync(Section section, TitledTag titledTag, CancellationToken cancellationToken)
        {
            return new ValueTask<SectionPageParams?>(new SectionPageParams(Site, SectionPageType.Tags, section));
        }

        public IAsyncEnumerable<ItemInfo> GetFullResult(SectionPageFilter filter)
        {
            return GetFullResultInternal(filter);
        }

        public async IAsyncEnumerable<ItemInfo> GetFullResultInternal(
            SectionPageFilter filter, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var section = filter.PageParams.Section.Modifier.HasFlag(SectionModifiers.Anime) ? "animation"
                : filter.PageParams.Section.Modifier.HasFlag(SectionModifiers.Cartoon) ? "cartoons"
                : filter.PageParams.Section.Modifier.HasFlag(SectionModifiers.Serial) ? "series"
                : filter.PageParams.Section.Modifier.HasFlag(SectionModifiers.Film) ? "films"
                : null;
            if (section == null)
            {
                yield break;
            }

            int page = 0, maxPage = 1;

            while (page < maxPage)
            {
                var currentPage = ++page;

                var domain = await siteProvider.GetMirrorAsync(cancellationToken).ConfigureAwait(false);

                var html = (await siteProvider
                    .HttpClient
                    .GetBuilder(new Uri(domain, $"{section}/page/{currentPage}/"))
                    .WithHeader("Referer", domain.ToString())
                    .SendAsync(cancellationToken)
                    .AsHtml(cancellationToken)
                    .ConfigureAwait(false))?
                    .QuerySelector(".b-content__inline_items");

                if (html == null)
                {
                    yield break;
                }

                maxPage = html.QuerySelectorAll(".b-navigation a[href*=page]")
                    .Select(a => a.TextContent?.ToIntOrNull())
                    .Where(page => page.HasValue)
                    .OrderByDescending(page => page)
                    .FirstOrDefault() ?? -1;

                var items = html.QuerySelectorAll(".b-content__inline_item[data-id][data-url]")
                    .Select(htmlItem => RezkaItemInfoProvider.ParseItemInfoFromTileHtml(Site, domain, htmlItem))
                    .Where(item => !string.IsNullOrEmpty(item.SiteId));

                foreach (var item in items)
                {
                    yield return item!;
                }
            }
        }
    }
}
