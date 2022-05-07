namespace FSClient.Providers.Test.Filmix
{
    using System;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    using FSClient.Providers.Test.Helpers;
    using FSClient.Shared.Models;

    using NUnit.Framework;

    [TestFixture(Category = TestCategories.ItemInfoProvider)]
    public class FilmixItemInfoProviderTest
    {
        [OneTimeSetUp]
        public Task OneTimeSetUp()
        {
            return new FilmixSiteProvider(Test.ProviderConfigService).CheckIsAvailableAsync();
        }

        [TestCase("https://filmix.co/trillery/116576-zalozhnica-2016.html")]
        [TestCase("https://filmix.co/asd/116576-asd.html")]
        [Timeout(20000)]
        public void FilmixItemInfoProvider_CanOpenFromLinkTest(string link)
        {
            var provider = new FilmixItemInfoProvider(new FilmixSiteProvider(Test.ProviderConfigService));
            Assert.That(provider.CanOpenFromLink(new Uri(link)), Is.True);
        }

        [TestCase("http://ex-fs.net/series/-britaniya.html")]
        [TestCase("http://film.cc/trillery/116576-zalozhnica-2016.html")]
        [TestCase("http://filmix.me/series/-britaniya.html")]
        [Timeout(20000)]
        public void FilmixItemInfoProvider_CanOpenFromLink_InvalidTest(string link)
        {
            var provider = new FilmixItemInfoProvider(new FilmixSiteProvider(Test.ProviderConfigService));
            Assert.That(provider.CanOpenFromLink(new Uri(link)), Is.False);
        }

        [TestCase("/filmi/triller/119980-6-inostranec-2017.html", SectionModifiers.Film)]
        [TestCase("/seria/triller/116576-zalozhnica-2016.html", SectionModifiers.Serial)]
        [TestCase("/multserialy/animes/90764-tokiyskiy-gul-tokyo-ghoul-multserial-2014-.html",
            SectionModifiers.Serial | SectionModifiers.Cartoon | SectionModifiers.Anime)]
        [TestCase("/seria/tvs/56186-tachku-na-prokachku-pimp-my-ride-serial-2004-2007.html", SectionModifiers.Serial | SectionModifiers.TVShow)]
        [Timeout(6 * 60 * 1000)]
        public async Task FilmixItemInfoProvider_OpenFromLinkAsyncTest(string link, SectionModifiers sectionModifier)
        {
            var provider = new FilmixItemInfoProvider(new FilmixSiteProvider(Test.ProviderConfigService));
            var item = await provider.OpenFromLinkAsync(new Uri(link, UriKind.Relative), CancellationToken.None).ConfigureAwait(false);
            Assert.That(item, Is.Not.Null, "Item is null");
            Assert.That(item!.SiteId, Is.Not.Null, "Item id is null");
            Assert.That(item.Title, Is.Not.Null, "Item title is null");
            Assert.That(item.Details.Description, Is.Not.Null, "Item desk is null");
            Assert.That(item.Details.Year, Is.Not.Null, "Item year is null");
            Assert.That(item.Poster, Is.Not.Empty, "Item posters is empty");
            Assert.That(item.Poster.All(p => p.Value.IsAbsoluteUri), Is.True, "Item poster image is not absolute uri");
            Assert.That(item.Section, Is.Not.EqualTo(Section.Any), "Item section is not setted");
            Assert.That(item.Section.Modifier.HasFlag(sectionModifier), Is.True, "Item Section is invalid");
            if (sectionModifier.HasFlag(SectionModifiers.Serial))
            {
                Assert.That(item.Details.Status.CurrentEpisode, Is.Not.Null, "Item episode is null");
                Assert.That(item.Details.Status.CurrentSeason, Is.Not.Null, "Item season is null");

                if (!sectionModifier.HasFlag(SectionModifiers.Cartoon))
                {
                    var episodes = await item.Details.EpisodesCalendar!.Take(16).ToListAsync().ConfigureAwait(false);

                    Assert.That(episodes.Count, Is.Not.EqualTo(0), "No episodes in calendar page");
                    CollectionAssert.AllItemsAreUnique(episodes, "Episodes is not unique");
                    CollectionAssert.AllItemsAreNotNull(episodes, "Some episode is null");
                }
            }

            if (!sectionModifier.HasFlag(SectionModifiers.Serial)
                || sectionModifier.HasFlag(SectionModifiers.Cartoon))
            {
                Assert.That(item.Details.EpisodesCalendar, Is.Null, "Calendar should be null for non-serials");
            }
        }
    }
}
