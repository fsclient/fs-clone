namespace FSClient.Providers
{
    using System;
    using System.Collections.Generic;

    using FSClient.Shared;
    using FSClient.Shared.Providers;
    using FSClient.Shared.Services;

    public class CollapsSiteProvider : BaseSiteProvider
    {
        internal const string CollapsRefererKey = "CollapsReferer";

        public CollapsSiteProvider(IProviderConfigService providerConfigService) : base(
            providerConfigService,
            new ProviderConfig(Sites.Collaps,
                isEnabledByDefault: false,
                enforceDisabled: string.IsNullOrEmpty(Secrets.CollapsApiKey),
                properties: new Dictionary<string, string?>
                {
                    [CollapsRefererKey] = null // set 'null' to use Host as Referer
                },
                mirrors: new[] { new Uri("https://apicollaps.cc") }))
        {
        }
    }
}
