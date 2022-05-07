namespace FSClient.Providers
{
    using System;
    using System.Linq;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Threading.Tasks;

    using AngleSharp.Dom;

    using FSClient.Shared.Helpers;
    using FSClient.Shared.Helpers.Web;
    using FSClient.Shared.Models;
    using FSClient.Shared.Providers;

    public class AnitubeItemInfoProvider : IItemInfoProvider
    {
        private readonly AnitubeSiteProvider siteProvider;

        public AnitubeItemInfoProvider(AnitubeSiteProvider siteProvider)
        {
            this.siteProvider = siteProvider;
        }

        public Site Site => siteProvider.Site;

        public bool CanPreload => false;

        public ProviderRequirements ReadRequirements => ProviderRequirements.None;

        public bool CanOpenFromLink(Uri link)
        {
            return link != null && siteProvider.Mirrors.Any(m => link.Host == m.Host || link.Host.Contains("anitube"));
        }

        public async Task<ItemInfo?> OpenFromLinkAsync(Uri link, CancellationToken cancellationToken)
        {
            if (link == null)
            {
                return null;
            }

            var domain = await siteProvider.GetMirrorAsync(cancellationToken).ConfigureAwait(false);

            link = new Uri(domain, link);
            var html = await siteProvider
                .HttpClient
                .GetBuilder(link)
                .WithTimeSpanSemaphore(siteProvider.RequestSemaphore)
                .SendAsync(cancellationToken)
                .AsHtml(cancellationToken)
                .ConfigureAwait(false);
            if (html == null)
            {
                return null;
            }

            var id = link.GetPath().SplitLazy(2, StringSplitOptions.RemoveEmptyEntries, new[] { '/', '-' }).FirstOrDefault()?.ToIntOrNull();
            var isFilm = Regex.IsMatch(html.QuerySelector("strong:contains('Серій:')")?.NextSibling?.TextContent ?? "1 з 1", @"\b1 з 1\b");

            var fullTitle = html.QuerySelector("span[itemtype*='BreadcrumbList']")?.ChildNodes.OfType<IText>().LastOrDefault()?.Text;
            var originalTitle = html.QuerySelector("strong:contains('Оригінальна назва:')")?.NextSibling?.TextContent.Trim()
                ?? (fullTitle?.Contains(" / ") == true ? fullTitle.Split(new[] { " / " }, StringSplitOptions.RemoveEmptyEntries).LastOrDefault()?.Trim() : null);

            return new ItemInfo(Site, id.ToString())
            {
                Link = link,
                Title = html.QuerySelector(".story_c .rcol h2")?.TextContent?.Trim(),
                Poster = html.QuerySelector(".lcol span.story_post img[src]")?.GetAttribute("src")?.ToUriOrNull(domain),
                Section = Section.CreateDefault(SectionModifiers.Anime
                    | (isFilm ? SectionModifiers.Film : SectionModifiers.Serial)),
                Details =
                {
                    TitleOrigin = originalTitle,
                    Description = html.QuerySelector(".story_c_text > .my-text")?.TextContent.Trim(),
                    Year = html.QuerySelector(".story_c_r > a[href*=year]")?.TextContent?.ToIntOrNull()
                }
            };
        }

        public Task<bool> PreloadItemAsync(ItemInfo item, PreloadItemStrategy preloadItemStrategy, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }
    }
}
