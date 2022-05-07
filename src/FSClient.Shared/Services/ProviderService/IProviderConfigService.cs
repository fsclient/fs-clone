namespace FSClient.Shared.Services
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    using FSClient.Shared.Models;

    public interface IProviderConfigService
    {
        bool? GetIsEnabledByUser(Site site);

        void SetIsEnabledByUser(Site site, bool value);

        Uri? GetUserMirror(Site site);

        void SetUserMirror(Site site, Uri? value);

        ProviderConfig GetConfig(Site site);

        ValueTask<Uri> GetMirrorAsync(Site site, MirrorGetterConfig config, CancellationToken cancellationToken);
    }
}
