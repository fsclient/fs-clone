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

    public class FindAnimeItemInfoProvider : IItemInfoProvider
    {
        private readonly FindAnimeSiteProvider siteProvider;

        public FindAnimeItemInfoProvider(FindAnimeSiteProvider siteProvider)
        {
            this.siteProvider = siteProvider;
        }

        public Site Site => siteProvider.Site;

        public bool CanPreload => false;

        public ProviderRequirements ReadRequirements => ProviderRequirements.None;

        public bool CanOpenFromLink(Uri link)
        {
            return link != null && siteProvider.Mirrors.Any(m => link.Host == m.Host || link.Host.Contains("findanime"));
        }

        public async Task<ItemInfo?> OpenFromLinkAsync(Uri link, CancellationToken cancellationToken)
        {
            if (link == null)
            {
                return null;
            }

            var domain = await siteProvider.GetMirrorAsync(cancellationToken).ConfigureAwait(false);

            link = new Uri(domain, link);
            var html = (await siteProvider
                .HttpClient
                .GetBuilder(link)
                .WithTimeSpanSemaphore(siteProvider.RequestSemaphore)
                .SendAsync(cancellationToken)
                .AsHtml(cancellationToken)
                .ConfigureAwait(false));
            if (html == null)
            {
                return null;
            }

            var id = link.Segments.Last()?.Trim('/');
            var isSerial = html.QuerySelector(".subject-meta > p > :contains('Полнометражное')") == null;

            return new ItemInfo(Site, id)
            {
                Link = new Uri(domain, id),
                Title = html.QuerySelector(".names > .name")?.TextContent?.Trim()
                    ?? html.QuerySelector("meta[itemprop=name][content]")?.GetAttribute("content"),
                Poster = html.QuerySelector(".picture-fotorama img[src]")?.GetAttribute("src")?.ToUriOrNull(domain),
                Section = Section.CreateDefault(SectionModifiers.Anime
                    | (isSerial ? SectionModifiers.Serial : SectionModifiers.Film)),
                Details =
                {
                    TitleOrigin = html.QuerySelector(".names > .original-name")?.TextContent?.Trim()
                        ?? html.QuerySelector("meta[itemprop=alternativeHeadline][content]")?.GetAttribute("content"),
                    Description = html.QuerySelector("meta[itemprop=description][content]")?.GetAttribute("content"),
                    Year = html.QuerySelector(".subject-meta > .elementList a[href*=year]")?.TextContent?.ToIntOrNull()
                }
            };
        }

        public Task<bool> PreloadItemAsync(ItemInfo item, PreloadItemStrategy preloadItemStrategy, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }
    }
}
