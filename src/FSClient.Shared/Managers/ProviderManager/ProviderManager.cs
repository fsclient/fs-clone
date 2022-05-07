namespace FSClient.Shared.Managers
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    using FSClient.Shared.Models;
    using FSClient.Shared.Providers;
    using FSClient.Shared.Services;

    using Microsoft.Extensions.Logging;

    public sealed class ProviderManager : IProviderManager
    {
        private readonly Dictionary<Site, ISiteProvider> siteProviders;
        private readonly IProviderConfigService providerConfigService;
        private readonly ISettingService settingService;
        private readonly ILogger logger;
        private readonly IEnumerable<IFileProvider> fileProviders;

        public ProviderManager(
            IEnumerable<ISiteProvider> siteProviders,
            IEnumerable<IFileProvider> fileProviders,
            IProviderConfigService providerConfigService,
            ISettingService settingService,
            ILogger logger)
        {
            this.providerConfigService = providerConfigService;
            this.settingService = settingService;
            this.siteProviders = siteProviders.ToDictionary(p => p.Site, p => p);
            this.fileProviders = fileProviders;
            this.logger = logger;
        }

        /// <inheritdoc/>
        public void EnsureCurrentMainProvider()
        {
            var available = GetMainProviders();
            var current = Settings.Instance.MainSite;
            if (!available.Contains(current))
            {
                Settings.Instance.MainSite = available.FirstOrDefault();
            }
        }

        /// <inheritdoc/>
        public bool IsProviderEnabled(Site site)
        {
            if (!siteProviders.TryGetValue(site, out var provider))
            {
                return false;
            }

            return !provider.EnforceDisabled && (providerConfigService.GetIsEnabledByUser(site) ?? provider.IsEnabledByDefault);
        }

        /// <inheritdoc/>
        public ValueTask<bool> IsSiteAvailable(Site site, CancellationToken cancellationToken)
        {
            if (!siteProviders.TryGetValue(site, out var provider))
            {
                return new ValueTask<bool>(false);
            }

            return new ValueTask<bool>(provider.IsAvailableAsync(cancellationToken));
        }

        /// <inheritdoc/>
        public async ValueTask<ItemInfo?> EnsureItemAsync(ItemInfo itemInfo, CancellationToken cancellationToken)
        {
            if (!siteProviders.TryGetValue(itemInfo.Site, out var siteProvider)
                || cancellationToken.IsCancellationRequested)
            {
                return itemInfo;
            }
            try
            {
                return await siteProvider.EnsureItemAsync(itemInfo, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                logger.LogError(ex);
            }
            return itemInfo;
        }

        /// <inheritdoc/>
        public IEnumerable<Site> GetFileProviders(FileProviderTypes fileProviderTypes)
        {
            var order = GetOrderedProviders().ToList();
            return fileProviders
                .Where(p =>
                     (!fileProviderTypes.HasFlag(FileProviderTypes.Online) || p.ProvideOnline)
                     && (!fileProviderTypes.HasFlag(FileProviderTypes.Torrent) || p.ProvideTorrent)
                     && (!fileProviderTypes.HasFlag(FileProviderTypes.Trailer) || p.ProvideTrailers))
                .Select(p => p.Site)
                .OrderBy(s => order.IndexOf(s) is var index && index >= 0 ? index : int.MaxValue);
        }

        /// <inheritdoc/>
        public void SetProvidersOrder(IEnumerable<Site> collection)
        {
            settingService.SetSetting(
                Settings.UserSettingsContainer,
                "OrderedProviders",
                string.Join(",", collection.Select(p => p.Value)));
        }

        /// <inheritdoc/>
        public IEnumerable<Site> GetOrderedProviders()
        {
            var available = siteProviders.OrderByDescending(p => p.Value.Details.Priority).Select(p => p.Key).ToArray();
            var orderedProvidersStr = settingService
                .GetSetting(Settings.UserSettingsContainer, "OrderedProviders", string.Empty);

            return orderedProvidersStr
                .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => Site.TryParse(s, out var site) && siteProviders.ContainsKey(site)
                    ? site
                    : default)
                .Where(p => p != default)
                .Intersect(available)
                .Union(available);
        }

        /// <inheritdoc/>
        public IEnumerable<Site> GetMainProviders()
        {
            return siteProviders
                .Values
                .OrderByDescending(s => s.Details.Priority)
#if !DEBUG
                .Where(s => s.CanBeMain)
#endif
                .Select(s => s.Site)
                .ToArray();
        }
    }
}
