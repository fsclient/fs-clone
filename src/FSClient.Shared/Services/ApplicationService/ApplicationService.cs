namespace FSClient.Shared.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net.Http;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;

    using FSClient.Shared.Helpers.Web;
    using FSClient.Shared.Models;

    using Microsoft.Extensions.Logging;

    using Nito.AsyncEx;

    /// <inheritdoc/>
    public sealed class ApplicationService : IApplicationService, IDisposable
    {
        private readonly AsyncLazy<ApplicationGlobalSettings> lazyDataInfoTask;
        private readonly AsyncLazy<BlockListSettings> lazyBlockListTask;
        private readonly AsyncLazy<IEnumerable<ChangelogEntity>> lazyChangelogTask;
        private readonly Uri applicationDomain;
        private readonly HttpClient httpClient;
        private readonly ISettingService settingService;
        private readonly ILogger logger;

        public ApplicationService(
            ISettingService settingService,
            ILogger logger)
        {
            applicationDomain = new Uri("https://fsclient.github.io");

            httpClient = new HttpClient();
            this.settingService = settingService;
            this.logger = logger;

            lazyDataInfoTask = new AsyncLazy<ApplicationGlobalSettings>(async () => await httpClient
                .GetBuilder(new Uri(applicationDomain, "/fs/data.json"))
                .SendAsync(CancellationToken.None)
                .AsJson<ApplicationGlobalSettings>(CancellationToken.None)
                .ConfigureAwait(false) ?? new ApplicationGlobalSettings());
            lazyBlockListTask = new AsyncLazy<BlockListSettings>(async () => await httpClient
                .GetBuilder(new Uri(applicationDomain, "/fs/blacklist.json"))
                .SendAsync(CancellationToken.None)
                .AsJson<BlockListSettings>(CancellationToken.None)
                .ConfigureAwait(false) ?? new BlockListSettings());
            lazyChangelogTask = new AsyncLazy<IEnumerable<ChangelogEntity>>(async () => await httpClient
                .GetBuilder(new Uri(applicationDomain, "/fs/changelog.json"))
                .SendAsync(CancellationToken.None)
                .AsJson<IEnumerable<ChangelogEntity>>(CancellationToken.None)
                .ConfigureAwait(false) ?? Enumerable.Empty<ChangelogEntity>());
        }

        public Uri PrivacyInfoLink => new Uri(applicationDomain, "/fs/priv_policy.html");

        public Uri FAQLink => new Uri(applicationDomain, "/fs/faq.html");

        public Task<IEnumerable<ChangelogEntity>> GetChangelogAsync(CancellationToken cancellationToken)
        {
            return lazyChangelogTask.Task.WaitAsync(cancellationToken);
        }

        public Task<BlockListSettings> GetBlockListSettingsAsync(CancellationToken cancellationToken)
        {
            return lazyBlockListTask.Task.WaitAsync(cancellationToken);
        }

        public Task<ApplicationGlobalSettings> GetApplicationGlobalSettingsAsync(CancellationToken cancellationToken)
        {
            return lazyDataInfoTask.Task.WaitAsync(cancellationToken);
        }

        public ApplicationGlobalSettings GetApplicationGlobalSettingsFromCache()
        {
            try
            {
                var previousJsonSourceStr = settingService.GetSetting(Settings.InternalSettingsContainer, nameof(ApplicationGlobalSettings), null);
                if (previousJsonSourceStr == null)
                {
                    // Application should wait with blocking for settings at first startup.
                    _ = LoadApplicationGlobalSettingsToCacheAsync(default).GetAwaiter().GetResult();
                    previousJsonSourceStr = settingService.GetSetting(Settings.InternalSettingsContainer, nameof(ApplicationGlobalSettings), null);
                }
                var settings = JsonSerializer.Deserialize<ApplicationGlobalSettings>(previousJsonSourceStr ?? "{}", new JsonSerializerOptions
                {
                    Converters =
                    {
                        new SiteJsonConverter()
                    }
                });
                if (settings != null)
                {
                    return settings;
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex);
            }
            return new ApplicationGlobalSettings();
        }

        public async Task<bool> LoadApplicationGlobalSettingsToCacheAsync(CancellationToken cancellationToken)
        {
            if (settingService == null)
            {
                return false;
            }

            try
            {
                var info = await lazyDataInfoTask.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
                if (info == null)
                {
                    return false;
                }

                var previousJsonSourceStr = settingService.GetSetting(Settings.InternalSettingsContainer, nameof(ApplicationGlobalSettings), null);

                var newJsonSourceStr = JsonSerializer.Serialize(info, new JsonSerializerOptions
                {
                    Converters =
                    {
                        new SiteJsonConverter()
                    }
                });

                if (previousJsonSourceStr != newJsonSourceStr)
                {
                    if (info.ProviderConfigs != null)
                    {
                        foreach (var site in info.ProviderConfigs)
                        {
                            settingService.DeleteSetting(Settings.InternalSettingsContainer, "CachedDomain_" + site.Site);
                        }
                    }

                    settingService.SetSetting(Settings.InternalSettingsContainer, nameof(ApplicationGlobalSettings), newJsonSourceStr);
                    return true;
                }
            }
            catch (OperationCanceledException)
            {

            }
            catch (Exception ex)
            {
                logger.LogWarning(ex);
            }
            return false;
        }

        public void Dispose()
        {
            httpClient.Dispose();
        }
    }
}
