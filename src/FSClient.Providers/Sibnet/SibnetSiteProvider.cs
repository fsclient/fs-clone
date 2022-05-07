namespace FSClient.Providers
{
    using System;

    using FSClient.Shared.Models;
    using FSClient.Shared.Providers;
    using FSClient.Shared.Services;

    public class SibnetSiteProvider : BaseSiteProvider
    {
        public SibnetSiteProvider(IProviderConfigService providerConfigService) : base(
               providerConfigService,
               new ProviderConfig(Sites.Sibnet,
                   isVisibleToUser: false,
                   mirrors: new[] { new Uri("https://video.sibnet.ru") }))
        {
        }
    }
}
