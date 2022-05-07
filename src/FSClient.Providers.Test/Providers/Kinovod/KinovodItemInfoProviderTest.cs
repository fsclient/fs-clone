namespace FSClient.Providers.Test.Kinovod
{
    using System;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    using FSClient.Providers.Test.Helpers;
    using FSClient.Shared.Models;

    using NUnit.Framework;

    [TestFixture(Category = TestCategories.ItemInfoProvider)]
    public class KinovodItemInfoProviderTest
    {
        [OneTimeSetUp]
        public Task OneTimeSetUp()
        {
            return new KinovodSiteProvider(Test.ProviderConfigService).CheckIsAvailableAsync();
        }

        [TestCase("https://kinovod.net/film/124697-moj-sozdatel")]
        [TestCase("https://kinovod.net/serial/127164-112263")]
        [Timeout(20000)]
        public void KinovodItemInfoProvider_CanOpenFromLinkTest(string link)
        {
            var provider = new KinovodItemInfoProvider(new KinovodSiteProvider(Test.ProviderConfigService));
            Assert.That(provider.CanOpenFromLink(new Uri(link)), Is.True);
        }

        [TestCase("http://ex-fs.net/series/-britaniya.html")]
        [Timeout(20000)]
        public void KinovodItemInfoProvider_CanOpenFromLink_InvalidTest(string link)
        {
            var provider = new KinovodItemInfoProvider(new KinovodSiteProvider(Test.ProviderConfigService));
            Assert.That(provider.CanOpenFromLink(new Uri(link)), Is.False);
        }

        [TestCase("/film/124697-moj-sozdatel")]
        [TestCase("/serial/127164-112263")]
        [Timeout(20000)]
        public async Task KinovodItemInfoProvider_OpenFromLinkAsyncTest(string link)
        {
            var provider = new KinovodItemInfoProvider(new KinovodSiteProvider(Test.ProviderConfigService));
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
