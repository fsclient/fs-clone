namespace FSClient.Providers
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;

    using AngleSharp.Html.Dom;

    using FSClient.Shared.Helpers;
    using FSClient.Shared.Helpers.Web;
    using FSClient.Shared.Models;
    using FSClient.Shared.Providers;
    using FSClient.Shared.Services;

    using Newtonsoft.Json.Linq;

    public class FilmixAuthProvider : IAuthProvider
    {
        private const string HasProCookieName = "dle_has_pro";
        private const string AvatarCookieName = "die_avatarSrc";
        private const string UserNameCookieName = "dle_user_name";
        private const string PasswordCookieName = "dle_password";
        private const string UserIdCookieName = "dle_user_id";

        private readonly FilmixSiteProvider siteProvider;
        private readonly IContentDialog<OAuthDialogInput, OAuthDialogOutput> oauthDialog;
        private readonly IContentDialog<LoginDialogOutput> loginDialog;

        public FilmixAuthProvider(
            FilmixSiteProvider siteProvider,
            IContentDialog<OAuthDialogInput, OAuthDialogOutput> oauthDialog,
            IContentDialog<LoginDialogOutput> loginDialog)
        {
            this.siteProvider = siteProvider;
            this.oauthDialog = oauthDialog;
            this.loginDialog = loginDialog;

            AuthModels = new[]
            {
                new AuthModel
                {
                    Title = Site.Title,
                    IsOAuth = false
                }
            };
        }

        public IEnumerable<AuthModel> AuthModels { get; }

        public Site Site => siteProvider.Site;

        public Task<AuthResult> AuthorizeAsync(AuthModel authModel, CancellationToken cancellationToken)
        {
            if (authModel.IsOAuth)
            {
                return OAuthLoginAsync(authModel, cancellationToken);
            }

            return SimpleLoginAsync(authModel, cancellationToken);
        }

        private async Task<AuthResult> OAuthLoginAsync(AuthModel oAuthProvider, CancellationToken cancellationToken)
        {
            var domain = await siteProvider.GetMirrorAsync(cancellationToken).ConfigureAwait(false);

            var socData = await siteProvider.HttpClient
                .PostBuilder(new Uri(domain, "/api/auth/social_data"))
                .WithAjax()
                .WithHeader("Origin", domain.GetOrigin())
                .WithHeader("Referer", domain.ToString())
                .WithTimeSpanSemaphore(siteProvider.RequestSemaphore)
                .SendAsync(cancellationToken)
                .AsNewtonsoftJson<JObject>()
                .ConfigureAwait(false);

            Uri? startLink;
            switch (oAuthProvider.Title)
            {
                case "VK":
                    startLink = socData?["message"]?["vk"]?.ToUriOrNull(domain);
                    break;
                case "Facebook":
                    startLink = socData?["message"]?["fb"]?.ToUriOrNull(domain);
                    break;
                case "Google+":
                    startLink = socData?["message"]?["gg"]?.ToUriOrNull(domain);
                    break;
                case "Twitter":
                    startLink = socData?["message"]?["tw"]?.ToUriOrNull(domain);
                    break;
                default:
                    return new AuthResult(AuthStatus.Error);
            }

            if (startLink == null)
            {
                return new AuthResult(AuthStatus.Error);
            }

            var result = await oauthDialog
                .ShowAsync(
                    new OAuthDialogInput(startLink, domain),
                    cancellationToken)
                .ConfigureAwait(false);

            switch (result.Status)
            {
                case AuthStatus.Success:

                    if (result.Data?.ToUriOrNull() is Uri continueUri)
                    {
                        await siteProvider.HttpClient
                            .GetBuilder(continueUri)
                            .WithTimeSpanSemaphore(siteProvider.RequestSemaphore)
                            .SendAsync(HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                            .ConfigureAwait(false);
                    }

                    var userId = result
                        .Cookies
                        .Concat(siteProvider.Handler.GetCookies(domain))
                        .FirstOrDefault(c => c.Name == UserIdCookieName)?
                        .Value;

                    if (string.IsNullOrWhiteSpace(userId))
                    {
                        break;
                    }

                    var cooks = new[]
                    {
                        "PHPSESSID",
                        UserIdCookieName,
                        PasswordCookieName,
                        "dle_newpm",
                        "dle_hash"
                    };

                    var resultCooks = result.Cookies.Where(c => Array.IndexOf(cooks, c.Name) >= 0).ToList();
                    foreach (var mirror in siteProvider.Mirrors)
                    {
                        siteProvider.Handler.SetCookies(mirror, resultCooks.ToArray());
                    }

                    var html = await siteProvider
                        .HttpClient
                        .GetBuilder(domain)
                        .WithTimeSpanSemaphore(siteProvider.RequestSemaphore)
                        .SendAsync(cancellationToken)
                        .AsHtml(cancellationToken)
                        .ConfigureAwait(false);
                    if (html == null)
                    {
                        return new AuthResult(AuthStatus.Error);
                    }

                    var (hasAccount, hasPro) = await HasAccountAndProAsync(html, cancellationToken).ConfigureAwait(false);
                    if (!hasAccount)
                    {
                        return new AuthResult(AuthStatus.Error);
                    }

                    var loginCook = new Cookie(
                        UserNameCookieName,
                        html.QuerySelector(".login .user-name")?.TextContent.Trim());
                    var avatarCook = new Cookie(
                        AvatarCookieName,
                        html.QuerySelector(".login .avatar")?.GetAttribute("src"));

                    resultCooks.Add(loginCook);
                    resultCooks.Add(avatarCook);
                    resultCooks.Add(new Cookie(HasProCookieName, hasPro.ToString()));

                    var user = GetUserOrNull(domain, resultCooks);
                    if (user != null)
                    {
                        return new AuthResult(AuthStatus.Success, user, resultCooks);
                    }

                    break;
                case AuthStatus.Canceled:
                    return new AuthResult(AuthStatus.Canceled);
                case AuthStatus.Error:
                    return new AuthResult(AuthStatus.Error);
            }
            return new AuthResult(AuthStatus.Error);
        }

        private async Task<AuthResult> SimpleLoginAsync(AuthModel authModel, CancellationToken cancellationToken)
        {
            var domain = await siteProvider.GetMirrorAsync(cancellationToken).ConfigureAwait(false);

            var result = await loginDialog.ShowAsync(cancellationToken).ConfigureAwait(false);

            if (result.Status != AuthStatus.Success
                || string.IsNullOrWhiteSpace(result.Login)
                || string.IsNullOrWhiteSpace(result.Password))
            {
                return new AuthResult(result.Status);
            }

            await siteProvider.HttpClient.GetBuilder(domain).SendAsync(cancellationToken).ConfigureAwait(false);

            var response = await siteProvider
                .HttpClient
                .PostBuilder(new Uri(domain, "engine/ajax/user_auth.php"))
                .WithBody(new Dictionary<string, string>
                {
                    ["login_name"] = result.Login ?? string.Empty,
                    ["login_password"] = result.Password ?? string.Empty,
                    ["login_not_save"] = "1",
                    ["login"] = "submit"
                })
                .WithAjax()
                .WithHeader("Origin", domain.GetOrigin())
                .WithHeader("Referer", domain.ToString())
                .WithTimeSpanSemaphore(siteProvider.RequestSemaphore)
                .SendAsync(cancellationToken)
                .ConfigureAwait(false);
            if (response == null)
            {
                return new AuthResult(AuthStatus.Error);
            }

            var respCooks = response.GetCookies().Concat(siteProvider.Handler.GetCookies(domain));
            var userId = respCooks.FirstOrDefault();
            if (userId?.Value == null
                || userId.Expired)
            {
                return new AuthResult(AuthStatus.Error);
            }

            response = await siteProvider
                .HttpClient
                .GetBuilder(domain)
                .WithTimeSpanSemaphore(siteProvider.RequestSemaphore)
                .SendAsync(cancellationToken)
                .ConfigureAwait(false);
            var html = await response.AsHtml(cancellationToken).ConfigureAwait(false);

            if (response != null)
            {
                respCooks = response.GetCookies().Concat(respCooks);
            }

            var allowed = new[] { "PHPSESSID", "dle_hash", PasswordCookieName, "dle_newpm", UserIdCookieName };
            var resultCooks = respCooks
                .GroupBy(cook => cook.Name)
                .Select(group => group.First())
                .Where(c => Array.IndexOf(allowed, c.Name) >= 0).ToList();

            var (hasAccount, hasPro) = await HasAccountAndProAsync(html, cancellationToken).ConfigureAwait(false);
            if (!hasAccount)
            {
                return new AuthResult(AuthStatus.Error);
            }

            resultCooks.Add(new Cookie(UserNameCookieName, html?.QuerySelector(".login .user-name")?.TextContent.Trim() ?? result.Login));
            resultCooks.Add(new Cookie(AvatarCookieName, html?.QuerySelector(".login .avatar")?.GetAttribute("src") ?? authModel.Icon?.AbsoluteUri));
            resultCooks.Add(new Cookie(HasProCookieName, hasPro.ToString()));

            var user = GetUserOrNull(domain, resultCooks);
            if (user != null)
            {
                return new AuthResult(AuthStatus.Success, user, resultCooks);
            }
            return new AuthResult(AuthStatus.Error);
        }

        public async ValueTask<Uri> GetRegisterLinkAsync(CancellationToken cancellationToken)
        {
            var domain = await siteProvider.GetMirrorAsync(cancellationToken).ConfigureAwait(false);
            // There is no register page, but user can find the form on home page.
            return domain;
        }

        public async ValueTask<AuthResult> TryGetOrRefreshUserAsync(IEnumerable<Cookie> cookies, CancellationToken cancellationToken)
        {
            var domain = await siteProvider.GetMirrorAsync(cancellationToken);

            var user = GetUserOrNull(domain, cookies);
            if (user == null)
            {
                return new AuthResult(AuthStatus.Error);
            }

            var (hasAccount, hasPro) = await HasAccountAndProAsync(null, cancellationToken).ConfigureAwait(false);
            if (!hasAccount)
            {
                return new AuthResult(AuthStatus.Error);
            }
            cookies = cookies.Where(c => c.Name != HasProCookieName).Concat(new[] { new Cookie(HasProCookieName, hasPro.ToString()) });

            user = GetUserOrNull(domain, cookies);
            return user != null
                ? new AuthResult(AuthStatus.Success, user, cookies)
                : new AuthResult(AuthStatus.Error);
        }

        private User? GetUserOrNull(Uri domain, IEnumerable<Cookie> cooks)
        {
            var userIdCook = cooks.FirstOrDefault(c => c.Name == UserIdCookieName);
            var passCook = cooks.FirstOrDefault(c => c.Name == PasswordCookieName);
            var loginName = cooks.FirstOrDefault(c => c.Name == UserNameCookieName);

            if (string.IsNullOrWhiteSpace(userIdCook?.Value) || userIdCook!.Expired
                || string.IsNullOrWhiteSpace(passCook?.Value) || passCook!.Expired
                || string.IsNullOrWhiteSpace(loginName?.Value) || loginName!.Expired)
            {
                return null;
            }

            _ = bool.TryParse(cooks.FirstOrDefault(c => c.Name == HasProCookieName)?.Value, out var hasPro);
            var avatar = cooks.FirstOrDefault(c => c.Name == AvatarCookieName)?.Value?.ToUriOrNull(domain);

            return new User(Site, loginName.Value, avatar, hasProStatus: hasPro);
        }

        public async Task LogoutAsync(CancellationToken cancellationToken)
        {
            var domain = await siteProvider.GetMirrorAsync(cancellationToken).ConfigureAwait(false);

            await siteProvider
                .HttpClient
                .GetBuilder(domain)
                .WithArgument("action", "logout")
                .WithAjax()
                .WithTimeSpanSemaphore(siteProvider.RequestSemaphore)
                .SendAsync(cancellationToken)
                .ConfigureAwait(false);
        }

        private async Task<(bool account, bool pro)> HasAccountAndProAsync(IHtmlDocument? html, CancellationToken cancellationToken)
        {
            if (html == null)
            {
                var domain = await siteProvider.GetMirrorAsync(CancellationToken.None)
                   .ConfigureAwait(false);

                html = await siteProvider.HttpClient.GetBuilder(domain)
                    .WithTimeSpanSemaphore(siteProvider.RequestSemaphore)
                    .SendAsync(CancellationToken.None)
                    .AsHtml(cancellationToken)
                    .ConfigureAwait(false);
            }

            if (html == null)
            {
                return (false, false);
            }

            return (
                html.QuerySelector("a.user-profile[href]") != null,
                html.QuerySelector(".login-pro.proplus") != null
            );
        }
    }
}
