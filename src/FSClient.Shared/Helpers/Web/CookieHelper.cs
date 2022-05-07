namespace FSClient.Shared.Helpers.Web
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Net.Http.Headers;

    public static class CookieHelper
    {
        private static readonly char[] reserved2Name = { ' ', '\t', '\r', '\n', '=', ';', ',' };
        private static readonly char[] reserved2Value = { ';', ',' };

        public static string ToCookieString(this IEnumerable<Cookie> cookies)
        {
            return string.Join(";", cookies
                .Select(cookie => string
                .IsNullOrEmpty(cookie.Value)
                    ? cookie.Name
                    : $"{cookie.Name}={cookie.Value}"));
        }

        public static IEnumerable<Cookie> FromCookieString(string? cookies)
        {
            return (cookies?.Split(';')
                .Select(cookie =>
                {
                    var parts = cookie.Split(new[] { '=', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    var name = parts.FirstOrDefault();
                    var value = parts.Skip(1).FirstOrDefault() ?? "";
                    if (string.IsNullOrWhiteSpace(name)
                        || value == "deleted")
                    {
                        return null;
                    }

                    return new Cookie(name, value);
                })
                .Where(cookie => cookie != null)
                ?? Enumerable.Empty<Cookie>())!;
        }

        public static void DeleteCookies(this HttpClientHandler handler, Uri domain, params string[] cookKeys)
        {
            if (domain == null)
            {
                throw new ArgumentNullException(nameof(domain));
            }

            foreach (var cookie in handler.GetCookies(domain, cookKeys))
            {
                cookie.Value = "deleted";
                cookie.Expired = true;
            }
        }

        public static Cookie? SetCookie(this HttpClientHandler handler, Uri domain, string key, string value)
        {
            if (domain == null)
            {
                throw new ArgumentNullException(nameof(domain));
            }

            var oldCook = handler.GetCookies(domain, key).FirstOrDefault();

            if (oldCook != null)
            {
                oldCook.Value = value;
                return oldCook;
            }

            if (!string.IsNullOrWhiteSpace(key))
            {
                var cook = new Cookie(key, value);
                handler.SetCookies(domain, cook);
                return cook;
            }
            return null;
        }

        public static void SetCookies(this HttpClientHandler handler, Uri domain, params Cookie[] cookies)
        {
            if (domain == null)
            {
                throw new ArgumentNullException(nameof(domain));
            }

            foreach (var cookie in cookies)
            {
                if (string.IsNullOrEmpty(cookie.Path))
                {
                    cookie.Path = "/";
                }
                cookie.Name = cookie.Name.RemoveCharacters(reserved2Name);
                cookie.Value = cookie.Value?.RemoveCharacters(reserved2Value);

                if (cookie.Domain != null)
                {
                    if (handler.CookieContainer.GetCookies(domain)[cookie.Name] is Cookie oldCook)
                    {
                        if (oldCook.Value != cookie.Value)
                        {
                            oldCook.Value = cookie.Value;
                            oldCook.Expires = cookie.Expires;
                        }
                    }
                    else
                    {
                        handler.CookieContainer.Add(domain, new Cookie(cookie.Name, cookie.Value)
                        {
                            Path = cookie.Path
                        });
                    }
                }
                else
                {
                    handler.CookieContainer.Add(domain, cookie);
                }
            }
        }

        public static IEnumerable<Cookie> GetCookies(this HttpClientHandler handler, Uri domain)
        {
            if (domain == null)
            {
                throw new ArgumentNullException(nameof(domain));
            }

            return GetCookiesIterator();
            IEnumerable<Cookie> GetCookiesIterator()
            {
                if (handler?.CookieContainer?.GetCookies(domain) is not CookieCollection cookieCollection)
                {
                    yield break;
                }

                foreach (var fc in cookieCollection.OfType<Cookie>())
                {
                    yield return fc;
                }
            }
        }

        public static IEnumerable<Cookie> GetCookies(this HttpClientHandler handler, Uri domain, params string[] cookKeys)
        {
            if (domain == null)
            {
                throw new ArgumentNullException(nameof(domain));
            }

            return handler.GetCookies(domain).Where(c => Array.IndexOf(cookKeys, c.Name) >= 0);
        }

        public static IEnumerable<Cookie> GetCookies(this HttpResponseMessage response)
        {
            return response.Headers.GetCookies();
        }

        public static IEnumerable<Cookie> GetCookies(this HttpRequestMessage request)
        {
            return request.Headers.GetCookies();
        }

        public static IEnumerable<Cookie> GetCookies(this HttpHeaders headers)
        {
            var cookies = Enumerable.Empty<string>();

            if (headers.TryGetValues("Cookie", out var cooks))
            {
                cookies = cookies.Concat(cooks);
            }

            if (headers.TryGetValues("set-cookie", out var setCooks))
            {
                cookies = cookies.Union(setCooks
                    .Select(c => c
                        .Split(';').First()));
            }

            foreach (var cookieStr in cookies)
            {
                foreach (var cookie in FromCookieString(cookieStr))
                {
                    yield return cookie;
                }
            }
        }
    }
}
