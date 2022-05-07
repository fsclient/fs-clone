namespace FSClient.Providers
{
    using System;
    using System.Collections.Generic;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;

    using FSClient.Shared;
    using FSClient.Shared.Helpers;
    using FSClient.Shared.Services;

    public class LookbaseSiteProvider : BasePlayerJsSiteProvider
    {
        internal const string LookbaseApiDomainKey = "LookbaseApiDomain";

        public LookbaseSiteProvider(IProviderConfigService providerConfigService) : base(
            providerConfigService,
            new ProviderConfig(Sites.Lookbase,
                enforceDisabled: string.IsNullOrEmpty(Secrets.LookbaseApiKey),
                isEnabledByDefault: ProviderHelper.ShouldUkrainianProvidersBeEnabledByDefault,
                properties: new Dictionary<string, string?>
                {
                    [LookbaseApiDomainKey] = "https://lookbase.tv"
                },
                mirrors: new[] { new Uri("https://lookbaze.com") }))
        {
        }

        protected override ValueTask<bool> IsValidMirrorResponse(HttpResponseMessage responseMessage, CancellationToken cancellationToken)
        {
            // player page returns forbidden
            return new ValueTask<bool>(responseMessage.IsSuccessStatusCode || responseMessage.StatusCode == System.Net.HttpStatusCode.Forbidden);
        }
    }
}
