namespace FSClient.Providers
{
    using System;
    using System.Collections.Generic;

    using FSClient.Shared.Providers;
    using FSClient.Shared.Services;

    public class ThePirateBaySiteProvider : BaseSiteProvider
    {
        internal const string ApiDomainKey = "ApiDomain";

        public ThePirateBaySiteProvider(IProviderConfigService providerConfigService) : base(
            providerConfigService,
            new ProviderConfig(Sites.ThePirateBay,
                properties: new Dictionary<string, string?>
                {
                    [ApiDomainKey] = "https://apibay.org"
                },
                isEnabledByDefault: false,
                healthCheckRelativeLink: new Uri("static/main.js", UriKind.Relative),
                mirrors: new[] { new Uri("https://thepiratebay.org") }))
        {
        }
    }
}
