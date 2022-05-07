namespace FSClient.Providers.Test.Zona
{
    using System;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    using FSClient.Providers.Test.Helpers;
    using FSClient.Shared.Models;

    using NUnit.Framework;

    [TestFixture(Category = TestCategories.ItemInfoProvider)]
    public class ZonaItemInfoProviderTest
    {
        [OneTimeSetUp]
        public Task OneTimeSetUp()
        {
            return new ZonaSiteProvider(Test.ProviderConfigService).CheckIsAvailableAsync();
        }

        [TestCase("http://zona.mobi/tvseries/akademiya-vedmochek")]
        [TestCase("http://zona.mobi/movies/begushchii-po-lezviyu-2049")]
        [TestCase("http://zona.mobi/tvseries/vo-vse-tyazhkie-2008")]
        [Timeout(20000)]
        public void ZonaItemInfoProvider_CanOpenFromLinkTest(string link)
        {
            var provider = new ZonaItemInfoProvider(new ZonaSiteProvider(Test.ProviderConfigService));
            Assert.That(provider.CanOpenFromLink(new Uri(link)), Is.True);
        }

        [TestCase("http://ex-fs.net/series/-britaniya.html")]
        [TestCase("http://ex-fs.net/фыва/82870-ы.html")]
        [TestCase("https://ex-fs/82870-ы.html")]
        [Timeout(20000)]
        public void ZonaItemInfoProvider_CanOpenFromLink_InvalidTest(string link)
        {
            var provider = new ZonaItemInfoProvider(new ZonaSiteProvider(Test.ProviderConfigService));
            Assert.That(provider.CanOpenFromLink(new Uri(link)), Is.False);
        }

        [TestCase("/tvseries/akademiya-vedmochek-2013")]
        [TestCase("/movies/begushchii-po-lezviyu-2049")]
        [TestCase("/tvseries/vo-vse-tyazhkie-2008")]
        [Timeout(20000)]
        public async Task ZonaItemInfoProvider_OpenFromLinkAsyncTest(string link)
        {
            var provider = new ZonaItemInfoProvider(new ZonaSiteProvider(Test.ProviderConfigService));
            var item = await provider.OpenFromLinkAsync(new Uri(link, UriKind.Relative), CancellationToken.None).ConfigureAwait(false);
            Assert.That(item, Is.Not.Null, "Item is null");
            Assert.That(item!.SiteId, Is.Not.Null, "Item id is null");
            Assert.That(item.Title, Is.Not.Null, "Item title is null");
            Assert.That(item.Details.Description, Is.Not.Null, "Item desk is null");
            Assert.That(item.Details.Year, Is.Not.Null, "Item year is null");
            Assert.That(item.Poster, Is.Not.Empty, "Item posters is empty");
            Assert.That(item.Poster.All(p => p.Value.IsAbsoluteUri), Is.True, "Item poster image is not absolute uri");
            Assert.That(item.Section, Is.Not.EqualTo(Section.Any), "Item section is not setted");
        }
    }
}
