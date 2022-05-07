namespace FSClient.Providers.Test.Rezka
{
    using System;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    using FSClient.Providers.Test.Helpers;
    using FSClient.Shared.Models;

    using NUnit.Framework;

    [TestFixture(Category = TestCategories.ItemInfoProvider)]
    public class RezkaItemInfoProviderTest
    {
        [OneTimeSetUp]
        public Task OneTimeSetUp()
        {
            return new RezkaSiteProvider(Test.ProviderConfigService).CheckIsAvailableAsync();
        }

        [TestCase("https://rezka.ag/films/drama/31756-abbatstvo-daunton-2019.html")]
        [TestCase("https://rezka.ag/series/adventures/32545-avatar-korolya-2019.html")]
        [Timeout(20000)]
        public void RezkaItemInfoProvider_CanOpenFromLinkTest(string link)
        {
            var provider = new RezkaItemInfoProvider(new RezkaSiteProvider(Test.ProviderConfigService));
            Assert.That(provider.CanOpenFromLink(new Uri(link)), Is.True);
        }

        [TestCase("http://ex-fs.net/series/-britaniya.html")]
        [TestCase("https://nereska.ag/films/drama/31756-abbatstvo-daunton-2019.html")]
        [Timeout(20000)]
        public void RezkaItemInfoProvider_CanOpenFromLink_InvalidTest(string link)
        {
            var provider = new RezkaItemInfoProvider(new RezkaSiteProvider(Test.ProviderConfigService));
            Assert.That(provider.CanOpenFromLink(new Uri(link)), Is.False);
        }

        [TestCase("/films/drama/31756-abbatstvo-daunton-2019.html", SectionModifiers.Film)]
        [TestCase("/series/adventures/32545-avatar-korolya-2019.html", SectionModifiers.Serial)]
        [TestCase("/animation/fantasy/32325-mastera-mecha-onlayn-alisizaciya-voyna-v-podmire-tv-4-2019.html",
            SectionModifiers.Serial | SectionModifiers.Anime)]
        [Timeout(20000)]
        public async Task RezkaItemInfoProvider_OpenFromLinkAsyncTest(string link, SectionModifiers sectionModifier)
        {
            var provider = new RezkaItemInfoProvider(new RezkaSiteProvider(Test.ProviderConfigService));
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

            if (!sectionModifier.HasFlag(SectionModifiers.Serial))
            {
                Assert.That(item.Details.EpisodesCalendar, Is.Null, "Calendar should be null for non-serials");
            }

            Assume.That(item.Details.LinkedIds.ContainsKey(Sites.IMDb), "IMDb id is missed");
            Assume.That(item.Details.LinkedIds.ContainsKey(Sites.Kinopoisk), "KP id is missed");
        }
    }
}
