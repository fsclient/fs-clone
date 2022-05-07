namespace FSClient.Providers
{
    using System;

    using FSClient.Shared.Providers;
    using FSClient.Shared.Services;

    public class AnimeJoySiteProvider : BaseSiteProvider
    {
        public AnimeJoySiteProvider(IProviderConfigService providerConfigService) : base(
            providerConfigService,
            new ProviderConfig(Sites.AnimeJoy,
                isVisibleToUser: false,
                mirrors: new[]
                {
                    new Uri("https://animejoy.ru")
                }))
        {
        }
    }
}
