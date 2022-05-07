namespace FSClient.Providers
{
    using System;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    using FSClient.Shared.Models;
    using FSClient.Shared.Providers;

    using Newtonsoft.Json.Linq;

    public class KinoPubItemInfoProvider : IItemInfoProvider
    {
        private readonly KinoPubSiteProvider siteProvider;

        public KinoPubItemInfoProvider(
            KinoPubSiteProvider kinoPubSiteProvider)
        {
            siteProvider = kinoPubSiteProvider;
        }

        public Site Site => siteProvider.Site;

        public bool CanPreload => false;

        public ProviderRequirements ReadRequirements => ProviderRequirements.ProForAny;

        public bool CanOpenFromLink(Uri link)
        {
            return link != null && siteProvider.Mirrors.Any(m => link.Host == m.Host);
        }

        public async Task<ItemInfo?> OpenFromLinkAsync(Uri link, CancellationToken cancellationToken)
        {
            var id = link?.Segments.Skip(3).FirstOrDefault()?.Trim('/');
            if (!int.TryParse(id, out _))
            {
                return null;
            }

            var domain = await siteProvider.GetMirrorAsync(cancellationToken).ConfigureAwait(false);

            var response = await siteProvider
                .GetAsync("items/" + id, cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            if (response?["item"] is not JObject item)
            {
                return null;
            }

            return siteProvider.ParseFromJson(item, domain);
        }

        public Task<bool> PreloadItemAsync(ItemInfo item, PreloadItemStrategy preloadItemStrategy, CancellationToken cancellationToken)
        {
            // KinoPub items should be already preloaded
            return Task.FromResult(true);
        }
    }
}
