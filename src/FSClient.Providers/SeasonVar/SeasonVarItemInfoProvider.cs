namespace FSClient.Providers
{
    using System;
    using System.Linq;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Threading.Tasks;

    using FSClient.Shared.Helpers;
    using FSClient.Shared.Helpers.Web;
    using FSClient.Shared.Models;
    using FSClient.Shared.Providers;

    public class SeasonVarItemInfoProvider : IItemInfoProvider
    {
        private static readonly Regex TitleCleanRegex = new Regex(@"(?:^сериал)|(?:\d+\s*сезон)|(?:онлайн$)", RegexOptions.IgnoreCase);
        private readonly SeasonVarSiteProvider siteProvider;

        public SeasonVarItemInfoProvider(SeasonVarSiteProvider seasonVarSiteProvider)
        {
            siteProvider = seasonVarSiteProvider;
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
            var nameId = link?.GetPath().Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries).LastOrDefault()?.Trim('/');
            if (nameId == null
                || !Uri.TryCreate(domain, nameId, out var parsedLink))
            {
                return null;
            }

            var id = nameId.Split('-').FirstOrDefault(part => int.TryParse(part, out _));
            if (id == null)
            {
                return null;
            }

            var page = await siteProvider
                .HttpClient
                .GetBuilder(parsedLink)
                .SendAsync(cancellationToken)
                .AsHtml(cancellationToken)
                .ConfigureAwait(false);
            if (page == null)
            {
                return null;
            }

            var genresStr = page.QuerySelector("span[itemprop='genre']")?.TextContent;
            var name = page.QuerySelector("h1[itemprop='name'], .pgs-sinfo-title")?.TextContent ?? "";

            var nameParts = TitleCleanRegex.Replace(name, "").Trim().Split('/').Select(t => t.Trim()).Where(t => !string.IsNullOrEmpty(t));

            Uri.TryCreate(link, page.QuerySelector("img[itemprop='thumbnailUrl']")?.GetAttribute("src"), out var poster);

            var firstSeasonPage = page;
            var firstSeasonLinkATag = page.QuerySelector(".pgs-seaslist a[href]");
            if (firstSeasonLinkATag?.TextContent?.StartsWith(">", StringComparison.Ordinal) == false
                && Uri.TryCreate(link, firstSeasonLinkATag.GetAttribute("href")?.TrimStart('/'), out var firstSeasonLink)
                && firstSeasonLink != link)
            {
                firstSeasonPage = await siteProvider
                    .HttpClient
                    .GetBuilder(firstSeasonLink)
                    .SendAsync(cancellationToken)
                    .AsHtml(cancellationToken)
                    .ConfigureAwait(false) ?? page;
            }

            var yearStr = firstSeasonPage
                .QuerySelectorAll(".pgs-sinfo_list > span")
                .FirstOrDefault(span => span.TextContent?.Length == 4 && int.TryParse(span.TextContent, out _))?
                .TextContent;

            return new ItemInfo(Site, id)
            {
                Link = link,
                Title = nameParts.FirstOrDefault(),
                Poster = poster,
                Section = Section.CreateDefault(
                    SectionModifiers.Serial
                    | (genresStr?.Contains("аним") ?? false ? SectionModifiers.Cartoon : SectionModifiers.None)),
                Details =
                {
                    TitleOrigin = nameParts.Skip(1).FirstOrDefault(),
                    Year = int.TryParse(yearStr, out var year) ? year : (int?)null,
                    Description = page.QuerySelector("p[itemprop='description']")?.TextContent?.Trim()
                }
            };
        }

        public Task<bool> PreloadItemAsync(ItemInfo item, PreloadItemStrategy preloadItemStrategy, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }
    }
}
