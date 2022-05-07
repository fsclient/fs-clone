namespace FSClient.Shared.Services
{
    using System;
    using System.Collections.Concurrent;
    using System.Linq;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;

    using FSClient.Shared.Helpers;
    using FSClient.Shared.Helpers.Web;
    using FSClient.Shared.Models;

    using Nito.AsyncEx;

    public sealed class ProviderConfigService : IProviderConfigService, IDisposable
    {
        private readonly ConcurrentDictionary<Site, (SemaphoreSlim semaphore, Uri? mirror)> perSiteMirrorCache;
        private readonly ISettingService settingService;
        private readonly ApplicationGlobalSettings applicationSettings;

        public ProviderConfigService(
            ISettingService settingService,
            ApplicationGlobalSettings applicationSettings)
        {
            perSiteMirrorCache = new ConcurrentDictionary<Site, (SemaphoreSlim semaphore, Uri? mirror)>();
            this.settingService = settingService;
            this.applicationSettings = applicationSettings;
        }

        public bool? GetIsEnabledByUser(Site site)
        {
            return settingService
                .GetSetting<bool>(Settings.UserSettingsContainer, "IsProviderEnabled_" + site.Value, null, SettingStrategy.Local);
        }

        public void SetIsEnabledByUser(Site site, bool value)
        {
            settingService.SetSetting(Settings.UserSettingsContainer, "IsProviderEnabled_" + site.Value, value, SettingStrategy.Local);
        }

        public Uri? GetUserMirror(Site site)
        {
            var userDomainStr = settingService
                .GetSetting(Settings.UserSettingsContainer, "UserDomain_" + site.Value, null, SettingStrategy.Local);
            return userDomainStr.ToUriOrNull();
        }

        public void SetUserMirror(Site site, Uri? value)
        {
            if (value?.IsAbsoluteUri == false)
            {
                throw new ArgumentException("User domain must be an absolute uri", nameof(value));
            }

            if (value == null)
            {
                settingService.DeleteSetting(Settings.UserSettingsContainer, "UserDomain_" + site.Value, SettingStrategy.Local);
            }
            else
            {
                settingService.SetSetting(Settings.UserSettingsContainer, "UserDomain_" + site.Value, value.OriginalString, SettingStrategy.Local);
            }
        }

        private static bool FastPathGetMirrorAvailable(MirrorGetterConfig config, Uri? userMirror)
        {
            return (config.Mirrors.Count == 1 && config.MirrorFinder == null) || userMirror != null;
        }

        public ValueTask<Uri> GetMirrorAsync(Site site, MirrorGetterConfig config, CancellationToken cancellationToken)
        {
            if (perSiteMirrorCache.TryGetValue(site, out var cacheTuple)
                && cacheTuple.mirror != null)
            {
                return new ValueTask<Uri>(cacheTuple.mirror);
            }

            var userMirror = GetUserMirror(site);
            if (FastPathGetMirrorAvailable(config, userMirror))
            {
                if (userMirror != null)
                {
                    return new ValueTask<Uri>(userMirror);
                }

                if (config.Mirrors.Count == 1)
                {
                    return new ValueTask<Uri>(config.Mirrors.First());
                }
            }

            var (_, inMemoryCachedMirror) = perSiteMirrorCache.GetOrAdd(site, _ => (new SemaphoreSlim(1), null));
            if (inMemoryCachedMirror != null)
            {
                return new ValueTask<Uri>(inMemoryCachedMirror);
            }

            return GetMirrorInternalAsync();

            async ValueTask<Uri> GetMirrorInternalAsync()
            {
                try
                {
                    using var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(15));
                    using var linkedCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, cancellationTokenSource.Token);
                    var localCancellationToken = linkedCancellationTokenSource.Token;

                    var mirrorSemaphore = perSiteMirrorCache[site].semaphore;
                    using var _ = await mirrorSemaphore.LockAsync(cancellationTokenSource.Token);

                    var inMemoryCachedMirror = perSiteMirrorCache[site].mirror;
                    if (inMemoryCachedMirror != null)
                    {
                        return inMemoryCachedMirror;
                    }

                    if (GetUserMirror(site) is Uri userMirror)
                    {
                        perSiteMirrorCache[site] = (mirrorSemaphore, userMirror);
                        return userMirror;
                    }

                    // If it was cached domain, try it first
                    var cachedDomain = settingService
                        .GetSetting(Settings.InternalSettingsContainer, "CachedDomain_" + site.Value, null, SettingStrategy.Local)?
                        .ToUriOrNull();

                    // Check only 4 mirrors at one time
                    var mirrors = config.Mirrors
                        .OrderBy(mirror => mirror == cachedDomain)
                        .AsEnumerable();
                    if (config.MirrorCheckingStrategy == ProviderMirrorCheckingStrategy.Parallel)
                    {
                        if (cachedDomain != null
                            && mirrors.Contains(cachedDomain))
                        {
                            var (mirrorToSave, mirrorToUse, response, isAvailable) = await IsMirrorAvailableAsync(cachedDomain, localCancellationToken).ConfigureAwait(false);

                            if (isAvailable)
                            {
                                return ProcessMirror(mirrorToSave, mirrorToUse, response, true);
                            }
                        }

                        while (mirrors.Any())
                        {
                            var nextMirrors = mirrors.Take(4);
                            mirrors = mirrors.Skip(4);
                            var (mirrorToSave, mirrorToUse, response, _) = await nextMirrors
                                .Select(mirror => new Func<CancellationToken, Task<(Uri, Uri, HttpResponseMessage?, bool isAvailable)>>(
                                    ct => IsMirrorAvailableAsync(mirror, ct)))!
                                .WhenAny(tuple => tuple.isAvailable, (null, null, null, false), localCancellationToken)
                                .ConfigureAwait(false);

                            if (mirrorToUse != null)
                            {
                                return ProcessMirror(mirrorToSave, mirrorToUse, response, true);
                            }

                            await Task.Delay(200, localCancellationToken).ConfigureAwait(false);
                        }
                    }
                    else
                    {
                        foreach (var mirror in mirrors)
                        {
                            using (var currentMirrorCts = new CancellationTokenSource(TimeSpan.FromSeconds(5)))
                            using (var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(currentMirrorCts.Token, localCancellationToken))
                            {
                                var (mirrorToSave, mirrorToUse, response, isAvailable) = await IsMirrorAvailableAsync(mirror, linkedCts.Token).ConfigureAwait(false);

                                if (isAvailable)
                                {
                                    return ProcessMirror(mirrorToSave, mirrorToUse, response, true);
                                }
                            }

                            await Task.Delay(200, localCancellationToken).ConfigureAwait(false);
                        }
                    }

                    if (config.MirrorFinder is { } mirrorFinder)
                    {
                        var previousMirror = cachedDomain ?? config.Mirrors.FirstOrDefault();
                        var foundedMirror = await mirrorFinder(previousMirror, localCancellationToken).ConfigureAwait(false);
                        if (foundedMirror != null)
                        {
                            var (mirrorToSave, mirrorToUse, response, isAvailable) = await IsMirrorAvailableAsync(foundedMirror, localCancellationToken).ConfigureAwait(false);
                            if (isAvailable)
                            {
                                return ProcessMirror(mirrorToSave, mirrorToUse, response, true);
                            }
                        }
                    }

                    async Task<(Uri mirrorToSave, Uri mirrorToUse, HttpResponseMessage? response, bool isAvailable)> IsMirrorAvailableAsync(Uri mirror, CancellationToken ct)
                    {
                        var mirrorToCheck = mirror;
                        var relativeUri = config.HealthCheckRelativeLink;
                        if (relativeUri != null)
                        {
                            mirrorToCheck = new Uri(mirrorToCheck, relativeUri);
                        }

                        var (mirrorToUse, response, isAvailable) = await mirrorToCheck
                            .IsAvailableWithLocationAsync(config.HttpMethod, config.AdditionalHeaders, config.Validator, ct)
                            .ConfigureAwait(false);

                        if (mirrorToUse == mirrorToCheck)
                        {
                            mirrorToUse = mirror;
                        }
                        else if (relativeUri != null && !relativeUri.IsAbsoluteUri)
                        {
                            mirrorToUse = new Uri(mirrorToUse.OriginalString.Replace(relativeUri.OriginalString, ""));
                        }

                        return (mirror, mirrorToUse, response, isAvailable);
                    }

                    Uri ProcessMirror(Uri mirrorToSave, Uri mirrorToUse, HttpResponseMessage? response, bool saveToCache)
                    {
                        if (saveToCache)
                        {
                            settingService.SetSetting(
                               Settings.InternalSettingsContainer,
                               "CachedDomain_" + site.Value, mirrorToSave.AbsoluteUri,
                               SettingStrategy.Local);
                        }

                        SaveCookiesToHandler(mirrorToUse, response, config.Handler);
                        perSiteMirrorCache[site] = (mirrorSemaphore, mirrorToUse);
                        return mirrorToUse;
                    }
                }
                catch (OperationCanceledException)
                {
                }
                catch (Exception ex)
                {
                    Logger.Instance.LogError(ex);
                }

                var fallback = config.Mirrors.First();
                if (perSiteMirrorCache[site].mirror is Uri cachedMirror)
                {
                    fallback = cachedMirror;
                }

                return fallback;
            }
        }

        public ProviderConfig GetConfig(Site site)
        {
            return applicationSettings.ProviderConfigs
                .FirstOrDefault(p => p.Site == site)
                ?? new ProviderConfig(site);
        }

        public void Dispose()
        {
            foreach (var (semaphore, _) in perSiteMirrorCache.Values)
            {
                semaphore.Dispose();
            }
        }

        private static void SaveCookiesToHandler(Uri domain, HttpResponseMessage? response, HttpClientHandler? handler)
        {
            if (response != null && handler != null)
            {
                handler.SetCookies(domain, response.GetCookies().ToArray());
            }
        }
    }
}
