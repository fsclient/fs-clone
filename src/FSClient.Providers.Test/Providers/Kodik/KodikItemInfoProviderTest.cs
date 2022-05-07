namespace FSClient.Providers.Test.Kodik
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    using FSClient.Providers.Test.Helpers;
    using FSClient.Shared.Models;

    using NUnit.Framework;

    [TestFixture(Category = TestCategories.ItemInfoProvider)]
    public class KodikItemInfoProviderTest
    {
        [OneTimeSetUp]
        public Task OneTimeSetUp()
        {
            return new KodikSiteProvider(Test.ProviderConfigService).CheckIsAvailableAsync();
        }

        [TestCase("http://kodik.cc/serial/2046/d73994954a08782fddc68295cc8d072a/720p")]
        [TestCase("http://kodik.cc/video/1704/f89980fbd13e8154d3ea137848fd8350/720p")]
        [TestCase("http://kodik.info/go/seria/454701/3fbfea0db147576d7328b3f939954f5a/720p")]
        [Timeout(20000)]
        public void KodikItemInfoProvider_CanOpenFromLinkTest(string link)
        {
            var provider = new KodikItemInfoProvider(new KodikSiteProvider(Test.ProviderConfigService));
            Assert.That(provider.CanOpenFromLink(new Uri(link)), Is.True);
        }

        [TestCase("http://kodik.top/7")]
        [Timeout(20000)]
        public void KodikItemInfoProvider_CanOpenFromLink_InvalidTest(string link)
        {
            var provider = new KodikItemInfoProvider(new KodikSiteProvider(Test.ProviderConfigService));
            Assert.That(provider.CanOpenFromLink(new Uri(link)), Is.False);
        }

        [TestCase("/serial/2046/d73994954a08782fddc68295cc8d072a/720p", SectionModifiers.Serial)]
        [TestCase("/video/1704/f89980fbd13e8154d3ea137848fd8350/720p", SectionModifiers.Film)]
        [Timeout(20000)]
        public async Task KodikItemInfoProvider_OpenFromLinkAsyncTest(string link, SectionModifiers sectionModifier)
        {
            var provider = new KodikItemInfoProvider(new KodikSiteProvider(Test.ProviderConfigService));
            var item = await provider.OpenFromLinkAsync(new Uri(link, UriKind.Relative), CancellationToken.None).ConfigureAwait(false);
            Assert.That(item, Is.Not.Null, "Item is null");
            Assert.That(item!.SiteId, Is.Not.Null, "Item id is null");
            Assert.That(string.IsNullOrWhiteSpace(item.Title), Is.False, "Item title is null or empty");
            Assert.That(item.Section, Is.Not.EqualTo(Section.Any), "Item section is not setted");
            Assert.That(item.Section.Modifier.HasFlag(sectionModifier), Is.True, "Item section is invalid");
        }
    }
}
