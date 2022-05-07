namespace FSClient.Providers
{
    using System;

    using FSClient.Shared.Helpers;
    using FSClient.Shared.Providers;
    using FSClient.Shared.Services;

    public class KinoUkrSiteProvider : BaseSiteProvider
    {
        public KinoUkrSiteProvider(IProviderConfigService providerConfigService) : base(
            providerConfigService,
            new ProviderConfig(Sites.KinoUkr,
                isEnabledByDefault: ProviderHelper.ShouldUkrainianProvidersBeEnabledByDefault,
                mirrors: new[] { new Uri("https://kinoukr.com") }))
        {
        }
    }
}
