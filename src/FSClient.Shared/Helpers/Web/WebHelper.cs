namespace FSClient.Shared.Helpers.Web
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Net.Http.Json;
    using System.Text;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;

    using AngleSharp.Dom;
    using AngleSharp.Html.Dom;
    using AngleSharp.Html.Parser;

    using FSClient.Shared.Models;
    using FSClient.Shared.Services;

    public static class WebHelper
    {
        public const string DefaultUserAgent =
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/85.0.4183.121 Safari/537.36";

        public const string MobileUserAgent =
            "Mozilla/5.0 (iPhone; CPU iPhone OS 11_0 like Mac OS X) AppleWebKit/604.1.38 (KHTML, like Gecko) Version/11.0 Mobile/15A372 Safari/604.1";

        private static readonly Lazy<Encoding?> Win1251Encoding;

        private static readonly HtmlParser HtmlParser;

        static WebHelper()
        {
            Win1251Encoding = new Lazy<Encoding?>(() =>
            {
                try
                {
                    Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
                    return Encoding.GetEncoding(1251);
                }
                catch (Exception ex)
                {
                    Logger.Instance.LogWarning(ex);
                    return null;
                }
            });

            HtmlParser = new HtmlParser();
        }

        public static HttpRequestBuilder RequestBuilder(this HttpClient httpClient, HttpMethod method, Uri link)
        {
            return new HttpRequestBuilder(httpClient, method, link);
        }

        public static HttpRequestBuilder GetBuilder(this HttpClient httpClient, Uri link)
        {
            return new HttpRequestBuilder(httpClient, HttpMethod.Get, link);
        }

        public static HttpRequestBuilder PostBuilder(this HttpClient httpClient, Uri link)
        {
            return new HttpRequestBuilder(httpClient, HttpMethod.Post, link);
        }

        public static HttpRequestBuilder HeadBuilder(this HttpClient httpClient, Uri link)
        {
            return new HttpRequestBuilder(httpClient, HttpMethod.Head, link);
        }

        public static async Task<Stream?> AsStream(this Task<HttpResponseMessage?> responseTask, bool throwOnError = false)
        {
            var response = await responseTask.ConfigureAwait(false);

            return await response.AsStream(throwOnError).ConfigureAwait(false);
        }

        public static Task<Stream?> AsStream(this HttpResponseMessage? response, bool throwOnError = false)
        {
            if (response == null)
            {
                return Task.FromResult<Stream?>(null);
            }

            try
            {
                return response.Content.ReadAsStreamAsync();
            }
            catch (Exception ex)
            {
                Logger.Instance.LogWarning(ex);

                if (throwOnError)
                {
                    throw;
                }
            }
            return Task.FromResult<Stream?>(null);
        }

        public static async Task<IHtmlDocument?> AsHtml(this Task<HttpResponseMessage?> responseTask, CancellationToken cancellationToken, Encoding? encoding = null, bool throwOnError = false)
        {
            var response = await responseTask.ConfigureAwait(false);

            return await response.AsHtml(cancellationToken, encoding, throwOnError).ConfigureAwait(false);
        }

        public static async Task<IHtmlDocument?> AsHtml(this HttpResponseMessage? response, CancellationToken cancellationToken, Encoding? encoding = null, bool throwOnError = false)
        {
            try
            {
                if (response == null)
                {
                    return null;
                }

                encoding ??= GetEncodingFromResponseOrNull(response);

                if (encoding != null
                    && encoding != Encoding.UTF8)
                {
                    var htmlString = await response.ReadAsStringAsync(encoding).ConfigureAwait(false);

                    return htmlString != null
                        ? await HtmlParser.ParseDocumentAsync(htmlString, cancellationToken).ConfigureAwait(false)
                        : null;
                }
                var stream = await response.AsStream(throwOnError).ConfigureAwait(false);
                return stream != null
                    ? await HtmlParser.ParseDocumentAsync(stream).ConfigureAwait(false)
                    : null;
            }
            catch (DomException)
            {
                if (throwOnError)
                {
                    throw;
                }

                return null;
            }
            catch (Exception ex)
            {
                Logger.Instance.LogWarning(ex);

                if (throwOnError)
                {
                    throw;
                }

                return null;
            }
        }

        public static IHtmlDocument? ParseHtml(string html, bool throwOnError = false)
        {
            try
            {
                return HtmlParser.ParseDocument(html);
            }
            catch (Exception ex)
            {
                Logger.Instance.LogWarning(ex);

                if (throwOnError)
                {
                    throw;
                }
            }
            return null;
        }

        public static async Task<TResult?> AsJson<TResult>(this Task<HttpResponseMessage?> responseTask, CancellationToken cancellationToken, Encoding? encoding = null, bool throwOnError = false)
            where TResult : class
        {
            var response = await responseTask.ConfigureAwait(false);

            return await response.AsJson<TResult>(cancellationToken, encoding, throwOnError).ConfigureAwait(false);
        }

        public static async Task<TResult?> AsJson<TResult>(this HttpResponseMessage? response, CancellationToken cancellationToken, Encoding ? encoding = null, bool throwOnError = false)
            where TResult : class
        {
            if (response == null)
            {
                return null;
            }

            try
            {
                var serializeOptions = new JsonSerializerOptions
                {
                    Converters =
                    {
                        new SiteJsonConverter()
                    },
                    PropertyNameCaseInsensitive = true
                };

                if (encoding == null)
                {
                    return await response.Content.ReadFromJsonAsync<TResult>(serializeOptions, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    var jsonString = await response.AsText(encoding, throwOnError).ConfigureAwait(false);
                    if (jsonString == null)
                    {
                        return default;
                    }

                    return JsonSerializer.Deserialize<TResult>(jsonString, serializeOptions);
                }
            }
            catch (JsonException)
            {
                if (throwOnError)
                {
                    throw;
                }

                return null;
            }
            catch (Exception ex)
            {
                Logger.Instance.LogWarning(ex);

                if (throwOnError)
                {
                    throw;
                }

                return null;
            }
        }

        public static async Task<JsonElement?> AsJson(this Task<HttpResponseMessage?> responseTask, CancellationToken cancellationToken, Encoding? encoding = null, bool throwOnError = false)
        {
            var response = await responseTask.ConfigureAwait(false);

            return await response.AsJson(cancellationToken, encoding, throwOnError).ConfigureAwait(false);
        }

        public static async Task<JsonElement?> AsJson(this HttpResponseMessage? response, CancellationToken cancellationToken, Encoding? encoding = null, bool throwOnError = false)
        {
            if (response == null)
            {
                return null;
            }

            try
            {
                var serializeOptions = new JsonSerializerOptions
                {
                    Converters =
                    {
                        new SiteJsonConverter()
                    }
                };

                if (encoding == null)
                {
                    return await response.Content.ReadFromJsonAsync<JsonElement>(serializeOptions, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    var jsonString = await response.AsText(encoding, throwOnError).ConfigureAwait(false);
                    if (jsonString == null)
                    {
                        return default;
                    }

                    return JsonSerializer.Deserialize<JsonElement>(jsonString, serializeOptions);
                }
            }
            catch (JsonException)
            {
                if (throwOnError)
                {
                    throw;
                }

                return null;
            }
            catch (Exception ex)
            {
                Logger.Instance.LogWarning(ex);

                if (throwOnError)
                {
                    throw;
                }

                return null;
            }
        }

        public static async Task<string?> AsText(this Task<HttpResponseMessage?> responseTask, Encoding? encoding = null, bool throwOnError = false)
        {
            var response = await responseTask.ConfigureAwait(false);

            return await response.AsText(encoding, throwOnError).ConfigureAwait(false);
        }

        public static async Task<string?> AsText(this HttpResponseMessage? response, Encoding? encoding = null, bool throwOnError = false)
        {
            if (response == null)
            {
                return null;
            }

            try
            {
                encoding ??= GetEncodingFromResponseOrNull(response);

                if (encoding != null
                    && encoding != Encoding.UTF8)
                {
                    return await response.ReadAsStringAsync(encoding).ConfigureAwait(false);
                }

                return await response
                    .Content
                    .ReadAsStringAsync()
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Logger.Instance.LogWarning(ex);

                if (throwOnError)
                {
                    throw;
                }

                return null;
            }
        }

        public static async Task<string?> ReadAsStringAsync(this HttpResponseMessage? response, Encoding encoding)
        {
            if (response == null)
            {
                return null;
            }

            var bytes = await response
                .Content
                .ReadAsByteArrayAsync()
                .ConfigureAwait(false);

            return encoding.GetString(bytes, 0, bytes.Length);
        }

        public static HttpRequestMessage CreateCopy(this HttpRequestMessage original)
        {
            var copy = new HttpRequestMessage(original.Method, original.RequestUri)
            {
                Content = original.Content,
                Version = original.Version
            };
            foreach (var prop in original.Properties)
            {
                copy.Properties.Add(prop);
            }

            foreach (var header in original.Headers)
            {
                copy.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }

            return copy;
        }

        public static Task<long> GetContentSizeAsync(this HttpClient client, Uri link, CancellationToken cancellationToken)
        {
            return client.GetContentSizeAsync(link, new Dictionary<string, string>(), cancellationToken);
        }

        public static async Task<long> GetContentSizeAsync(this HttpClient client, Uri link, IDictionary<string, string> headers, CancellationToken cancellationToken)
        {
            var head = await client
                .GetBuilder(link)
                .WithHeader("Range", "bytes=0-1")
                .WithHeaders(headers)
                .SendAsync(HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);
            if (head == null)
            {
                return 0;
            }

            if (head.StatusCode == HttpStatusCode.NotAcceptable
                || head.StatusCode == HttpStatusCode.RequestedRangeNotSatisfiable
                || head.StatusCode == HttpStatusCode.NotImplemented)
            {
                head = await client
                    .GetBuilder(link)
                    .WithHeaders(headers)
                    .SendAsync(HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                    .ConfigureAwait(false);
            }

            return head?.GetContentSize() ?? 0;
        }

        public static long GetContentSize(this HttpResponseMessage response)
        {
            if (response.Content == null
                || !response.IsSuccessStatusCode)
            {
                return 0;
            }

            return Math.Max(
                response.Content.Headers.ContentRange?.Length ?? 0,
                response.Content.Headers.ContentLength ?? 0);
        }

        public static long GetContentSize(IReadOnlyDictionary<string, string> headers)
        {
            if (headers.TryGetValue("Content-Length", out var contentLengthStr)
                && long.TryParse(contentLengthStr, out var contentLength))
            {
                return contentLength;
            }

            if (headers.TryGetValue("Content-Range", out var contentRangeStr)
                && long.TryParse(contentRangeStr.Split('-').Last(), out var rangeLength))
            {
                return rangeLength;
            }

            return 0;
        }

        public static async Task<bool> HasConnectionAsync(this HttpClient httpClient, CancellationToken cancellationToken)
        {
            var response = await httpClient
                .HeadBuilder(new Uri("https://1.1.1.1/"))
                .SendAsync(cancellationToken)
                .ConfigureAwait(false);
            return response?.IsSuccessStatusCode == true;
        }

        public static Task<bool> HasConnectionAsync(this IWebProxy webProxy, CancellationToken cancellationToken)
        {
            var httpClient = new HttpClient(new HttpClientHandler
            {
                Proxy = webProxy,
                UseProxy = true
            });
            return httpClient.HasConnectionAsync(cancellationToken);
        }

        private static Encoding? GetEncodingFromResponseOrNull(HttpResponseMessage response)
        {
            var charset = response.Content.Headers.ContentType?.CharSet?.ToLowerInvariant();
            if ((charset?.Contains("1251") ?? false)
                && Win1251Encoding.Value is Encoding win1251)
            {
                return win1251;
            }
            // Invalid content type
            // Should be utf-8
            if (charset == "utf8")
            {
                return Encoding.UTF8;
            }
            return null;
        }
    }
}
