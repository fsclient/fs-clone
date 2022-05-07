namespace FSClient.Shared.Helpers
{
    using System;

    public static class UriHelper
    {
        public static bool IsHttpUri(this Uri uri)
        {
            var scheme = uri.Scheme;
            return ((string.Compare("http", scheme, StringComparison.OrdinalIgnoreCase) == 0) ||
                (string.Compare("https", scheme, StringComparison.OrdinalIgnoreCase) == 0));
        }

        public static bool IsAbsoluteHttpUri(this Uri uri)
        {
            return uri.IsAbsoluteUri
                && uri.IsHttpUri();
        }

        public static string? GetOrigin(this Uri uri)
        {
            if (uri?.IsAbsoluteUri != true)
            {
                return null;
            }

            return $"{uri.Scheme}://{uri.Host}";
        }

        public static string GetPath(this Uri uri)
        {
            return uri.IsAbsoluteUri ? uri.PathAndQuery : uri.OriginalString;
        }

        public static string GetRootDomain(this Uri uri)
        {
            // Note, that solution does not cover all cases and can cause wrong output in some of them.
            // Consider using top level domains database to get valid result.
            var host = uri.Host;
            var hostLength = host.Length;
            var n1DotPosition = host.LastIndexOf(".", StringComparison.Ordinal);
            if (n1DotPosition <= 0)
            {
                return host;
            }

            var n2DotPosition = host.LastIndexOf(".", n1DotPosition - 1, StringComparison.Ordinal);

            // handle international country code TLDs 
            // www.amazon.co.uk => amazon.co.uk
            if (n2DotPosition > 0
                && (hostLength - n1DotPosition - 1) < 3
                && (n1DotPosition - n2DotPosition - 1) <= 3)
            {
                var n3DotPosition = host.LastIndexOf(".", n2DotPosition - 1, StringComparison.Ordinal);
                return host.Substring(n3DotPosition + 1);
            }
            else
            {
                return host.Substring(n2DotPosition + 1);
            }
        }
    }
}
