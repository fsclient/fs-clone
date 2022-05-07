namespace FSClient.Providers.Test.FindAnime
{
    using System;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    using FSClient.Providers.Test.Helpers;
    using FSClient.Shared.Models;

    using NUnit.Framework;

    [TestFixture(Category = TestCategories.ItemInfoProvider)]
    public class FindAnimeItemInfoProviderTest
    {
        [OneTimeSetUp]
        public Task OneTimeSetUp()
        {
            return new FindAnimeSiteProvider(Test.ProviderConfigService).CheckIsAvailableAsync();
        }

        [TestCase("http://findanime.org/volchica_i_prianosti__tv_1_")]
        [TestCase("http://findanime.org/van_pis__tv_/series891")]
        [Timeout(20000)]
        public void FindAnimeItemInfoProvider_CanOpenFromLinkTest(string link)
        {
            var provider = new FindAnimeItemInfoProvider(new FindAnimeSiteProvider(Test.ProviderConfigService));
            Assert.That(provider.CanOpenFromLink(new Uri(link)), Is.True);
        }

        [TestCase("http://findanims.org/van_pis__tv_/series891")]
        [Timeout(20000)]
        public void FindAnimeItemInfoProvider_CanOpenFromLink_InvalidTest(string link)
        {
            var provider = new FindAnimeItemInfoProvider(new FindAnimeSiteProvider(Test.ProviderConfigService));
            Assert.That(provider.CanOpenFromLink(new Uri(link)), Is.False);
        }

        [TestCase("jizn_ne_v_odinochku", true)]
        [TestCase("barefoot_gen", false)]
        [Timeout(20000)]
        public async Task FindAnimeItemInfoProvider_OpenFromLinkAsyncTest(string link, bool isSeial)
        {
            var provider = new FindAnimeItemInfoProvider(new FindAnimeSiteProvider(Test.ProviderConfigService));
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
            Assert.That(item.Section.Modifier.HasFlag(SectionModifiers.Cartoon), Is.True, "Item section is invalid");
        }
    }
}
