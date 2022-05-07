namespace FSClient.Providers
{
    using System;
    using System.Linq;

    using FSClient.Shared.Helpers;
    using FSClient.Shared.Providers;
    using FSClient.Shared.Services;

    public class RezkaSiteProvider : BaseSiteProvider
    {
        public RezkaSiteProvider(IProviderConfigService providerConfigService) : base(
            providerConfigService,
            new ProviderConfig(Sites.Rezka,
                canBeMain: true,
                priority: 7,
                healthCheckRelativeLink: new Uri("/support.html", UriKind.Relative),
                mirrors: new[]
                {
                    new Uri("https://rezkery.com")
                }))
        {
        }

        public static int? GetIdFromUrl(Uri link)
        {
            return link.GetPath().Split('/').Last().Split('-').First().GetDigits().ToIntOrNull();
        }
    }
}
