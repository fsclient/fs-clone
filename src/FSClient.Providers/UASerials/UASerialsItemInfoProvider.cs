namespace FSClient.Providers
{
    using System;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    using FSClient.Shared.Helpers;
    using FSClient.Shared.Helpers.Web;
    using FSClient.Shared.Models;
    using FSClient.Shared.Providers;

    public class UASerialsItemInfoProvider : IItemInfoProvider
    {
        private readonly UASerialsSiteProvider siteProvider;

        public UASerialsItemInfoProvider(UASerialsSiteProvider siteProvider)
        {
            this.siteProvider = siteProvider;
        }

        public Site Site => siteProvider.Site;

        public bool CanPreload => false;

        public ProviderRequirements ReadRequirements => ProviderRequirements.None;

        public bool CanOpenFromLink(Uri link)
        {
            return link != null && siteProvider.Mirrors.Any(m => link.Host == m.Host);
        }

        public async Task<ItemInfo?> OpenFromLinkAsync(Uri link, CancellationToken cancellationToken)
        {
            var domain = await siteProvider.GetMirrorAsync(cancellationToken).ConfigureAwait(false);
            var id = link?.GetPath().SplitLazy(2, StringSplitOptions.RemoveEmptyEntries, new[] { '/', '-' }).FirstOrDefault()?.ToIntOrNull();
            if (!id.HasValue
                || !Uri.TryCreate(domain, link, out link))
            {
                return null;
            }

            var itemInfo = new UASerialsItemInfo(Site, id.ToString())
            {
                Link = link,
                Section = Section.CreateDefault(SectionModifiers.Serial)
            };

            var preloaded = await PreloadItemAsync(itemInfo, PreloadItemStrategy.Poster, cancellationToken).ConfigureAwait(false);
            if (preloaded)
            {
                return itemInfo;
            }
            {
                return null;
            }
        }

        public async Task<bool> PreloadItemAsync(ItemInfo item, PreloadItemStrategy preloadItemStrategy, CancellationToken cancellationToken)
        {
            var domain = await siteProvider.GetMirrorAsync(cancellationToken).ConfigureAwait(false);
            var id = item.Link?.GetPath().SplitLazy(2, StringSplitOptions.RemoveEmptyEntries, new[] { '/', '-' }).FirstOrDefault()?.ToIntOrNull();
            if (!id.HasValue
                || !Uri.TryCreate(domain, item.Link, out var link))
            {
                return false;
            }

            var html = await siteProvider.HttpClient
                .GetBuilder(link)
                .WithHeader("Referer", domain.ToString())
                .SendAsync(cancellationToken)
                .AsHtml(cancellationToken)
                .ConfigureAwait(false);

            var serialDesc = html?.QuerySelector("[itemtype='http://schema.org/Article']");
            if (serialDesc == null)
            {
                return false;
            }

            var poster = serialDesc.QuerySelector(".fimg img[src]")?.GetAttribute("src")?.ToUriOrNull(link);
            var desc = serialDesc.QuerySelector(".ftext.full-text")?.TextContent.Trim();
            var year = serialDesc.QuerySelector(".short-list li a[href*=year]")?.TextContent?.ToIntOrNull();
            var ruTitle = serialDesc.QuerySelector(".oname_ua")?.TextContent?.Trim();
            var enTitle = serialDesc.QuerySelector(".oname")?.TextContent?.Trim();

            item.Link = link;
            item.Poster = poster;
            item.Title = ruTitle;
            item.Section = Section.CreateDefault(SectionModifiers.Serial);
            item.Details.TitleOrigin = enTitle;
            item.Details.Year = year;
            item.Details.Description = desc;

            if (item is UASerialsItemInfo uaSerialsItem)
            {
                uaSerialsItem.Translation = serialDesc.QuerySelector(".short-list li:contains('Переклад:')")?.LastChild?.TextContent;
                uaSerialsItem.DataTag = serialDesc.QuerySelector("player-control[data-tag]")?.GetAttribute("data-tag");
            }

            return true;
        }
    }
}
