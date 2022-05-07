namespace FSClient.Providers
{
    using System;
    using System.Collections.Generic;

    using FSClient.Shared.Helpers;
    using FSClient.Shared.Providers;
    using FSClient.Shared.Services;

    public class NekomoriSiteProvider : BaseSiteProvider
    {
        private readonly Uri WebClientDomain = new Uri("https://nekomori.ch/");

        public NekomoriSiteProvider(IProviderConfigService providerConfigService) : base(
            providerConfigService,
            new ProviderConfig(Sites.Nekomori,
                mirrors: new[]
                {
                    new Uri("https://nekomori.ch")
                },
                healthCheckRelativeLink: new Uri("/api/arts/5270", UriKind.Relative)))
        {
        }

        protected override MirrorGetterConfig PrepareMirrorGetterConfig()
        {
            return base.PrepareMirrorGetterConfig() with
            {
                AdditionalHeaders = new Dictionary<string, string>
                {
                    ["Origin"] = WebClientDomain.GetOrigin() ?? string.Empty,
                    ["Referer"] = WebClientDomain.ToString(),
                }
            };
        }
    }
}
