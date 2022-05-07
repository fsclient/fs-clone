namespace FSClient.Providers
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

    public class ExFSAuthProvider : IAuthProvider
    {
        private readonly ExFSSiteProvider siteProvider;
        private readonly IContentDialog<OAuthDialogInput, OAuthDialogOutput> oauthDialog;
        private readonly IContentDialog<LoginDialogOutput> loginDialog;

        public ExFSAuthProvider(
            ExFSSiteProvider siteProvider,
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

        public Site Site => siteProvider.Site;

        public IEnumerable<AuthModel> AuthModels { get; }

        public async ValueTask<Uri> GetRegisterLinkAsync(CancellationToken cancellationToken)
        {
            var domain = await siteProvider.GetMirrorAsync(cancellationToken).ConfigureAwait(false);
            return new Uri(domain, "/index.php?do=register");
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
                var userIdCook = cooks.FirstOrDefault(c => c.Name == "dle_user_id");
                var passCook = cooks.FirstOrDefault(c => c.Name == "dle_password");
                var newpmCook = cooks.FirstOrDefault(c => c.Name == "dle_newpm");
                var loginName = cooks.FirstOrDefault(c => c.Name == "die_loginname");

                if (string.IsNullOrWhiteSpace(userIdCook?.Value) || userIdCook!.Expired
                    || string.IsNullOrWhiteSpace(passCook?.Value) || passCook!.Expired
                    || string.IsNullOrWhiteSpace(newpmCook?.Value) || newpmCook!.Expired
                    || string.IsNullOrWhiteSpace(loginName?.Value) || loginName!.Expired)
                {
                    continue;
                }

                var avatarSrc = cooks.FirstOrDefault(c => c.Name == "die_avatarSrc")?.Value;
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
                .GetBuilder(new Uri(domain, "index.php"))
                .WithArgument("action", "logout")
                .WithAjax()
                .WithTimeSpanSemaphore(siteProvider.RequestSemaphore)
                .SendAsync(cancellationToken)
                .ConfigureAwait(false);
        }

        public Task<AuthResult> AuthorizeAsync(AuthModel authModel, CancellationToken cancellationToken)
        {
            if (authModel.IsOAuth)
            {
                return OAuthLoginAsync(authModel, cancellationToken);
            }

            return SimpleLoginAsync(cancellationToken);
        }

        private async Task<AuthResult> OAuthLoginAsync(AuthModel oAuthProvider, CancellationToken cancellationToken)
        {
            var domain = await siteProvider.GetMirrorAsync(cancellationToken).ConfigureAwait(false);

            var result = await oauthDialog
                .ShowAsync(
                    new OAuthDialogInput(
                        new Uri(domain, oAuthProvider.AuthLink),
                        domain,
                        new[] { "dle_user_id" }),
                    cancellationToken)
                .ConfigureAwait(false);

            switch (result.Status)
            {
                case AuthStatus.Success:

                    var userId = result
                        .Cookies
                        .Concat(siteProvider.Handler.GetCookies(domain))
                        .FirstOrDefault(c => c.Name == "dle_user_id")?
                        .Value;

                    if (string.IsNullOrWhiteSpace(userId))
                    {
                        break;
                    }

                    var cooks = new[]
                    {
                        "dle_user_id",
                        "dle_password",
                        "dle_newpm"
                    };
                    var resultCooks = result.Cookies.Where(c => Array.IndexOf(cooks, c.Name) >= 0).ToList();
                    foreach (var mirror in siteProvider.Mirrors)
                    {
                        siteProvider.Handler.SetCookies(mirror, resultCooks.ToArray());
                    }

                    var html = await siteProvider
                        .HttpClient
                        .GetBuilder(domain)
                        .WithAjax()
                        .WithTimeSpanSemaphore(siteProvider.RequestSemaphore)
                        .SendAsync(cancellationToken)
                        .AsHtml(cancellationToken)
                        .ConfigureAwait(false);

                    var loginName = html?.QuerySelector(".loginbox h4")?.TextContent;
                    var avatarSrc = html?.QuerySelector(".loginbox img")?.GetAttribute("src");

                    if (!string.IsNullOrEmpty(loginName)
                        && siteProvider.Handler.SetCookie(domain, "die_loginname", loginName!) is Cookie loginCook)
                    {
                        resultCooks.Add(loginCook);
                    }

                    if (!string.IsNullOrEmpty(avatarSrc)
                        && siteProvider.Handler.SetCookie(domain, "die_avatarSrc", avatarSrc!) is Cookie avatarCook)
                    {
                        resultCooks.Add(avatarCook);
                    }

                    var user = GetUserOrNull();
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

        private async Task<AuthResult> SimpleLoginAsync(CancellationToken cancellationToken)
        {
            var result = await loginDialog.ShowAsync(cancellationToken).ConfigureAwait(false);

            if (result.Status != AuthStatus.Success)
            {
                return new AuthResult(result.Status);
            }

            var domain = await siteProvider.GetMirrorAsync(cancellationToken).ConfigureAwait(false);

            var html = await siteProvider
                .HttpClient
                .PostBuilder(domain)
                .WithBody(new Dictionary<string, string>
                {
                    ["login_name"] = result.Login ?? string.Empty,
                    ["login_password"] = result.Password ?? string.Empty,
                    ["login"] = "submit"
                })
                .WithAjax()
                .WithTimeSpanSemaphore(siteProvider.RequestSemaphore)
                .SendAsync(cancellationToken)
                .AsHtml(cancellationToken)
                .ConfigureAwait(false);

            var userId = siteProvider.Handler.GetCookies(domain, "dle_user_id").FirstOrDefault();
            var loginName = html?.QuerySelector(".loginbox h4")?.TextContent;
            if (userId?.Value == null
                || userId.Expired
                || loginName == null)
            {
                return new AuthResult(AuthStatus.Error);
            }

            var avatarSrc = html?.QuerySelector(".loginbox img")?.GetAttribute("src");

            var cooks = siteProvider.Handler.GetCookies(domain, "dle_user_id", "dle_password", "dle_newpm").ToList();

            if (!string.IsNullOrEmpty(loginName)
                && siteProvider.Handler.SetCookie(domain, "die_loginname", loginName!) is Cookie loginCook)
            {
                cooks.Add(loginCook);
            }

            if (!string.IsNullOrEmpty(avatarSrc)
                && siteProvider.Handler.SetCookie(domain, "die_avatarSrc", avatarSrc!) is Cookie avatarCook)
            {
                cooks.Add(avatarCook);
            }

            var user = GetUserOrNull();
            if (user == null)
            {
                return new AuthResult(AuthStatus.Error);
            }
            else
            {
                return new AuthResult(AuthStatus.Success, user, cooks);
            }
        }
    }
}
