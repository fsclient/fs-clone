namespace FSClient.Providers.Test.ExFS
{
    using System;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    using FSClient.Providers.Test.Helpers;
    using FSClient.Shared.Models;

    using NUnit.Framework;

    [TestFixture(Category = TestCategories.ItemInfoProvider)]
    public class ExFSItemInfoProviderTest
    {
        [OneTimeSetUp]
        public Task OneTimeSetUp()
        {
            return new ExFSSiteProvider(Test.ProviderConfigService).CheckIsAvailableAsync();
        }

        [TestCase("http://ex-fs.net/series/82870-britaniya.html")]
        [TestCase("http://ex-fs.net/фыва/82870-ы.html")]
        [Timeout(20000)]
        public void ExFSItemInfoProvider_CanOpenFromLinkTest(string link)
        {
            var provider = new ExFSItemInfoProvider(new ExFSSiteProvider(Test.ProviderConfigService));
            Assert.That(provider.CanOpenFromLink(new Uri(link)), Is.True);
        }

        [TestCase("http://ex-fs.net/series/-britaniya.html")]
        [TestCase("https://ex-fs.ru/фыва/82870-ы.html")]
        [TestCase("https://ex-fs/82870-ы.html")]
        [TestCase("http://filmix.me/series/82870-britaniya.html")]
        [Timeout(20000)]
        public void ExFSItemInfoProvider_CanOpenFromLink_InvalidTest(string link)
        {
            var provider = new ExFSItemInfoProvider(new ExFSSiteProvider(Test.ProviderConfigService));
            Assert.That(provider.CanOpenFromLink(new Uri(link)), Is.False);
        }

        [TestCase("/series/82870-britaniya.html", true)]
        [TestCase("/s/79743-s.html", false)]
        [Timeout(20000)]
        public async Task ExFSItemInfoProvider_OpenFromLinkAsyncTest(string link, bool isSerial)
        {
            var provider = new ExFSItemInfoProvider(new ExFSSiteProvider(Test.ProviderConfigService));
            var item = await provider.OpenFromLinkAsync(new Uri(link, UriKind.Relative), CancellationToken.None).ConfigureAwait(false);
            Assert.That(item, Is.Not.Null, "Item is null");
            Assert.That(item!.SiteId, Is.Not.Null, "Item id is null");
            Assert.That(item.Title, Is.Not.Null, "Item title is null");
            Assert.That(item.Details.Description, Is.Not.Null, "Item desk is null");
            Assert.That(item.Details.Year, Is.Not.Null, "Item year is null");
            Assert.That(item.Poster, Is.Not.Empty, "Item posters is empty");
            Assert.That(item.Poster.All(p => p.Value.IsAbsoluteUri), Is.True, "Item poster image is not absolute uri");
            Assert.That(item.Section, Is.Not.EqualTo(Section.Any), "Item section is not setted");
            Assert.That(item.Section.Modifier.HasFlag(SectionModifiers.Serial), Is.EqualTo(isSerial),
                "Item Section.IsSerial is invalid");
            if (!isSerial)
            {
                Assert.That(item.Details.EpisodesCalendar, Is.Null, "Calendar should be null for non-serials");
            }

            Assume.That(item.Details.LinkedIds.ContainsKey(Sites.Kinopoisk), "KP id is missed");
        }
    }
}
