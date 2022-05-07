namespace FSClient.Shared.Helpers
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;

    public static class QueryStringHelper
    {
        public static IEnumerable<KeyValuePair<string, string>> ParseQuery(string? query)
        {
            if (query == null)
            {
                return Enumerable.Empty<KeyValuePair<string, string>>();
            }

            var startIndex = query.IndexOf("?", StringComparison.Ordinal) + 1;

            return query[startIndex..]
                .Split(new[] { '&' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(prop =>
                {
                    var eqIndex = prop.IndexOf("=", StringComparison.Ordinal);
                    if (eqIndex < 0)
                    {
                        return (name: prop, value: string.Empty);
                    }

                    return (
                        name: Uri.UnescapeDataString(prop[..eqIndex]),
                        value: Uri.UnescapeDataString(prop[(eqIndex + 1)..])
                    );
                })
                .Where(p => !string.IsNullOrEmpty(p.name))
                .Select(p => new KeyValuePair<string, string>(p.name, p.value));
        }

        public static string CreateQueryString(IEnumerable<KeyValuePair<string, string?>> dictionary)
        {
            if (dictionary == null)
            {
                throw new ArgumentNullException(nameof(dictionary));
            }

            var builder = new StringBuilder();

            foreach (var prop in dictionary)
            {
                if (builder.Length > 0)
                {
                    builder.Append('&');
                }

                builder.Append(Uri.EscapeDataString(prop.Key));
                if (!string.IsNullOrEmpty(prop.Value))
                {
                    builder.Append('=');
                    builder.Append(Uri.EscapeDataString(prop.Value));
                }
            }

            return builder.ToString();
        }

        public static string CreateArgumentPair(string name, string? value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return Uri.EscapeDataString(name);
            }
            else
            {
                return $"{Uri.EscapeDataString(name)}={Uri.EscapeDataString(value)}";
            }
        }
    }
}
