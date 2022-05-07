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

    public class KinoUkrItemInfoProvider : IItemInfoProvider
    {
        private readonly KinoUkrSiteProvider siteProvider;

        public KinoUkrItemInfoProvider(KinoUkrSiteProvider zonaSiteProvider)
        {
            siteProvider = zonaSiteProvider;
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
            if (link == null)
            {
                return null;
            }

            var domain = await siteProvider.GetMirrorAsync(cancellationToken).ConfigureAwait(false);

            link = new Uri(domain, link);
            var id = link.Segments.LastOrDefault()?.Split('-').First().GetDigits().ToIntOrNull();
            if (id == null)
            {
                return null;
            }

            var html = (await siteProvider
                .HttpClient
                .GetBuilder(link)
                .SendAsync(cancellationToken)
                .AsHtml(cancellationToken)
                .ConfigureAwait(false))?
                .QuerySelector(".fpage");
            if (html == null)
            {
                return null;
            }

            var genreContent = html.QuerySelector(".finfo .sd-line:has(> span:contains('Жанр'))")?.TextContent;
            var isSerial = genreContent?.IndexOf("серіал", StringComparison.CurrentCultureIgnoreCase) > 0;
            var isCartoon = genreContent?.IndexOf("мульт", StringComparison.CurrentCultureIgnoreCase) > 0
                || genreContent?.IndexOf("анім", StringComparison.CurrentCultureIgnoreCase) > 0;

            var item = new ItemInfo(Site, id.ToString())
            {
                Link = link,
                Title = html.QuerySelector(".ftitle h1")?.TextContent.Trim(),
                Poster = html.QuerySelector(".fposter img[src]")?.GetAttribute("src")?.ToUriOrNull(domain),
                Section = Section.CreateDefault(
                    (isSerial ? SectionModifiers.Serial : SectionModifiers.Film)
                    | (isCartoon ? SectionModifiers.Cartoon : SectionModifiers.None)),
                Details =
                {
                    TitleOrigin = html.QuerySelector(".ftitle .foriginal")?.TextContent.Trim(),
                    Description = html.QuerySelector(".fdesc")?.TextContent.Trim(),
                    Year = html.QuerySelector(".finfo .sd-line:has(> span:contains('Рік')) a")?.TextContent?.ToIntOrNull()
                }
            };

            return item;
        }

        public Task<bool> PreloadItemAsync(ItemInfo item, PreloadItemStrategy preloadItemStrategy, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }
    }
}
