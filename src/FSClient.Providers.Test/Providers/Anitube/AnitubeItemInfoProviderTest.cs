namespace FSClient.Providers.Test.Anitube
{
    using System;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    using FSClient.Providers.Test.Helpers;
    using FSClient.Shared.Models;

    using NUnit.Framework;

    [TestFixture(Category = TestCategories.ItemInfoProvider)]
    public class AnitubeItemInfoProviderTest
    {
        [OneTimeSetUp]
        public Task OneTimeSetUp()
        {
            return new AnitubeSiteProvider(Test.ProviderConfigService).CheckIsAvailableAsync();
        }

        [TestCase("https://anitube.in.ua/3613-uzaki-chan-wa-asobitai.html")]
        [Timeout(20000)]
        public void AnitubeItemInfoProvider_CanOpenFromLinkTest(string link)
        {
            var provider = new AnitubeItemInfoProvider(new AnitubeSiteProvider(Test.ProviderConfigService));
            Assert.That(provider.CanOpenFromLink(new Uri(link)), Is.True);
        }

        [TestCase("http://atube.org/van_pis__tv_/series891")]
        [Timeout(20000)]
        public void AnitubeItemInfoProvider_CanOpenFromLink_InvalidTest(string link)
        {
            var provider = new AnitubeItemInfoProvider(new AnitubeSiteProvider(Test.ProviderConfigService));
            Assert.That(provider.CanOpenFromLink(new Uri(link)), Is.False);
        }

        [TestCase("3613-uzaki-chan-wa-asobitai.html", true)]
        [TestCase("2590-tenki-no-ko.html", false)]
        [Timeout(20000)]
        public async Task AnitubeItemInfoProvider_OpenFromLinkAsyncTest(string link, bool isSeial)
        {
            var provider = new AnitubeItemInfoProvider(new AnitubeSiteProvider(Test.ProviderConfigService));
            var item = await provider.OpenFromLinkAsync(new Uri(link, UriKind.Relative), CancellationToken.None)
                .ConfigureAwait(false);

            Assert.That(item, Is.Not.Null, "Item is null");
            Assert.That(string.IsNullOrEmpty(item!.SiteId), Is.False, "Item id is null or empty");
            Assert.That(item.Link, Is.Not.Null, "Item link is null");
            Assert.That(item.Title, Is.Not.Null, "Item title is null");
            Assert.That(item.Details.Description, Is.Not.Null, "Item desk is null");
            Assert.That(item.Details.TitleOrigin, Is.Not.Null, "Item title origin is null");
            Assert.That(item.Details.Year, Is.Not.Null, "Item year is null");
            Assert.That(item.Poster, Is.Not.Empty, "Item posters is empty");
            Assert.That(item.Poster.All(p => p.Value.IsAbsoluteUri), Is.True, "Item poster image is not absolute uri");
            Assert.That(item.Section.Modifier.HasFlag(SectionModifiers.Serial), Is.EqualTo(isSeial), "Item section is invalid");
            Assert.That(item.Section.Modifier.HasFlag(SectionModifiers.Anime), Is.True, "Item section is invalid");
        }
    }
}
