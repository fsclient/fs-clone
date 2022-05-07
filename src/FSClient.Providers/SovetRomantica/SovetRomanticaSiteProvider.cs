namespace FSClient.Providers
{
    using System;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;

    using FSClient.Shared.Providers;
    using FSClient.Shared.Services;

    public class SovetRomanticaSiteProvider : BaseSiteProvider
    {
        public SovetRomanticaSiteProvider(IProviderConfigService providerConfigService) : base(
            providerConfigService,
            new ProviderConfig(Sites.SovetRomantica,
                isVisibleToUser: false,
                // DDoS-GUARD issue
                isEnabledByDefault: false,
                mirrors: new[] { new Uri("https://sovetromantica.com") }))
        {
        }

        protected override async ValueTask<bool> IsValidMirrorResponse(HttpResponseMessage responseMessage, CancellationToken cancellationToken)
        {
            return await base.IsValidMirrorResponse(responseMessage, cancellationToken)
                || responseMessage.StatusCode == System.Net.HttpStatusCode.Forbidden;
        }
    }
}
