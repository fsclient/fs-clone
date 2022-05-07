namespace FSClient.Shared.Helpers.Web
{
    using FSClient.Shared.Helpers.TimeSpanSemaphore;

    using System;
    using System.Collections.Generic;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;

    public static class MirrorHelper
    {
        private static readonly HttpClient HttpClient = new HttpClient(new HttpClientHandler
        {
            AllowAutoRedirect = false
        });

        public static async Task<bool> IsAvailableAsync(this Uri link, CancellationToken cancellationToken = default)
        {
            var (_, _, isAvailable) = await IsAvailableWithLocationAsync(link, HttpMethod.Get, null, null, cancellationToken).ConfigureAwait(false);
            return isAvailable;
        }

        public static async Task<bool> IsAvailableAsync(this Uri link, IReadOnlyDictionary<string, string> headers, CancellationToken cancellationToken = default)
        {
            var (_, _, isAvailable) = await IsAvailableWithLocationAsync(link, HttpMethod.Get, headers, null, cancellationToken).ConfigureAwait(false);
            return isAvailable;
        }

        public static async Task<bool> IsAvailableAsync(this Uri link, Func<HttpResponseMessage, CancellationToken, ValueTask<bool>> validator, CancellationToken cancellationToken = default)
        {
            var (_, _, isAvailable) = await IsAvailableWithLocationAsync(link, HttpMethod.Get, null, validator, cancellationToken).ConfigureAwait(false);
            return isAvailable;
        }

        public static async Task<(Uri locationLink, HttpResponseMessage? response, bool isAvailable)> IsAvailableWithLocationAsync(
            this Uri link, HttpMethod httpMethod, IReadOnlyDictionary<string, string>? headers, Func<HttpResponseMessage, CancellationToken, ValueTask<bool>>? validator, CancellationToken cancellationToken = default,
            ITimeSpanSemaphore? timeSpanSemaphore = null)
        {
            if (cancellationToken.IsCancellationRequested
                || !link.IsAbsoluteUri)
            {
                return (link, null, false);
            }

            var response = await HttpClient
                .RequestBuilder(httpMethod, link)
                .WithHeaders(headers ?? new Dictionary<string, string>())
                .WithTimeSpanSemaphore(timeSpanSemaphore)
                .WithFollowRedirects(false)
                .SendAsync(HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);
            if (response == null)
            {
                return (link, response, false);
            }

            var statusCode = (int)response.StatusCode;
            if (response.Headers.Location is Uri location
                && location.IsAbsoluteUri
                && statusCode >= 301
                && statusCode <= 307
                && !cancellationToken.IsCancellationRequested
                && link.GetRootDomain() == location.GetRootDomain())
            {
                response = await HttpClient
                    .RequestBuilder(httpMethod, location)
                    .WithHeaders(headers ?? new Dictionary<string, string>())
                    .WithTimeSpanSemaphore(timeSpanSemaphore)
                    .SendAsync(HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                    .ConfigureAwait(false);
            }

            var locationLink = response?.Headers.Location ?? link;

            if (response == null
                || cancellationToken.IsCancellationRequested)
            {
                return (locationLink, response, false);
            }

            if (validator != null)
            {
                return (locationLink, response, await validator.Invoke(response, cancellationToken).ConfigureAwait(false));
            }

            return (locationLink, response, response.IsSuccessStatusCode);
        }
    }
}
