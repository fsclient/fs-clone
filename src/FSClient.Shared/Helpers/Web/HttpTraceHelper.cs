namespace FSClient.Shared.Helpers.Web
{
    using System;
    using System.Collections.Generic;
    using System.Net.Http;
    using System.Net.Http.Headers;

    using FSClient.Shared.Services;

    using Microsoft.Extensions.Logging;

    public static class HttpTraceHelper
    {
        public static void TraceHttp(
            this ILogger logger,
            HttpRequestMessage request,
            HttpResponseMessage? response,
            HttpContent? contentCopy,
            bool includeCookies)
        {
            Exception? warningException = null;
            try
            {
                var builder = new List<string>(100);
                try
                {
                    builder.Add($"{request.Method} {request.RequestUri?.PathAndQuery} HTTP/{request.Version}");
                    builder.Add($"> Host {request.RequestUri?.Scheme}://{request.RequestUri?.Host}:{request.RequestUri?.Port}");
                    builder.AppendHeaders(request.Headers, "> ", includeCookies);
                    if (request.Content?.Headers != null)
                    {
                        builder.AppendHeaders(request.Content.Headers, "> ", includeCookies);
                    }

                    if (contentCopy is HttpContent content)
                    {
                        builder.Add(string.Empty);
                        builder.Add($"| {content.ReadAsStringAsync().GetAwaiter().GetResult()}");
                    }

                    if (response != null)
                    {
                        builder.Add(string.Empty);
                        builder.Add($"HTTP {response.Version} {(int)response.StatusCode} {response.StatusCode}");
                        builder.AppendHeaders(response.Headers, "< ", includeCookies);
                        if (response.Content?.Headers != null)
                        {
                            builder.AppendHeaders(response.Content.Headers, "< ", includeCookies);
                        }
                    }
                }
                catch (Exception ex)
                {
                    warningException = ex;
                }
                logger.Log(LogLevel.Trace, default, builder, null!, (_, __) => "TraceHttp");
            }
            catch (Exception ex)
            {
                logger.LogError(ex);
            }
            if (warningException != null)
            {
                logger.LogWarning(warningException);
            }
        }

        private static void AppendHeaders(this List<string> builder, HttpHeaders headers, string direction, bool includeCookies)
        {
            foreach (var header in headers)
            {
                if (!includeCookies
                    && header.Key.IndexOf("cookie", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    continue;
                }

                builder.Add($"{direction}{header.Key}: {string.Join("; ", header.Value)}");
            }
        }
    }
}
