namespace FSClient.Shared.Managers
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;

    using FSClient.Shared.Helpers.Web;
    using FSClient.Shared.Models;
    using FSClient.Shared.Providers;
    using FSClient.Shared.Services;

    using Microsoft.Extensions.Logging;

    using Nito.AsyncEx;

    /// <inheritdoc/>
    public sealed class UserManager : IUserManager
    {
        private readonly Dictionary<Site, (IAuthProvider authProvider, ISiteProvider siteProvider, SemaphoreSlim semaphore)> providers;
        private readonly ICookieManager cookieManager;
        private readonly ILauncherService launcherService;
        private readonly ILogger logger;

        public UserManager(
            IEnumerable<IAuthProvider> authProviders,
            IEnumerable<ISiteProvider> siteProviders,
            ICookieManager cookieManager,
            ILauncherService launcherService,
            ILogger logger)
        {
            this.cookieManager = cookieManager;
            this.launcherService = launcherService;
            this.logger = logger;

            providers = authProviders
                .Select(authProvider => (authProvider, siteProvider: siteProviders.FirstOrDefault(siteProvider => siteProvider.Site == authProvider.Site)))
                .Where(tuple => tuple.siteProvider != null)
                .ToDictionary(tuple => tuple.siteProvider.Site, tuple => (tuple.authProvider, tuple.siteProvider, new SemaphoreSlim(1)));
        }

        public event Action<Site, User>? UserLoggedIn;

        public event Action<Site>? UserLoggedOut;

        public ValueTask<User?> GetCurrentUserAsync(Site site, CancellationToken cancellationToken)
        {
            if (!providers.TryGetValue(site, out var tuple))
            {
                return new ValueTask<User?>((User?)null);
            }
            if (tuple.siteProvider.CurrentUser is User user)
            {
                return new ValueTask<User?>(user);
            }

            return new ValueTask<User?>(TryGetOrRefreshCurrentUserAsync(site, cancellationToken));
        }

        public ValueTask<bool> CheckRequirementsAsync(Site site, ProviderRequirements providerRequirements, CancellationToken cancellationToken)
        {
            if (providerRequirements == ProviderRequirements.None)
            {
                return new ValueTask<bool>(true);
            }
            if (!providers.TryGetValue(site, out var tuple))
            {
                return new ValueTask<bool>(!providerRequirements.HasFlag(ProviderRequirements.AccountForAny));
            }
            if (tuple.siteProvider.CurrentUser is User currentUser)
            {
                return new ValueTask<bool>(CheckRequirementForUser(currentUser, providerRequirements));
            }
            return CheckRequirementForUserSlowAsync(providerRequirements, cancellationToken);

            static bool CheckRequirementForUser(User? user, ProviderRequirements providerRequirements)
            {
                if (user == null)
                {
                    return !providerRequirements.HasFlag(ProviderRequirements.AccountForAny);
                }
                if (providerRequirements.HasFlag(ProviderRequirements.ProForAny))
                {
                    return user.HasProStatus;
                }

                // User is not null here, so any other ProviderRequirements is allowed.
                return true;
            }

            async ValueTask<bool> CheckRequirementForUserSlowAsync(ProviderRequirements providerRequirements, CancellationToken cancellationToken)
            {
                var user = await TryGetOrRefreshCurrentUserAsync(site, cancellationToken).ConfigureAwait(false);
                return CheckRequirementForUser(user, providerRequirements);
            }
        }

        public async Task<(User? user, AuthStatus status)> AuthorizeAsync(Site site, AuthModel authModel, CancellationToken cancellationToken)
        {
            try
            {
                if (!providers.TryGetValue(site, out var tuple))
                {
                    return (null, AuthStatus.Error);
                }

                using (await tuple.semaphore.LockAsync(cancellationToken).ConfigureAwait(false))
                {
                    var result = await tuple.authProvider.AuthorizeAsync(authModel, cancellationToken).ConfigureAwait(false);
                    if (result.User != null)
                    {
                        if (result.AuthStatus != AuthStatus.Success)
                        {
                            logger.LogWarning($"User is created, but result is {result.AuthStatus}");
                        }

                        if (tuple.siteProvider is IHttpSiteProvider httpSiteProvider)
                        {
                            foreach (var mirror in httpSiteProvider.Mirrors)
                            {
                                httpSiteProvider.Handler.SetCookies(mirror, result.Cookies.ToArray());
                            }
                        }

                        cookieManager.Save(site, result.Cookies);

                        UserLoggedIn?.Invoke(site, result.User);

                        tuple.siteProvider.CurrentUser = result.User;
                        return (result.User, AuthStatus.Success);
                    }

                    return (null, result.AuthStatus);
                }
            }
            catch (OperationCanceledException)
            {
                return (null, AuthStatus.Canceled);
            }
            catch (Exception ex)
            {
                ex.Data["Site"] = site;
                logger?.LogError(ex);
                return (null, AuthStatus.Error);
            }
        }

        public IEnumerable<AuthModel> GetAuthModels(Site site)
        {
            if (providers.TryGetValue(site, out var tuple))
            {
                return tuple.authProvider.AuthModels;
            }
            return Enumerable.Empty<AuthModel>();
        }

        public async Task LogoutAsync(Site site, CancellationToken cancellationToken)
        {
            try
            {
                if (!providers.TryGetValue(site, out var tuple))
                {
                    return;
                }

                using (await tuple.semaphore.LockAsync(cancellationToken).ConfigureAwait(false))
                {
                    await tuple.authProvider.LogoutAsync(cancellationToken).ConfigureAwait(false);
                    tuple.siteProvider.CurrentUser = null;

                    if (tuple.siteProvider is IHttpSiteProvider httpSiteProvider)
                    {
                        var cookNames = cookieManager.DeleteAll(site).ToArray() ?? Array.Empty<string>();
                        foreach (var mirror in tuple.siteProvider.Mirrors)
                        {
                            httpSiteProvider.Handler.DeleteCookies(mirror, cookNames);
                        }
                    }
                }

                UserLoggedOut?.Invoke(site);
            }
            catch (Exception ex)
            {
                ex.Data["Site"] = ex;
                logger?.LogError(ex);
            }
        }

        public async Task<bool> RegisterAsync(Site site, CancellationToken cancellationToken)
        {
            if (providers.TryGetValue(site, out var tuple))
            {
                var link = await tuple.authProvider.GetRegisterLinkAsync(cancellationToken).ConfigureAwait(true);
                var result = await launcherService.LaunchUriAsync(link).ConfigureAwait(true);
                return result == LaunchResult.Success;
            }
            return false;
        }

        private async Task<User?> TryGetOrRefreshCurrentUserAsync(Site site, CancellationToken cancellationToken)
        {
            if (!providers.TryGetValue(site, out var tuple))
            {
                return null;
            }
            if (tuple.siteProvider.CurrentUser is User currentUser)
            {
                return currentUser;
            }

            using var _ = await tuple.semaphore.LockAsync(cancellationToken);

            if (tuple.siteProvider.CurrentUser is User currentUserAfterLock)
            {
                return currentUserAfterLock;
            }

            try
            {
                var cookies = EnsureHttpProviderCookies(site);
                var result = await tuple.authProvider.TryGetOrRefreshUserAsync(cookies, cancellationToken).ConfigureAwait(false);

                if (!cookies.SequenceEqual(result.Cookies))
                {
                    cookieManager.Save(site, result.Cookies);
                    EnsureHttpProviderCookies(site);
                }

                tuple.siteProvider.CurrentUser = result.User;
                return result.User;
            }
            catch (Exception ex)
            {
                ex.Data["Site"] = site;
                logger?.LogError(ex);
                return null;
            }
        }

        private IEnumerable<Cookie> EnsureHttpProviderCookies(Site site)
        {
            if (providers.TryGetValue(site, out var tuple)
                && tuple.siteProvider is IHttpSiteProvider httpSiteProvider)
            {
                var cookies = cookieManager.Load(tuple.siteProvider.Site).ToArray();
                foreach (var mirror in tuple.siteProvider.Mirrors)
                {
                    httpSiteProvider.Handler.SetCookies(mirror, cookies);
                }
                return cookies;
            }
            return Enumerable.Empty<Cookie>();
        }
    }
}
