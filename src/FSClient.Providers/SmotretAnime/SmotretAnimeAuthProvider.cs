namespace FSClient.Providers
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;

    using FSClient.Shared.Helpers;
    using FSClient.Shared.Helpers.Web;
    using FSClient.Shared.Models;
    using FSClient.Shared.Providers;
    using FSClient.Shared.Services;

    public class SmotretAnimeAuthProvider : IAuthProvider
    {
        private readonly SmotretAnimeSiteProvider siteProvider;
        private readonly IContentDialog<LoginDialogOutput> loginDialog;

        public SmotretAnimeAuthProvider(
            SmotretAnimeSiteProvider siteProvider,
            IContentDialog<LoginDialogOutput> loginDlg)
        {
            this.siteProvider = siteProvider;
            loginDialog = loginDlg;

            AuthModels = new[]
            {
                new AuthModel
                {
                    Title = Site.Title
                }
            };
        }

        public Site Site => siteProvider.Site;

        public IEnumerable<AuthModel> AuthModels { get; }

        public async ValueTask<Uri> GetRegisterLinkAsync(CancellationToken cancellationToken)
        {
            var domain = await siteProvider.GetMirrorAsync(cancellationToken).ConfigureAwait(false);
            return new Uri(domain, "/users/login");
        }

        public ValueTask<AuthResult> TryGetOrRefreshUserAsync(IEnumerable<Cookie> cookies, CancellationToken cancellationToken)
        {
            var user = GetUserOrNull();
            var result = user != null
                ? new AuthResult(AuthStatus.Success, user, cookies)
                : new AuthResult(AuthStatus.Error);

            return new ValueTask<AuthResult>(result);
        }

        private User? GetUserOrNull()
        {
            foreach (var mirror in siteProvider.Mirrors)
            {
                var cooks = siteProvider.Handler.GetCookies(mirror);
                var csrf = cooks.FirstOrDefault(c => c.Name == "csrf");
                var guestId = cooks.FirstOrDefault(c => c.Name == "guestId");
                var profileName = cooks.FirstOrDefault(c => c.Name == "profileName");

                if (string.IsNullOrWhiteSpace(csrf?.Value) || csrf!.Expired
                    || string.IsNullOrWhiteSpace(guestId?.Value) || guestId!.Expired
                    || string.IsNullOrWhiteSpace(profileName?.Value) || profileName!.Expired)
                {
                    continue;
                }

                var avatar = cooks.FirstOrDefault(c => c.Name == "avatarSrc")?.Value.ToUriOrNull(mirror);

                // TODO: detect pro status
                return new User(Site, Uri.UnescapeDataString(profileName.Value), avatar, hasProStatus: true);
            }

            return null;
        }

        public async Task LogoutAsync(CancellationToken cancellationToken)
        {
            var domain = await siteProvider.GetMirrorAsync(cancellationToken).ConfigureAwait(false);

            await siteProvider
                .HttpClient
                .GetBuilder(new Uri(domain, "users/logout"))
                .WithAjax()
                .SendAsync(cancellationToken)
                .ConfigureAwait(false);
        }

        public async Task<AuthResult> AuthorizeAsync(AuthModel authModel, CancellationToken cancellationToken)
        {
            var result = await loginDialog.ShowAsync(cancellationToken).ConfigureAwait(false);

            if (result.Status != AuthStatus.Success)
            {
                return new AuthResult(result.Status);
            }

            var noRedirectHttpHanlder = new HttpClientHandler
            {
                CookieContainer = new CookieContainer(),
                UseCookies = true,
                AllowAutoRedirect = false
            };
            using var noRedirectHttpClient = new HttpClient(noRedirectHttpHanlder, true);

            var domain = await siteProvider.GetMirrorAsync(cancellationToken).ConfigureAwait(false);

            var loginPageResponse = await noRedirectHttpClient
                .GetBuilder(new Uri(domain, "users/login"))
                .WithArgument("dynpage", "1")
                .WithHeader("Referer", domain.ToString())
                .SendAsync(cancellationToken)
                .ConfigureAwait(false);
            var loginPageHtml = await loginPageResponse
                .AsHtml(cancellationToken)
                .ConfigureAwait(false);
            if (loginPageResponse == null || loginPageHtml == null)
            {
                return new AuthResult(AuthStatus.Error);
            }

            var args = loginPageHtml.QuerySelectorAll("#login-form input[type=hidden][name]")
                .Select(input => (
                    name: input.GetAttribute("name"),
                    value: input.GetAttribute("value")))
                .Where(tuple => tuple.name != null
                    && tuple.value != null
                    && tuple.name != "LoginForm[username]"
                    && tuple.name != "LoginForm[password]"
                    && tuple.name != "dynpage")
                .DistinctBy(tuple => tuple.name)
                .ToDictionary(tuple => tuple.name!, tuple => tuple.value!);

            args.Add("LoginForm[username]", result.Login ?? string.Empty);
            args.Add("LoginForm[password]", result.Password ?? string.Empty);
            args.Add("dynpage", "1");

            var loginResponse = await noRedirectHttpClient
                .PostBuilder(new Uri(domain, "users/login"))
                .WithBody(args)
                .WithHeader("Origin", domain.GetOrigin())
                .WithHeader("Referer", domain.ToString())
                .WithAjax()
                .SendAsync(cancellationToken)
                .ConfigureAwait(false);

            if (loginResponse == null
                || (!loginResponse.IsSuccessStatusCode
                && (int)loginResponse.StatusCode < 300 && (int)loginResponse.StatusCode >= 400))
            {
                return new AuthResult(AuthStatus.Error);
            }
            var cooks = loginResponse.GetCookies().Where(cook => cook.Name != "PHPSESSID")
                .Concat(noRedirectHttpHanlder.GetCookies(domain, "csrf", "guestId", "aaaa8ed0da05b797653c4bd51877d861"))
                .ToList();

            if (!cooks.Any(c => c.Name == "csrf")
                && loginPageResponse.GetCookies().FirstOrDefault(c => c.Name == "csrf") is Cookie csrfCook)
            {
                cooks.Add(csrfCook);
            }

            if (!cooks.Any(c => c.Name == "guestId")
                && loginPageResponse.GetCookies().FirstOrDefault(c => c.Name == "guestId") is Cookie guestIdCook)
            {
                cooks.Add(guestIdCook);
            }

            var cooksToCopy = noRedirectHttpHanlder.GetCookies(domain)
                .Select(c => new Cookie(c.Name, c.Value))
                .ToArray();
            foreach (var mirror in siteProvider.Mirrors)
            {
                siteProvider.Handler.SetCookies(mirror, cooksToCopy);
            }

            var profilePageHtml = await siteProvider
                .HttpClient
                .GetBuilder(new Uri(domain, "/users/profile"))
                .WithArgument("dynpage", "1")
                .WithCookies(noRedirectHttpHanlder.GetCookies(domain))
                .WithHeader("Referer", domain.ToString())
                .SendAsync(cancellationToken)
                .AsHtml(cancellationToken)
                .ConfigureAwait(false);
            var profileCard = profilePageHtml?.QuerySelector(".m-catalog-view .card-content");

            var avatarSrc = profileCard?.QuerySelector("img[src]")?.GetAttribute("src");
            var profileName = profileCard?.QuerySelector(".m-small-title")?.TextContent?.Trim();

            if (!string.IsNullOrEmpty(profileName)
                && siteProvider.Handler.SetCookie(domain, "profileName", Uri.EscapeDataString(profileName!)) is Cookie loginCook)
            {
                cooks.Add(loginCook);
                foreach (var mirror in siteProvider.Mirrors)
                {
                    siteProvider.Handler.SetCookies(mirror, loginCook);
                }
            }

            if (!string.IsNullOrEmpty(avatarSrc)
                && siteProvider.Handler.SetCookie(domain, "avatarSrc", avatarSrc!) is Cookie avatarCook)
            {
                cooks.Add(avatarCook);
                foreach (var mirror in siteProvider.Mirrors)
                {
                    siteProvider.Handler.SetCookies(mirror, avatarCook);
                }
            }

            var user = GetUserOrNull();
            if (user != null)
            {
                return new AuthResult(AuthStatus.Success, user, cooks);
            }

            return new AuthResult(AuthStatus.Error);
        }
    }
}
