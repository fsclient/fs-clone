namespace FSClient.Providers
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;

    using FSClient.Shared.Models;
    using FSClient.Shared.Providers;
    using FSClient.Shared.Services;

    public class SeasonVarSiteProvider : BasePlayerJsSiteProvider
    {
        public SeasonVarSiteProvider(IProviderConfigService providerConfigService) : base(
            providerConfigService,
            new ProviderConfig(Sites.SeasonVar,
                mirrorCheckingStrategy: ProviderMirrorCheckingStrategy.Sequence,
                requirements: ProviderRequirements.ProForSpecial,
                properties: new Dictionary<string, string?>
                {
                    [PlayerJsConfigKey] = "{\"Keys\":[\"b2xvbG8=\"]}"
                },
                mirrors: new[]
                {
                    new Uri("http://seasonvar.ru"),
                    new Uri("http://seasonhit-api.herokuapp.com/get/ping")
                }))
        {
        }

        public override ValueTask<ItemInfo> EnsureItemAsync(ItemInfo itemInfo, CancellationToken cancellationToken)
        {
            if (itemInfo.Link == null || itemInfo.SiteId == null)
            {
                return new ValueTask<ItemInfo>(itemInfo);
            }

            return base.EnsureItemAsync(itemInfo, cancellationToken);
        }

        protected override ValueTask<bool> IsValidMirrorResponse(HttpResponseMessage responseMessage, CancellationToken cancellationToken)
        {
            return new ValueTask<bool>(responseMessage.IsSuccessStatusCode || responseMessage.StatusCode == HttpStatusCode.MethodNotAllowed);
        }
    }
}
