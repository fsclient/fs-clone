namespace FSClient.Providers
{
    using System;

    using FSClient.Shared.Providers;
    using FSClient.Shared.Services;

    public class AnimediaSiteProvider : BaseSiteProvider
    {
        public AnimediaSiteProvider(IProviderConfigService providerConfigService) : base(
            providerConfigService,
            new ProviderConfig(Sites.Animedia,
                isVisibleToUser: false,
                mirrors: new[] { new Uri("https://animedia.tv") }))
        {
        }
    }
}
