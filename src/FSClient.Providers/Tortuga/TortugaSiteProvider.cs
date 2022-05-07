namespace FSClient.Providers
{
    using System;

    using FSClient.Shared.Providers;
    using FSClient.Shared.Services;

    public class TortugaSiteProvider : BaseSiteProvider
    {
        public TortugaSiteProvider(IProviderConfigService providerConfigService) : base(
            providerConfigService,
            new ProviderConfig(Sites.Tortuga,
                isVisibleToUser: false,
                mirrors: new[] { new Uri("https://tortuga.wtf") }))
        {
        }
    }
}
