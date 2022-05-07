namespace FSClient.UWP.Shared.Views.Dialogs
{
    using System;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;

    using Windows.Security.Authentication.Web;
    using Windows.Web.Http;
    using Windows.Web.Http.Filters;

#if WINUI3
    using Microsoft.UI.Xaml.Controls;
#else
    using System.Linq;

    using Windows.UI.Xaml;
    using Windows.UI.Xaml.Controls;
#endif

    using FSClient.Shared.Providers;
    using FSClient.Shared.Services;
    using FSClient.UWP.Shared.Helpers;

    public sealed partial class OAuthDialog : ContentDialog, IContentDialog<OAuthDialogInput, OAuthDialogOutput>
    {
        private readonly HttpCookieManager cookieManager;

#pragma warning disable CA1823 // Avoid unused private fields
#pragma warning disable CS0169 // The field is never used
#pragma warning disable IDE0044 // Add readonly modifier
        private readonly object? webView;
        private OAuthDialogInput? oAuthArgument;
        private AuthStatus responseStatus;
        private string? responseData;
#pragma warning restore CA1823 // Avoid unused private fields
#pragma warning restore CS0169 // The field is never used
#pragma warning restore IDE0044 // Add readonly modifier

        public OAuthDialog()
        {
            var httpBaseProtocolFilter = new HttpBaseProtocolFilter();
            cookieManager = httpBaseProtocolFilter.CookieManager;

            InitializeComponent();

#if UAP
            var webView = new WebView();
            webView.NavigationStarting += WebView_NavigationStarting;
            webView.ContentLoading += WebView_ContentLoading;
            webView.NavigationFailed += WebView_NavigationFailed;
            webView.MinWidth = Math.Min(320, Window.Current.Bounds.Width - 60);
            webView.MinHeight = Math.Min(500, Window.Current.Bounds.Height - 40);
            Window.Current.SizeChanged += (s, e) =>
            {
                webView.MinWidth = Math.Min(webView.MinWidth, Window.Current.Bounds.Width - 60);
                webView.MinHeight = Math.Min(webView.MinHeight, Window.Current.Bounds.Height - 100);
            };
            this.webView = webView;
            RootGrid.Children.Add(webView);
#endif
        }

        private void ContentDialog_SecondaryButtonClick(ContentDialog _, ContentDialogButtonClickEventArgs args)
        {
            responseStatus = AuthStatus.Canceled;
        }

#if UAP
        private void WebView_NavigationStarting(WebView _, WebViewNavigationStartingEventArgs e)
        {
            if (oAuthArgument?.RequestCookies != null)
            {
                var cooks = cookieManager.GetCookies(oAuthArgument.EndLink);
                var hasAll = oAuthArgument.RequestCookies.All(k => cooks.Any(c => c.Name == k));
                if (hasAll)
                {
                    responseStatus = AuthStatus.Success;
                    responseData = e.Uri?.AbsoluteUri;
                    Hide();
                }
            }
        }

        private void WebView_ContentLoading(WebView _, WebViewContentLoadingEventArgs e)
        {
            if (e.Uri != null
                && e.Uri.AbsoluteUri == oAuthArgument?.EndLink?.AbsoluteUri
                && e.Uri.AbsoluteUri != oAuthArgument?.StartLink?.AbsoluteUri)
            {
                responseStatus = AuthStatus.Success;
                responseData = e.Uri.AbsoluteUri;
                Hide();
            }
        }

        private void WebView_NavigationFailed(object _, WebViewNavigationFailedEventArgs e)
        {
            if (oAuthArgument?.EndLink != null
                && e.Uri?.AbsoluteUri.StartsWith(oAuthArgument.EndLink.AbsoluteUri, StringComparison.OrdinalIgnoreCase) == true)
            {
                responseStatus = AuthStatus.Success;
                responseData = e.Uri.AbsoluteUri;
            }
            else
            {
                responseStatus = AuthStatus.Error;
            }

            Hide();
        }
#endif

        public Task<OAuthDialogOutput> ShowAsync(OAuthDialogInput arg, CancellationToken cancellationToken)
        {
            return Dispatcher.CheckBeginInvokeOnUI(async () =>
            {
                try
                {
#if UAP
                    if (arg.StartLink?.Scheme != "https"
                        || arg.EndLink?.Scheme != "https"
                        || arg.RequestCookies.Any())
                    {
                        oAuthArgument = arg;
                        foreach (var cookie in cookieManager.GetCookies(arg.StartLink))
                        {
                            cookieManager.DeleteCookie(cookie);
                        }

                        ((WebView?)webView)!.Navigate(arg.StartLink);
                        await this.ShowAsync(cancellationToken).ConfigureAwait(true);

                        var status = cancellationToken.IsCancellationRequested
                            ? AuthStatus.Canceled
                            : responseStatus;
                        var cookies = cookieManager
                            .GetCookies(arg.EndLink)
                            .Select(ConvertToCookie);
                        var result = new OAuthDialogOutput(status, responseData, cookies);

                        ((WebView)webView).NavigateToString("<html></html>");

                        return result;
                    }
#endif

                    var webAuthenticationResult = await WebAuthenticationBroker
                        .AuthenticateAsync(
                            WebAuthenticationOptions.None,
                            arg.StartLink,
                            arg.EndLink)
                        .AsTask(cancellationToken)
                        .ConfigureAwait(false);

                    return webAuthenticationResult.ResponseStatus switch
                    {
                        WebAuthenticationStatus.Success => new OAuthDialogOutput(AuthStatus.Success,
                            webAuthenticationResult.ResponseData),
                        WebAuthenticationStatus.UserCancel => new OAuthDialogOutput(AuthStatus.Canceled),
                        WebAuthenticationStatus.ErrorHttp => new OAuthDialogOutput(AuthStatus.Error,
                            webAuthenticationResult.ResponseErrorDetail.ToString()),

                        _ => throw new NotSupportedException(
                            $"{nameof(WebAuthenticationStatus)}.{webAuthenticationResult.ResponseStatus} is not supported"),
                    };
                }
                catch (Exception ex)
                {
                    return new OAuthDialogOutput(AuthStatus.Error, ex.Message);
                }
            });
        }

        public static Cookie ConvertToCookie(HttpCookie cookie)
        {
            return new Cookie(cookie.Name, cookie.Value, cookie.Path)
            {
                Expires = cookie.Expires?.DateTime ?? DateTime.MaxValue,
                Domain = cookie.Domain,
                Secure = cookie.Secure,
                HttpOnly = cookie.HttpOnly
            };
        }
    }
}
