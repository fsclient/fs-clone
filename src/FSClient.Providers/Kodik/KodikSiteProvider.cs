namespace FSClient.Providers
{
    using System;
    using System.Collections.Generic;

    using FSClient.Shared;
    using FSClient.Shared.Providers;
    using FSClient.Shared.Services;

    public class KodikSiteProvider : BaseSiteProvider
    {
        internal const string GetVideosRelativeLinkKey = "GetVideosRelativeLink";
        internal const string KodikApiDomainKey = "KodikApiDomain";
        internal const string KodikHash2Key = "KodikHash2";
        internal const string KodikRefererKey = "KodikReferer";

        public KodikSiteProvider(IProviderConfigService providerConfigService) : base(
               providerConfigService,
               new ProviderConfig(Sites.Kodik,
                   enforceDisabled: string.IsNullOrEmpty(Secrets.KodikApiKey),
                   properties: new Dictionary<string, string?>
                   {
                       [KodikApiDomainKey] = "https://kodikapi.com",
                       [KodikRefererKey] = "https://aniqit.com" // set 'null' to use Host as Referer
                   },
                   mirrors: new[] { new Uri("https://aniqit.com") }))
        {
        }
    }
}
