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

    public class KodikItemInfoProvider : IItemInfoProvider
    {
        private readonly KodikSiteProvider siteProvider;

        public KodikItemInfoProvider(KodikSiteProvider kodikSiteProvider)
        {
            siteProvider = kodikSiteProvider;
        }

        public Site Site => siteProvider.Site;

        public bool CanPreload => false;

        public ProviderRequirements ReadRequirements => ProviderRequirements.None;

        public bool CanOpenFromLink(Uri link)
        {
            return new KodikPlayerParseProvider(siteProvider).CanOpenFromLinkOrHostingName(link);
        }

        public async Task<ItemInfo?> OpenFromLinkAsync(Uri link, CancellationToken cancellationToken)
        {
            var segments = link.GetPath().Split(new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries)
                .Where(s => s != "go")
                .ToArray();

            var section = segments.FirstOrDefault();
            bool isSerial;
            switch (section)
            {
                case "video":
                    isSerial = false;
                    break;
                case "seria":
                case "serial":
                    isSerial = true;
                    break;
                default:
                    return null;
            }

            var id = segments.Skip(1).FirstOrDefault()?.ToIntOrNull();
            if (id == null || link == null)
            {
                return null;
            }

            if (!link.IsAbsoluteUri)
            {
                link = new Uri(new Uri("https://kodik.cc/"), link);
            }
            else if (link.Host.Contains("kodik.top"))
            {
                link = new Uri(link.ToString().Replace("kodik.top", "kodik.cc"));
            }

            var page = (await siteProvider
                .HttpClient
                .GetBuilder(link)
                .WithHeader("Referer", "http://yohoho.cc/")
                .SendAsync(cancellationToken)
                .AsHtml(cancellationToken)
                .ConfigureAwait(false));

            return new ItemInfo(Site, $"kdk{id}")
            {
                Title = page?.Title,
                Link = link,
                Section = Section.CreateDefault(isSerial ? SectionModifiers.Serial : SectionModifiers.Film)
            };
        }

        public Task<bool> PreloadItemAsync(ItemInfo item, PreloadItemStrategy preloadItemStrategy, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }
    }
}
