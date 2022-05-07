namespace FSClient.Providers.Test.VideoCDN
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    using FSClient.Providers.Test.Helpers;
    using FSClient.Shared.Helpers;
    using FSClient.Shared.Models;

    using NUnit.Framework;

    [TestFixture(Category = TestCategories.ItemInfoProvider)]
    public class VideoCDNItemInfoProviderTest
    {
        [OneTimeSetUp]
        public Task OneTimeSetUp()
        {
            return new VideoCDNSiteProvider(Test.ProviderConfigService).CheckIsAvailableAsync();
        }

        [TestCase("https://14.tvmovies.in/yIE0kdbKybog/tv-series/5")]
        [TestCase("https://14.tvmovies.in/yIE0kdbKybog/movie/32424")]
        [Timeout(20000)]
        public void VideoCDNItemInfoProvider_CanOpenFromLinkTest(string link)
        {
            var provider = new VideoCDNItemInfoProvider(new VideoCDNSiteProvider(Test.ProviderConfigService));
            Assert.That(provider.CanOpenFromLink(new Uri(link)), Is.True);
        }

        [TestCase("https://14.tvmovies.in/yIE0kdbKybog/5")]
        [TestCase("https://14.tvmovies.in/yIE0kdbKybog/movie")]
        [Timeout(20000)]
        public void VideoCDNItemInfoProvider_CanOpenFromLink_InvalidTest(string link)
        {
            var provider = new VideoCDNItemInfoProvider(new VideoCDNSiteProvider(Test.ProviderConfigService));
            Assert.That(provider.CanOpenFromLink(new Uri(link)), Is.False);
        }

        [TestCase("/yIE0kdbKybog/tv-series/5", true)]
        [TestCase("/yIE0kdbKybog/movie/32424", false)]
        [Timeout(20000)]
        public async Task VideoCDNItemInfoProvider_OpenFromLinkAsyncTest(string link, bool isSerial)
        {
            var provider = new VideoCDNItemInfoProvider(new VideoCDNSiteProvider(Test.ProviderConfigService));
            var item = await provider.OpenFromLinkAsync(new Uri(link, UriKind.Relative), CancellationToken.None).ConfigureAwait(false);
            Assert.That(item, Is.Not.Null, "Item is null");
            Assert.That(item!.SiteId, Is.Not.Null, "Item id is null");
            Assert.That(item.Link, Is.Not.Null, "Item link is null");
            Assert.That(item.Site, Is.EqualTo(provider.Site), "Item site is invalid");
            Assert.That(string.IsNullOrWhiteSpace(item.Title), Is.False, "Item title is empty");
            Assert.That(item.Section, Is.Not.EqualTo(Section.Any), "Item section is not setted");
            Assert.That(item.Section.Modifier.HasFlag(SectionModifiers.Serial), Is.EqualTo(isSerial),
                "Item Section.IsSerial is invalid");
            if (isSerial)
            {
                Assert.That(item.Details.Status.CurrentSeason, Is.Not.Null, "Item season is null");
            }

            if (!item.Details.LinkedIds.TryGetValue(Sites.Kinopoisk, out var kpId)
                || !kpId.ToIntOrNull().HasValue)
            {
                Assert.Inconclusive("No one item has kp id");
            }
            if (!item.Details.LinkedIds.TryGetValue(Sites.IMDb, out var imdb)
                || imdb == null)
            {
                Assert.Inconclusive("No one item has imdb id");
            }
        }
    }
}
