namespace FSClient.Providers
{
    using System;

    using FSClient.Shared.Providers;
    using FSClient.Shared.Services;

    public class MyviSiteProvider : BaseSiteProvider
    {
        public MyviSiteProvider(IProviderConfigService providerConfigService) : base(
            providerConfigService,
            new ProviderConfig(Sites.Myvi,
                isVisibleToUser: false,
                mirrors: new[] { new Uri("https://www.myvi.tv") }))
        {
        }
    }
}
