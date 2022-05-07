namespace FSClient.Providers
{
    using System;

    using FSClient.Shared.Providers;
    using FSClient.Shared.Services;

    public class TorLookSiteProvider : BaseSiteProvider
    {
        public TorLookSiteProvider(IProviderConfigService providerConfigService) : base(
            providerConfigService,
            new ProviderConfig(Sites.TorLook,
                mirrors: new[] { new Uri("https://torlook.info") }))
        {
        }
    }
}
