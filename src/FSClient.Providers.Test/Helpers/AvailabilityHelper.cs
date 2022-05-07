namespace FSClient.Providers.Test.Helpers
{
    using System;
    using System.Collections.Concurrent;
    using System.Threading;
    using System.Threading.Tasks;

    using FSClient.Shared.Helpers.TimeSpanSemaphore;
    using FSClient.Shared.Models;
    using FSClient.Shared.Providers;

    using Microsoft.Extensions.Logging;

    using Nito.AsyncEx;

    using NUnit.Framework;

    public static class AvailabilityHelper
    {
        private static readonly ConcurrentDictionary<Site, bool> isAvailableCache = new ConcurrentDictionary<Site, bool>();
        private static readonly ConcurrentDictionary<Site, SemaphoreSlim> semaphoresCahce = new ConcurrentDictionary<Site, SemaphoreSlim>();

        public static async Task CheckIsAvailableAsync(this ISiteProvider siteProvider)
        {
            var semaphore = semaphoresCahce.GetOrAdd(siteProvider.Site, _ => new SemaphoreSlim(1));
            using (await semaphore.LockAsync(CancellationToken.None).ConfigureAwait(false))
            {
                if (isAvailableCache.TryGetValue(siteProvider.Site, out var cached))
                {
                    if (!cached)
                    {
                        Assert.Inconclusive($"{siteProvider.Site} is not available");
                    }
                }
                else
                {
                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                    var mirror = await siteProvider.GetMirrorAsync(cts.Token);
                    var isAvailable = await siteProvider.IsAvailableAsync(cts.Token);
                    Test.Logger.LogInformation($"Mirror for {siteProvider.Site} is {mirror}");

                    isAvailableCache.AddOrUpdate(siteProvider.Site, isAvailable, (_, __) => isAvailable);
                    if (!isAvailable)
                    {
                        Assert.Inconclusive($"{siteProvider.Site} is not available");
                    }

                    await Task.Delay(1_000);
                }
            }
        }
    }
}
