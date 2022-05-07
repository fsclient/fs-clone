namespace FSClient.Providers
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;

    using FSClient.Shared;
    using FSClient.Shared.Helpers;
    using FSClient.Shared.Helpers.Web;
    using FSClient.Shared.Models;
    using FSClient.Shared.Providers;
    using FSClient.Shared.Services;

    using Newtonsoft.Json.Linq;

    public class KinoPubAuthProvider : IAuthProvider
    {
        private const string AccessTokenKey = "access";
        private const string RefreshTokenKey = "refresh";
        private const string EndTimeKey = "endTime";
        private const string UserNameKey = "login";
        private const string UserAvatarName = "avatar";

        private static readonly Uri defaultAvatar = new Uri("http://dats.xyz/kinopub/favicon.png");

        private readonly KinoPubSiteProvider siteProvider;
        private readonly IContentDialog<DeviceFlowDialogInput, AuthStatus> deviceFlowDialog;
        private readonly IAppInformation appInformation;

        public KinoPubAuthProvider(
            KinoPubSiteProvider siteProvider,
            IContentDialog<DeviceFlowDialogInput, AuthStatus> deviceFlowDialog,
            IAppInformation appInformation)
        {
            this.siteProvider = siteProvider;
            this.deviceFlowDialog = deviceFlowDialog;
            this.appInformation = appInformation;

            AuthModels = new[]
            {
                new AuthModel
                {
                    Title = Site.Title,
                    IsOAuth = false,
                    Icon = defaultAvatar
                }
            };
        }

        public IEnumerable<AuthModel> AuthModels { get; }

        public virtual Site Site => siteProvider.Site;

        public async Task<AuthResult> AuthorizeAsync(AuthModel authModel, CancellationToken cancellationToken)
        {
            string? access = null, refresh = null;

            var domain = await siteProvider.GetMirrorAsync(cancellationToken).ConfigureAwait(false);
            var response = await PostAuthApiAsync("device_code", null, cancellationToken).ConfigureAwait(false);

            var code = response?["code"]?.ToString();
            var userCode = response?["user_code"]?.ToString();
            if (response == null || code == null || userCode == null)
            {
                return new AuthResult(AuthStatus.Error);
            }

            var expiresIn = response["expires_in"]?.ToIntOrNull() ?? 5 * 60;
            var expiresAt = DateTimeOffset.Now + TimeSpan.FromSeconds(expiresIn - 30);
            var verificationUrl = response["verification_uri"]?.ToUriOrNull(domain) ?? new Uri(domain, "/device");
            var interval = TimeSpan.FromSeconds(response["interval"]?.ToIntOrNull() ?? 5);

            using (var dialogTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            {
                var deviceFlowDialogTask = deviceFlowDialog.ShowAsync(
                    new DeviceFlowDialogInput(userCode, expiresAt, verificationUrl),
                    dialogTokenSource.Token);

                do
                {
                    if (deviceFlowDialogTask.IsCompleted)
                    {
                        break;
                    }

                    await Task.Delay(interval, cancellationToken).ConfigureAwait(false);
                    response = await PostAuthApiAsync("device_token", code, cancellationToken).ConfigureAwait(false);

                    access = response?["access_token"]?.ToString();
                    refresh = response?["refresh_token"]?.ToString();
                    if (access != null)
                    {
                        break;
                    }

                    if (response == null
                        || response["error"]?.ToString() != "authorization_pending")
                    {
                        return new AuthResult(AuthStatus.Error);
                    }
                }
                while (DateTimeOffset.Now < expiresAt);

                // When operation is completed, we cancel dialog showing, but it still could be success result.
                if (!deviceFlowDialogTask.IsCompleted)
                {
                    dialogTokenSource.Cancel();
                }

                AuthStatus dialogResult;
                try
                {
                    dialogResult = await deviceFlowDialogTask.ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    dialogResult = AuthStatus.Canceled;
                }

                if (access == null
                    || dialogResult == AuthStatus.Error)
                {
                    return new AuthResult(dialogResult == AuthStatus.Canceled ? AuthStatus.Canceled : AuthStatus.Error);
                }
            }

            var cookies = RefreshTokens(Enumerable.Empty<Cookie>(), access, refresh);

            var (userName, avatar, endTime) = await GetUserInfoAsync(access, cancellationToken).ConfigureAwait(false);

            cookies = RefreshTokens(cookies, access, refresh, userName, avatar, endTime);

            await SendNotifyDevice(access, cancellationToken).ConfigureAwait(false);

            var user = GetUserOrNull(cookies);
            if (user != null)
            {
                return new AuthResult(AuthStatus.Success, user, cookies);
            }

            return new AuthResult(AuthStatus.Error);
        }

        public async ValueTask<Uri> GetRegisterLinkAsync(CancellationToken cancellationToken)
        {
            var domain = await siteProvider.GetMirrorAsync(cancellationToken).ConfigureAwait(false);
            return new Uri(domain, "user/register");
        }

        public async ValueTask<AuthResult> TryGetOrRefreshUserAsync(IEnumerable<Cookie> cookies, CancellationToken cancellationToken)
        {
            var user = GetUserOrNull(cookies);
            if (user?.AccessToken == null)
            {
                return new AuthResult(AuthStatus.Error);
            }

            var userInfo = await GetUserInfoAsync(user.AccessToken, cancellationToken).ConfigureAwait(false);
            if (userInfo.userName == null)
            {
                if (user.RefreshToken == null)
                {
                    return new AuthResult(AuthStatus.Error);
                }

                var (newAccessToken, newRefreshToken) = await RefreshAccessTokenAsync(user.AccessToken, user.RefreshToken, cancellationToken).ConfigureAwait(false);
                if (newAccessToken == null)
                {
                    return new AuthResult(AuthStatus.Error);
                }
                userInfo = await GetUserInfoAsync(newAccessToken, cancellationToken).ConfigureAwait(false);
                cookies = RefreshTokens(cookies, newAccessToken, newRefreshToken, userInfo.userName, userInfo.avatar, userInfo.endTime);
            }
            else
            {
                cookies = RefreshTokens(cookies, user.AccessToken, user.RefreshToken, userInfo.userName, userInfo.avatar, userInfo.endTime);
            }

            user = GetUserOrNull(cookies);
            if (user != null)
            {
                return new AuthResult(AuthStatus.Success, user, cookies);
            }
            if (cancellationToken.IsCancellationRequested)
            {
                return new AuthResult(AuthStatus.Canceled);
            }
            return new AuthResult(AuthStatus.Error);
        }

        private User? GetUserOrNull(IEnumerable<Cookie> cookies)
        {
            var access = cookies.FirstOrDefault(c => c.Name == AccessTokenKey)?.Value;
            var refresh = cookies.FirstOrDefault(c => c.Name == RefreshTokenKey)?.Value;
            var login = cookies.FirstOrDefault(c => c.Name == UserNameKey)?.Value ?? "unknown";
            var avatar = cookies.FirstOrDefault(c => c.Name == UserAvatarName)?.Value;
            var hasPro = cookies.FirstOrDefault(c => c.Name == EndTimeKey)?.Value is string endTimeStr
                && DateTimeOffset.TryParse(endTimeStr, out var endTime)
                && endTime > DateTimeOffset.UtcNow;

            if (access == null)
            {
                return null;
            }

            Uri.TryCreate(avatar, UriKind.Absolute, out var avatarUri);

            return new User(Site, login, avatarUri ?? defaultAvatar, access, refresh, hasPro);
        }

        public Task LogoutAsync(CancellationToken cancellationToken)
        {
            return siteProvider.PostAsync("device/unlink", null, cancellationToken);
        }

        private async Task<(string? userName, Uri? avatar, DateTimeOffset endTime)> GetUserInfoAsync(string accessToken, CancellationToken cancellationToken)
        {
            var domain = await siteProvider.GetMirrorAsync(cancellationToken).ConfigureAwait(false);
            var userInfo = await siteProvider.SendAsync(HttpMethod.Get, "user", accessToken, null, cancellationToken: cancellationToken).ConfigureAwait(false);
            var userName = userInfo?["user"]?["username"]?.ToString();
            var avatar = userInfo?["user"]?["profile"]?["avatar"]?.ToUriOrNull(domain);
            var days = userInfo?["user"]?["subscription"]?["days"]?.ToDoubleOrNull() ?? 10;
            var endTime = DateTimeOffset.Now.AddDays(days);

            return (userName, avatar, endTime);
        }

        private static IEnumerable<Cookie> RefreshTokens(
            IEnumerable<Cookie> cookies,
            string? access, string? refresh, string? userName = null, Uri? avatar = null, DateTimeOffset? endTime = default)
        {
            var newAccessCookie = access != null
                ? new Cookie(AccessTokenKey, access)
                : cookies.FirstOrDefault(c => c.Name == AccessTokenKey);

            var newRefreshCookie = refresh != null
                ? new Cookie(RefreshTokenKey, refresh)
                : cookies.FirstOrDefault(c => c.Name == RefreshTokenKey);

            var endTimeCookie = endTime.HasValue
                ? new Cookie(EndTimeKey, endTime.Value.ToString("O"))
                : cookies.FirstOrDefault(c => c.Name == EndTimeKey);

            var userNameCook = userName != null
                ? new Cookie(UserNameKey, userName)
                : cookies.FirstOrDefault(c => c.Name == UserNameKey);

            var avatarCook = avatar != null
                ? new Cookie(UserAvatarName, avatar.ToString())
                : cookies.FirstOrDefault(c => c.Name == UserAvatarName);

            return new[]
                {
                    newAccessCookie,
                    newRefreshCookie,
                    userNameCook,
                    avatarCook,
                    endTimeCookie
                }
                .Where(c => c != null);
        }

        private async Task<(string? accessToken, string? refreshToken)> RefreshAccessTokenAsync(
            string? accessToken, string refreshToken, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(Secrets.KinoPubClient) || string.IsNullOrEmpty(Secrets.KinoPubApiKey))
            {
                return default;
            }

            var response = await siteProvider.SendAsync(HttpMethod.Post, "/oauth2/token", accessToken, new Dictionary<string, string>
            {
                ["grant_type"] = "refresh_token",
                ["refresh_token"] = refreshToken,
                ["client_id"] = Secrets.KinoPubClient!,
                ["client_secret"] = Secrets.KinoPubApiKey!
            }, cancellationToken)
                .ConfigureAwait(false);
            if (response == null)
            {
                return default;
            }

            return (response["access_token"]?.ToString(),
                response["refresh_token"]?.ToString());
        }

        private Task<JObject?> PostAuthApiAsync(string type, string? code, CancellationToken cancellationToken)
        {
            if (Secrets.KinoPubClient == null
                || Secrets.KinoPubApiKey == null)
            {
                return Task.FromResult<JObject?>(null);
            }

            return siteProvider
                .SendAsync(HttpMethod.Post, "/oauth2/device", null, new Dictionary<string, string>
                {
                    ["grant_type"] = type,
                    ["code"] = code ?? string.Empty,
                    ["client_id"] = Secrets.KinoPubClient,
                    ["client_secret"] = Secrets.KinoPubApiKey
                }, cancellationToken);
        }

        private Task SendNotifyDevice(string accessToken, CancellationToken cancellationToken)
        {
            return siteProvider.SendAsync(HttpMethod.Post, "device/notify", accessToken,
                new Dictionary<string, string>
                {
                    ["title"] = appInformation.DisplayName,
                    ["software"] = $"{appInformation.DeviceFamily}/{appInformation.SystemVersion}",
                    ["hardware"] = string.IsNullOrEmpty(appInformation.DeviceModel)
                        ? appInformation.DeviceManufacturer
                        : $"{appInformation.DeviceManufacturer}/{appInformation.DeviceModel}"
                },
                cancellationToken);
        }
    }
}
