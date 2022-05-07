namespace FSClient.Providers.Test.UASerials
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    using FSClient.Providers.Test.Helpers;
    using FSClient.Shared.Models;

    using NUnit.Framework;

    [TestFixture(Category = TestCategories.ItemInfoProvider)]
    public class UASerialsItemInfoProviderTest
    {
        [OneTimeSetUp]
        public Task OneTimeSetUp()
        {
            return new UASerialsSiteProvider(Test.ProviderConfigService).CheckIsAvailableAsync();
        }

        [TestCase("https://uaserials.pro/408-tayemnici-graviti-folz-sezon-1.html")]
        [Timeout(20000)]
        public void UASerialsItemInfoProvider_CanOpenFromLinkTest(string link)
        {
            var provider = new UASerialsItemInfoProvider(new UASerialsSiteProvider(Test.ProviderConfigService));
            Assert.That(provider.CanOpenFromLink(new Uri(link)), Is.True);
        }

        [TestCase("http://ex-fs.net/series/-britaniya.html")]
        [TestCase("https://ex-fs/82870-ы.html")]
        [TestCase("http://uaserials.me/series/82870-britaniya.html")]
        [Timeout(20000)]
        public void UASerialsItemInfoProvider_CanOpenFromLink_InvalidTest(string link)
        {
            var provider = new UASerialsItemInfoProvider(new UASerialsSiteProvider(Test.ProviderConfigService));
            Assert.That(provider.CanOpenFromLink(new Uri(link)), Is.False);
        }

        [TestCase("/408-tayemnici-graviti-folz-sezon-1.html")]
        [Timeout(20000)]
        public async Task UASerialsItemInfoProvider_OpenFromLinkAsyncTest(string link)
        {
            var provider = new UASerialsItemInfoProvider(new UASerialsSiteProvider(Test.ProviderConfigService));
            var item = await provider.OpenFromLinkAsync(new Uri(link, UriKind.Relative), CancellationToken.None)
                .ConfigureAwait(false);
            Assert.That(item, Is.TypeOf<UASerialsItemInfo>(), "Item has wrong type");
            Assert.That(item, Is.Not.Null, "Item is null");
            Assert.That(item!.SiteId, Is.Not.Null, "Item id is null");
            Assert.That(item.Link, Is.Not.Null, "Item link is null");
            Assert.That(item.Details.Year, Is.Not.Null, "Item year is null");
            Assert.That(string.IsNullOrWhiteSpace(item.Title), Is.False, "Item title is empty");
            Assert.That(string.IsNullOrWhiteSpace(item.Details.TitleOrigin), Is.False, "Item title is empty");
            Assert.That(item.Section.Modifier.HasFlag(SectionModifiers.Serial), Is.True, "Item Section.IsSerial is invalid");
            Assert.That(((UASerialsItemInfo)item).DataTag, Is.Not.Null, "Item PlayerData is null");
            Assert.That(((UASerialsItemInfo)item).Translation, Is.Not.Null, "Item translator is null");
        }
    }
}
