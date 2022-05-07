namespace FSClient.Providers
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;

    using FSClient.Shared.Helpers.Web;
    using FSClient.Shared.Models;
    using FSClient.Shared.Providers;
    using FSClient.Shared.Services;

    public class SeasonVarAuthProvider : IAuthProvider
    {
        private readonly SeasonVarSiteProvider siteProvider;
        private readonly IContentDialog<LoginDialogOutput> loginDialog;

        public SeasonVarAuthProvider(
            SeasonVarSiteProvider siteProvider,
            IContentDialog<LoginDialogOutput> loginDialog)
        {
            this.siteProvider = siteProvider;
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

        public Site Site => siteProvider.Site;

        public IEnumerable<AuthModel> AuthModels { get; }

        public async ValueTask<Uri> GetRegisterLinkAsync(CancellationToken cancellationToken)
        {
            var domain = await siteProvider.GetMirrorAsync(cancellationToken).ConfigureAwait(false);
            return new Uri(domain, "/?mod=reg");
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
                var svid1Cook = cooks.FirstOrDefault(c => c.Name == "svid1");
                var loginName = cooks.FirstOrDefault(c => c.Name == "loginname");

                if (string.IsNullOrWhiteSpace(svid1Cook?.Value) || svid1Cook!.Expired
                    || string.IsNullOrWhiteSpace(loginName?.Value) || loginName!.Expired)
                {
                    continue;
                }

                var avatarSrc = cooks.FirstOrDefault(c => c.Name == "avatarSrc")?.Value;
                Uri.TryCreate(mirror, avatarSrc, out var avatar);

                return new User(Site, loginName.Value, avatar);
            }

            return null;
        }

        public async Task LogoutAsync(CancellationToken cancellationToken)
        {
            var domain = await siteProvider.GetMirrorAsync(cancellationToken).ConfigureAwait(false);

            await siteProvider
                .HttpClient
                .GetBuilder(domain)
                .WithArgument("mod", "logout")
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

            var domain = await siteProvider.GetMirrorAsync(cancellationToken).ConfigureAwait(false);

            var noRedirectHttpHanlder = new HttpClientHandler
            {
                CookieContainer = new CookieContainer(),
                UseCookies = true,
                AllowAutoRedirect = false
            };
            using var noRedirectHttpClient = new HttpClient(noRedirectHttpHanlder, true);

            var response = await noRedirectHttpClient
                .PostBuilder(domain)
                .WithArgument("mod", "login")
                .WithBody(new Dictionary<string, string>
                {
                    ["login"] = result.Login ?? string.Empty,
                    ["password"] = result.Password ?? string.Empty
                })
                .SendAsync(HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);
            if (response == null)
            {
                return new AuthResult(AuthStatus.Error);
            }

            var cooks = noRedirectHttpHanlder.GetCookies(domain, "svid1", "__cfduid").ToList();
            if (cooks.Count < 2)
            {
                return new AuthResult(AuthStatus.Error);
            }

            foreach (var mirror in siteProvider.Mirrors)
            {
                siteProvider.Handler.SetCookies(mirror, cooks.ToArray());
            }

            var html = await siteProvider.HttpClient
                .GetBuilder(domain)
                .SendAsync(cancellationToken)
                .AsHtml(cancellationToken)
                .ConfigureAwait(false);

            var loginName = html?.QuerySelector(".headblock .headmenu-title")?.TextContent?.Trim();
            var avatarSrc = html?.QuerySelector(".headblock img[alt=avatar]")?.GetAttribute("src");

            if (!string.IsNullOrEmpty(loginName)
                && siteProvider.Handler.SetCookie(domain, "loginname", loginName!) is Cookie loginCook)
            {
                cooks.Add(loginCook);
            }

            if (!string.IsNullOrEmpty(avatarSrc)
                && siteProvider.Handler.SetCookie(domain, "avatarSrc", avatarSrc!) is Cookie avatarCook)
            {
                cooks.Add(avatarCook);
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
