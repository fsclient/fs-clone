namespace FSClient.Providers.Test.HDVB
{
    using System;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    using FSClient.Providers.Test.Helpers;
    using FSClient.Shared.Models;

    using NUnit.Framework;

    [TestFixture(Category = TestCategories.ItemInfoProvider)]
    public class HDVBItemInfoProviderTest
    {
        [OneTimeSetUp]
        public Task OneTimeSetUp()
        {
            return new HDVBSiteProvider(Test.ProviderConfigService).CheckIsAvailableAsync();
        }

        [TestCase("https://videolishd.com/serial/a49a6976dd130f9d6f79b3a6ac99d0030c51ac4b722005bd701d5314d0516bed/iframe")]
        [TestCase("https://farsihd.info/movie/a49a6976dd130f9d6f79b3a6ac99d0030c51ac4b722005bd701d5314d0516bed/iframe")]
        [Timeout(20000)]
        public void HDVBItemInfoProvider_CanOpenFromLinkTest(string link)
        {
            var provider = new HDVBItemInfoProvider(new HDVBSiteProvider(Test.ProviderConfigService));
            Assert.That(provider.CanOpenFromLink(new Uri(link)), Is.True);
        }

        [TestCase("http://videolishd.com/series/asdasdasdasdasdasdasd/iframe")]
        [TestCase("http://videolishd.com/serial/")]
        [Timeout(20000)]
        public void HDVBItemInfoProvider_CanOpenFromLink_InvalidTest(string link)
        {
            var provider = new HDVBItemInfoProvider(new HDVBSiteProvider(Test.ProviderConfigService));
            Assert.That(provider.CanOpenFromLink(new Uri(link)), Is.False);
        }

        [TestCase("/serial/a49a6976dd130f9d6f79b3a6ac99d0030c51ac4b722005bd701d5314d0516bed/iframe", true)]
        [TestCase("/movie/9d429a3661b4cc71b4f8908ecfb5a902/iframe", false)]
        [Timeout(20000)]
        public async Task HDVBItemInfoProvider_OpenFromLinkAsyncTest(string link, bool isSerial)
        {
            var provider = new HDVBItemInfoProvider(new HDVBSiteProvider(Test.ProviderConfigService));
            var item = await provider.OpenFromLinkAsync(new Uri(link, UriKind.Relative), CancellationToken.None).ConfigureAwait(false);
            Assert.That(item, Is.Not.Null, "Item is null");
            Assert.That(item!.SiteId, Is.Not.Null, "Item id is null");
            Assert.That(item.Title, Is.Not.Null, "Item title is null");
            Assert.That(item.Details.Quality, Is.Not.Null, "Item title is null");
            Assert.That(item.Details.Year, Is.Not.Null, "Item year is null");
            Assert.That(item.Section, Is.Not.EqualTo(Section.Any), "Item section is not setted");
            Assert.That(item.Section.Modifier.HasFlag(SectionModifiers.Serial), Is.EqualTo(isSerial),
                "Item Section.IsSerial is invalid");
            if (isSerial)
            {
                var hdvbItem = item as HDVBItemInfo;
                Assert.That(hdvbItem, Is.Not.EqualTo(null), "Invalid item type");
                Assert.That(item.Details.Status.CurrentSeason, Is.Not.Null, "Item season is null");

                var episodes = hdvbItem!.EpisodesPerSeasons.Select(t => t.Value.Select(ep => (s: t.Key, ep))).SelectMany(t => t).ToList();
                Assert.That(episodes.Count, Is.Not.EqualTo(0), "No episodes in calendar");
                CollectionAssert.AllItemsAreUnique(episodes, "Episodes is not unique");
                CollectionAssert.AllItemsAreNotNull(episodes, "Some episode is null");
            }
        }
    }
}
