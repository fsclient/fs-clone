namespace FSClient.Providers
{
    using System;

    using FSClient.Shared.Providers;
    using FSClient.Shared.Services;

    public class YoutubeSiteProvider : BaseSiteProvider
    {
        public YoutubeSiteProvider(IProviderConfigService providerConfigService) : base(
            providerConfigService,
            new ProviderConfig(Sites.Youtube,
                isVisibleToUser: false,
                mirrors: new[] { new Uri("https://www.youtube.com") }))
        {
        }
    }
}
