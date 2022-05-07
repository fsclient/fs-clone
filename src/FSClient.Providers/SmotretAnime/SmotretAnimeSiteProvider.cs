namespace FSClient.Providers
{
    using System;

    using FSClient.Shared.Providers;
    using FSClient.Shared.Services;

    public class SmotretAnimeSiteProvider : BaseSiteProvider
    {
        public SmotretAnimeSiteProvider(IProviderConfigService providerConfigService) : base(
            providerConfigService,
            new ProviderConfig(Sites.SmotretAnime,
                requirements: ProviderRequirements.ProForAny,
                isEnabledByDefault: false,
                mirrors: new[]
                {
                    new Uri("https://smotret-anime.online"),
                    new Uri("https://smotretanime.ru"),
                    new Uri("https://smotret-anime.ru"),
                    new Uri("https://anime365.ru")
                }))
        {
        }
    }
}
