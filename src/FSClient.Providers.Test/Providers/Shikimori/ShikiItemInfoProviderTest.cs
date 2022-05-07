namespace FSClient.Providers.Test.Shikimori
{
    using System;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    using FSClient.Providers.Test.Helpers;
    using FSClient.Shared.Models;

    using NUnit.Framework;

    [TestFixture(Category = TestCategories.ItemInfoProvider)]
    public class ShikiItemInfoProviderTest
    {
        [OneTimeSetUp]
        public Task OneTimeSetUp()
        {
            return new ShikiSiteProvider(Test.ProviderConfigService).CheckIsAvailableAsync();
        }

        [TestCase("https://shikimori.org/animes/35849-darling-in-the-franxx")]
        [TestCase("https://shikimori.one/animes/32281-kimi-no-na-wa")]
        [TestCase("https://shikimori.org/animes/205-samurai-champloo/video_online/1")]
        [Timeout(20000)]
        public void ShikiItemInfoProvider_CanOpenFromLinkTest(string link)
        {
            var provider = new ShikiItemInfoProvider(new ShikiSiteProvider(Test.ProviderConfigService));
            Assert.That(provider.CanOpenFromLink(new Uri(link)), Is.True);
        }

        [TestCase("http://ex-fs.net/фыва/82870-ы.html")]
        [Timeout(20000)]
        public void ShikiItemInfoProvider_CanOpenFromLink_InvalidTest(string link)
        {
            var provider = new ShikiItemInfoProvider(new ShikiSiteProvider(Test.ProviderConfigService));
            Assert.That(provider.CanOpenFromLink(new Uri(link)), Is.False);
        }

        [TestCase("/animes/35849-darling-in-the-franxx", SectionModifiers.Serial)]
        [TestCase("/animes/32281-kimi-no-na-wa", SectionModifiers.Film)]
        [TestCase("/animes/y31636-dagashi-kashi/video_online/1", SectionModifiers.Serial)]
        [Timeout(40000)]
        public async Task ShikiItemInfoProvider_OpenFromLinkAsyncTest(string link, SectionModifiers sectionModifier)
        {
            var provider = new ShikiItemInfoProvider(new ShikiSiteProvider(Test.ProviderConfigService));
            var item = await provider.OpenFromLinkAsync(new Uri(link, UriKind.Relative), CancellationToken.None).ConfigureAwait(false);
            await provider.PreloadItemAsync(item!, Shared.Providers.PreloadItemStrategy.Full, CancellationToken.None).ConfigureAwait(false);

            Assert.That(item, Is.Not.Null, "Item is null");
            Assert.That(item!.SiteId, Is.Not.Null, "Item id is null");
            Assert.That(item.Title, Is.Not.Null, "Item title is null");
            Assert.That(item.Details.Description, Is.Not.Null, "Item desk is null");
            Assert.That(item.Details.Year, Is.Not.Null, "Item year is null");
            Assert.That(item.Poster, Is.Not.Empty, "Item posters is empty");
            Assert.That(item.Poster.All(p => p.Value.IsAbsoluteUri), Is.True, "Item poster image is not absolute uri");
            Assert.That(item.Section, Is.Not.EqualTo(Section.Any), "Item section is not setted");
            Assert.That(item.Section.Modifier.HasFlag(sectionModifier | SectionModifiers.Cartoon), Is.True,
                "Item section is invalid");
            Assert.That(item.Details.Status.Type, Is.Not.EqualTo(StatusType.Unknown), "Item status type is unknown");
            if (sectionModifier.HasFlag(SectionModifiers.Serial))
            {
                if (item.Details.Status.CurrentEpisode == null)
                {
                    Assert.Inconclusive("Item current episode is null");
                }

                Assert.That(item.Details.Status.TotalEpisodes, Is.Not.Null, "Item total episodes is null");

                var episodes = await item.Details.EpisodesCalendar!.Take(16).ToListAsync().ConfigureAwait(false);
                Assert.That(episodes.Count, Is.Not.EqualTo(0), "No episodes in calendar");
                CollectionAssert.AllItemsAreUnique(episodes, "Episodes is not unique");
                CollectionAssert.AllItemsAreNotNull(episodes, "Some episode is null");
            }
            else
            {
                Assert.That(item.Details.EpisodesCalendar, Is.Null, "Calendar should be null for non-serials");
            }

            Assume.That(item.Details.LinkedIds.ContainsKey(Sites.IMDb)
                || item.Details.LinkedIds.ContainsKey(Sites.Kinopoisk)
                || item.Details.LinkedIds.ContainsKey(Sites.Twitter), "Any supported ID is missed");
        }
    }
}
