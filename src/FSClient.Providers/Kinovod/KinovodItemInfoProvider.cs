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

    public class KinovodItemInfoProvider : IItemInfoProvider
    {
        private readonly KinovodSiteProvider siteProvider;

        public KinovodItemInfoProvider(KinovodSiteProvider KinovodSiteProvider)
        {
            siteProvider = KinovodSiteProvider;
        }

        public Site Site => siteProvider.Site;

        public bool CanPreload => false;

        public ProviderRequirements ReadRequirements => ProviderRequirements.None;

        public bool CanOpenFromLink(Uri link)
        {
            return link?.Host.Contains("kinovod") == true;
        }

        public async Task<ItemInfo?> OpenFromLinkAsync(Uri link, CancellationToken cancellationToken)
        {
            if (link == null)
            {
                return null;
            }

            var linkEntries = link.ToString().Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            var section = linkEntries.FirstOrDefault() switch
            {
                "serial" => Section.CreateDefault(SectionModifiers.Serial),
                "film" => Section.CreateDefault(SectionModifiers.Film),
                "tv_show" => Section.CreateDefault(SectionModifiers.TVShow),
                _ => Section.Any
            };
            var id = linkEntries.LastOrDefault()?.Split('-').FirstOrDefault().ToIntOrNull();

            if (!id.HasValue || section == default)
            {
                return null;
            }

            var domain = await siteProvider.GetMirrorAsync(cancellationToken).ConfigureAwait(false);

            link = new Uri(domain, link.GetPath());
            var html = await siteProvider
                .HttpClient
                .GetBuilder(link)
                .WithAjax()
                .SendAsync(cancellationToken)
                .AsHtml(cancellationToken)
                .ConfigureAwait(false);

            var movieEl = html?.QuerySelector("[itemtype='https://schema.org/Movie']");
            if (movieEl == null)
            {
                return null;
            }

            return new ItemInfo(Site, id.ToString())
            {
                Link = link,
                Title = movieEl.QuerySelector("[itemprop=name]")?.GetAttribute("content"),
                Poster = movieEl.QuerySelector("img[src][itemprop=image]")?.GetAttribute("src")?.ToUriOrNull(link),
                Section = section,
                Details =
                {
                    TitleOrigin = movieEl.QuerySelector("[itemprop=alternativeHeadline]")?.TextContent.Trim(),
                    Description = movieEl.QuerySelector("[itemprop=description]")?.TextContent.Trim(),
                    Year = movieEl.QuerySelector("[itemprop=copyrightYear]")?.GetAttribute("content")?.ToIntOrNull()
                }
            };
        }

        public Task<bool> PreloadItemAsync(ItemInfo item, PreloadItemStrategy preloadItemStrategy, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }
    }
}
