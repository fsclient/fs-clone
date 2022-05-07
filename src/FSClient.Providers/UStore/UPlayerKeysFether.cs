namespace FSClient.Providers
{
    using FSClient.Shared.Helpers;
    using FSClient.Shared.Helpers.Web;
    using FSClient.Shared.Providers;

    using Nito.AsyncEx;

    using System;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Threading.Tasks;

    public static class UPlayerKeysFether
    {
        private static readonly SemaphoreSlim semaphoreSlim = new SemaphoreSlim(1);
        private static (string relativeLink, string key1, string key2)? cache;

        public static async Task<(string relativeLink, string key1, string key2)?> FetchKeysAsync(IHttpSiteProvider siteProvider, Uri playerLink, CancellationToken cancellationToken)
        {
            if (cache.HasValue)
            {
                return cache;
            }

            using var _ = await semaphoreSlim.LockAsync(cancellationToken).ConfigureAwait(false);
            
            if (cache.HasValue)
            {
                return cache;
            }

            var text = await siteProvider
                .HttpClient
                .GetBuilder(playerLink)
                .WithTimeSpanSemaphore(siteProvider.RequestSemaphore)
                .SendAsync(cancellationToken)
                .AsText()
                .ConfigureAwait(false);

            if (string.IsNullOrEmpty(text))
            {
                return null;
            }

            var values = Regex.Match(text, @"\[(?<valuesArray>.+?)\]").Groups["valuesArray"].Value.Split(',');
            if (values.Length == 0)
            {
                return null;
            }

            var contentUrlIndex = Regex.Match(text, @"contentURL:.+?\[(?<index>.+?)\]").Groups["index"].Value.ToIntOrNull();

            var securityKeyIndexGroups = Regex.Match(text, @"securityKey:\[.+?\[(?<index1>.+?)\].+?\[(?<index2>.+?)\]\]").Groups;
            var securityKeyIndex1 = securityKeyIndexGroups["index1"].Value.ToIntOrNull();
            var securityKeyIndex2 = securityKeyIndexGroups["index2"].Value.ToIntOrNull();

            if (contentUrlIndex.HasValue && contentUrlIndex <= values.Length
                && securityKeyIndex1.HasValue && securityKeyIndex1 <= values.Length
                && securityKeyIndex2.HasValue && securityKeyIndex2 <= values.Length)
            {
                return cache = (
                    Regex.Unescape(values[contentUrlIndex.Value]).Trim('"'),
                    Regex.Unescape(values[securityKeyIndex1.Value]).Trim('"'),
                    Regex.Unescape(values[securityKeyIndex2.Value]).Trim('"')
                );
            }

            return null;
        }
    }
}
