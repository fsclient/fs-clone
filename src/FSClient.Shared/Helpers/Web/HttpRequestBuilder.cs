namespace FSClient.Shared.Helpers.Web
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Net.Http.Json;
    using System.Text;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;

    using CloudflareSolverRe;
    using CloudflareSolverRe.Types;

    using FSClient.Shared.Helpers.TimeSpanSemaphore;
    using FSClient.Shared.Services;

    using Microsoft.Extensions.Logging;

    public class HttpRequestBuilder
    {
        private readonly HttpClient client;
        private HttpRequestMessage request;
        private readonly ILogger logger;
        private Dictionary<string, string?>? requestHeaders;
        private ITimeSpanSemaphore? timeSpanSemaphore;
        private TimeSpan? oneTimeRetryOn429TimeSpan;
        private TimeSpan maxPossibleRetryLater = TimeSpan.FromSeconds(10);
        private Action<Exception>? catchException;
        private Func<HttpContent>? contentBuilder;
        private bool followRedirects;

        public HttpRequestBuilder(HttpClient httpClient, HttpMethod method, Uri uri)
        {
            if (uri == null)
            {
                throw new ArgumentNullException(nameof(uri));
            }
            if (!uri.IsAbsoluteHttpUri())
            {
                throw new ArgumentException($"Argument {nameof(uri)} must be absolute http uri");
            }

            client = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            oneTimeRetryOn429TimeSpan = TimeSpan.FromMilliseconds(200);
            followRedirects = true;

            request = new HttpRequestMessage(method ?? HttpMethod.Get, uri);
            request.Headers.Accept.ParseAdd("*/*");
            request.Headers.AcceptCharset.ParseAdd("utf-8,*;q=0.5");
            request.Headers.UserAgent.ParseAdd(WebHelper.DefaultUserAgent);

            logger = Logger.Instance;
        }

        public async Task<HttpResponseMessage?> SendAsync(HttpCompletionOption httpCompletionOption, CancellationToken cancellationToken, bool throwOnError = false)
        {
            HttpResponseMessage? response = null;
            IDisposable? lockedSemaphore = null;

            request.Content = contentBuilder?.Invoke();

            if (requestHeaders is not null)
            {
                foreach (var pair in requestHeaders)
                {
                    var name = pair.Key;
                    var value = pair.Value;
                    var headers = name.StartsWith("Content-", StringComparison.OrdinalIgnoreCase)
                        ? (HttpHeaders?)request.Content?.Headers
                        : request.Headers;
                    if (headers == null)
                    {
                        continue;
                    }

                    if (headers.Contains(name))
                    {
                        headers.Remove(name);
                    }

                    if (!string.IsNullOrEmpty(value))
                    {
                        headers.TryAddWithoutValidation(name, value);
                    }
                }
            }

            try
            {
                if (timeSpanSemaphore is ITimeSpanSemaphore semaphore)
                {
                    lockedSemaphore = await semaphore.LockAsync(cancellationToken).ConfigureAwait(false);
                }

                response = await client.SendAsync(request, httpCompletionOption, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                if (throwOnError)
                {
                    throw;
                }
                return null;
            }
            catch (Exception ex)
            {
#if DEBUG
                if (ex is ObjectDisposedException)
                {
                    System.Diagnostics.Debugger.Break();
                }
#endif

                catchException?.Invoke(ex);
                if (ex is HttpRequestException)
                {
                    logger.LogWarning(ex);
                }
                else
                {
                    logger.LogError(ex);
                }

                if (throwOnError)
                {
                    throw;
                }

                return null;
            }
            finally
            {
                lockedSemaphore?.Dispose();
                lockedSemaphore = null;

                if (Settings.Instance.TraceHttp)
                {
                    logger.TraceHttp(request, response, contentBuilder?.Invoke(), Settings.Instance.TraceHttpWithCookies);
                }
                request.Dispose();
            }

            if (CloudflareDetector.IsClearanceRequired(response))
            {
                try
                {
                    var cf = new CloudflareSolver
                    {
                        MaxTries = 1,
                        ClearanceDelay = 4000
                    };
                    var result = await cf.Solve(request.RequestUri, WebHelper.DefaultUserAgent, null, cancellationToken).ConfigureAwait(false);
                    if (!result.Success
                        || result.DetectResult.Protection == CloudflareProtection.NoProtection)
                    {
                        return response;
                    }

                    request = request.CreateCopy();
                    if (result.Cookies != null)
                    {
                        request.Headers.TryAddWithoutValidation("Cookie", result.Cookies.AsHeaderString());
                    }
                }
                catch (OperationCanceledException)
                {
                    if (throwOnError)
                    {
                        throw;
                    }
                    return null;
                }
                catch (Exception ex)
                {
                    catchException?.Invoke(ex);
                    if (ex is HttpRequestException)
                    {
                        logger.LogWarning(ex);
                    }
                    else
                    {
                        logger.LogError(ex);
                    }

                    if (throwOnError)
                    {
                        throw;
                    }

                    return null;
                }

                return await SendAsync(httpCompletionOption, cancellationToken).ConfigureAwait(false);
            }
            else if (response is { StatusCode: >= (HttpStatusCode)300 and < (HttpStatusCode)400 }
                && response.Headers.Location is Uri location
                && followRedirects)
            {
                if (!location.IsAbsoluteUri)
                {
                    location = new Uri(request.RequestUri.GetLeftPart(UriPartial.Authority) + location);
                }
                if (request.RequestUri == location
                    && request.Method != HttpMethod.Get)
                {
                    request.Method = HttpMethod.Get;
                }
                request = request.CreateCopy();
                request.RequestUri = location;
                return await SendAsync(httpCompletionOption, cancellationToken).ConfigureAwait(false);
            }
            else if (response is { StatusCode: (HttpStatusCode)429 })
            {
                var retryAfterHeader = response?.Headers?.RetryAfter;
                var delay = oneTimeRetryOn429TimeSpan;
                if (retryAfterHeader?.Delta is TimeSpan delta)
                {
                    delay = delta;
                }
                else if (retryAfterHeader?.Date is DateTimeOffset date)
                {
                    delay = date - DateTimeOffset.Now;
                }

                if (delay is TimeSpan timeSpan)
                {
                    oneTimeRetryOn429TimeSpan = null;

                    await Task.Delay((int)Math.Min(timeSpan.TotalMilliseconds, maxPossibleRetryLater.TotalMilliseconds));

                    request = request.CreateCopy();
                    return await SendAsync(httpCompletionOption, cancellationToken).ConfigureAwait(false);
                }
            }

            return response;
        }

        public Task<HttpResponseMessage?> SendAsync(CancellationToken cancellationToken, bool throwOnError = false)
        {
            return SendAsync(HttpCompletionOption.ResponseContentRead, cancellationToken, throwOnError);
        }

        public HttpRequestBuilder Catch<TException>(Action<TException> catchException)
            where TException : Exception
        {
            this.catchException = ex =>
            {
                if (ex is TException tEx)
                {
                    catchException(tEx);
                }
            };
            return this;
        }

        public HttpRequestBuilder WithFollowRedirects(bool follow)
        {
            followRedirects = follow;
            return this;
        }

        public HttpRequestBuilder WithTimeSpanSemaphore(ITimeSpanSemaphore? semaphore)
        {
            timeSpanSemaphore = semaphore;
            return this;
        }

        public HttpRequestBuilder WithArguments(IEnumerable<KeyValuePair<string, string?>>? args)
        {
            if (args != null)
            {
                var query = QueryStringHelper.CreateQueryString(args);
                AppendToQuery(query);
            }

            return this;
        }

        public HttpRequestBuilder WithArgument(string name, string? value)
        {
            var argumentPair = QueryStringHelper.CreateArgumentPair(name, value);
            AppendToQuery(argumentPair);

            return this;
        }

        public HttpRequestBuilder WithBody(IEnumerable<KeyValuePair<string, string>>? args)
        {
            if (args == null)
            {
                contentBuilder = null;
                return this;
            }

            contentBuilder = () => new FormUrlEncodedContent(args!);
            return this;
        }

        public HttpRequestBuilder WithBody(JsonDocument jToken, string mediaType = "application/json")
        {
            contentBuilder = () => JsonContent.Create(jToken, new MediaTypeHeaderValue(mediaType));
            return this;
        }

        public HttpRequestBuilder WithBody(StringBuilder stringBuilder, string mediaType = "application/x-www-form-urlencoded")
        {
            return WithBody(stringBuilder.ToString(), mediaType);
        }

        public HttpRequestBuilder WithBody(string stringContent, string mediaType = "application/x-www-form-urlencoded")
        {
            contentBuilder = () => new StringContent(stringContent, Encoding.UTF8, mediaType);
            return this;
        }

        public HttpRequestBuilder WithHeader(string name, string? value)
        {
            requestHeaders ??= new Dictionary<string, string?>();
            requestHeaders[name] = value;

            return this;
        }

        public HttpRequestBuilder WithHeaders(IEnumerable<KeyValuePair<string, string>> headers)
        {
            requestHeaders ??= new Dictionary<string, string?>();
            foreach (var header in headers)
            {
                requestHeaders[header.Key] = header.Value;
            }

            return this;
        }

        public HttpRequestBuilder WithBasicAuthorization(string username, string password)
        {
            var byteArray = Encoding.ASCII.GetBytes($"{username}:{password}");
            var base64Str = Convert.ToBase64String(byteArray);

            requestHeaders ??= new Dictionary<string, string?>();
            requestHeaders["Authorization"] = $"Basic {base64Str}";

            return this;
        }

        public HttpRequestBuilder WithAjax(bool ajax = true)
        {
            if (ajax)
            {
                request.Headers.Add("X-Requested-With", "XMLHttpRequest");
            }
            else
            {
                request.Headers.Remove("X-Requested-With");
            }

            return this;
        }

        public HttpRequestBuilder WithCookies(IEnumerable<Cookie> cookies)
        {
            request.Headers.TryAddWithoutValidation("Cookie", cookies.ToCookieString());
            return this;
        }

        private void AppendToQuery(string query)
        {
            if (request.RequestUri == null)
            {
                return;
            }

            try
            {
                if (request.RequestUri.IsAbsoluteUri)
                {
                    var uriBuilder = new UriBuilder(request.RequestUri);
                    if (string.IsNullOrEmpty(uriBuilder.Query))
                    {
                        uriBuilder.Query = query;
                    }
                    else
                    {
                        uriBuilder.Query = $"{uriBuilder.Query.TrimStart('?')}&{query}";
                    }
                    request.RequestUri = uriBuilder.Uri;
                }
                else
                {
                    var strUri = request.RequestUri.ToString();
                    request.RequestUri = new Uri(
                        strUri + (strUri.Contains("?") ? "&" : "?") + query,
                        UriKind.RelativeOrAbsolute);
                }
            }
            // TODO: Temp try/catch to investigate strange exception
            catch (System.IO.IOException ex)
            {
                ex.Data["Query"] = query;
                logger.LogError(ex);
            }
        }
    }
}
