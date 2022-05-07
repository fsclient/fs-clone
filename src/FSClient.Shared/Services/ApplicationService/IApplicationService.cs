namespace FSClient.Shared.Services
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    public interface IApplicationService
    {
        Uri PrivacyInfoLink { get; }

        Uri FAQLink { get; }

        Task<IEnumerable<ChangelogEntity>> GetChangelogAsync(CancellationToken cancellationToken);

        Task<BlockListSettings> GetBlockListSettingsAsync(CancellationToken cancellationToken);

        Task<ApplicationGlobalSettings> GetApplicationGlobalSettingsAsync(CancellationToken cancellationToken);

        ApplicationGlobalSettings GetApplicationGlobalSettingsFromCache();

        Task<bool> LoadApplicationGlobalSettingsToCacheAsync(CancellationToken cancellationToken);
    }
}
