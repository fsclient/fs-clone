namespace FSClient.Providers.Test.SeasonVar
{
    using System;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    using FSClient.Providers.Test.Helpers;
    using FSClient.Shared.Models;

    using NUnit.Framework;

    [TestFixture(Category = TestCategories.ItemInfoProvider)]
    public class SeasonVarItemInfoProviderTest
    {
        [OneTimeSetUp]
        public Task OneTimeSetUp()
        {
            return new SeasonVarSiteProvider(Test.ProviderConfigService).CheckIsAvailableAsync();
        }

        [TestCase("http://seasonvar.ru/serial-14022-Slepo3_pyatno--02-sezon.html")]
        [TestCase("http://seasonhit-api.herokuapp.com/get/serial-13895-91_den_.html")]
        [Timeout(20000)]
        public void SeasonVarItemInfoProvider_CanOpenFromLinkTest(string link)
        {
            var provider = new SeasonVarItemInfoProvider(new SeasonVarSiteProvider(Test.ProviderConfigService));
            Assert.That(provider.CanOpenFromLink(new Uri(link)), Is.True);
        }

        [TestCase("http://ex-fs.net/series/-britaniya.html")]
        [TestCase("https://ex-fs/82870-ы.html")]
        [TestCase("http://SeasonVar.me/series/82870-britaniya.html")]
        [Timeout(20000)]
        public void SeasonVarItemInfoProvider_CanOpenFromLink_InvalidTest(string link)
        {
            var provider = new SeasonVarItemInfoProvider(new SeasonVarSiteProvider(Test.ProviderConfigService));
            Assert.That(provider.CanOpenFromLink(new Uri(link)), Is.False);
        }

        [TestCase("serial-14022-Slepo3_pyatno--02-sezon.html")]
        [TestCase("serial-13895-91_den_.html")]
        [Timeout(20000)]
        public async Task SeasonVarItemInfoProvider_OpenFromLinkAsyncTest(string link)
        {
            var provider = new SeasonVarItemInfoProvider(new SeasonVarSiteProvider(Test.ProviderConfigService));
            var item = await provider.OpenFromLinkAsync(new Uri(link, UriKind.Relative), CancellationToken.None)
                .ConfigureAwait(false);
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
