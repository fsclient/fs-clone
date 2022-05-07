namespace FSClient.Providers.Test.Stubs
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    using FSClient.Shared.Models;
    using FSClient.Shared.Services;

    public class ProviderServiceStub : IProviderConfigService
    {
        private readonly ProviderConfigService providerConfigService;

        public ProviderServiceStub(Site site = default, Uri? userMirror = null)
        {
            providerConfigService = new ProviderConfigService(Test.MockNewSettingService().Object, new ApplicationGlobalSettings());
            if (userMirror != null)
            {
                providerConfigService.SetUserMirror(site, userMirror);
            }
        }

        public ProviderConfig GetConfig(Site site)
        {
            return providerConfigService.GetConfig(site);
        }

        public bool? GetIsEnabledByUser(Site site)
        {
            return providerConfigService.GetIsEnabledByUser(site);
        }

        public void SetIsEnabledByUser(Site site, bool value)
        {
            providerConfigService.SetIsEnabledByUser(site, value);
        }

        public ValueTask<Uri> GetMirrorAsync(Site site, MirrorGetterConfig config, CancellationToken cancellationToken)
        {
            return providerConfigService.GetMirrorAsync(site, config, cancellationToken);
        }

        public Uri? GetUserMirror(Site site)
        {
            return providerConfigService.GetUserMirror(site);
        }

        public void SetUserMirror(Site site, Uri? value)
        {
            providerConfigService.SetUserMirror(site, value);
        }
    }
}
