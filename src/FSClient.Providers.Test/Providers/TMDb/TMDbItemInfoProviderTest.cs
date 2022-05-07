namespace FSClient.Providers.Test.TMDb
{
    using System;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    using FSClient.Providers.Test.Helpers;
    using FSClient.Providers.Test.Stubs;
    using FSClient.Shared.Models;
    using FSClient.Shared.Providers;

    using NUnit.Framework;

    [TestFixture(Category = TestCategories.ItemInfoProvider)]
    public class TMDbItemInfoProviderTest
    {
        [OneTimeSetUp]
        public Task OneTimeSetUp()
        {
            return new TMDbSiteProvider(Test.ProviderConfigService, new AppLanguageServiceStub()).CheckIsAvailableAsync();
        }

        [TestCase("https://www.themoviedb.org/tv/1418-the-big-bang-theory")]
        [TestCase("https://www.themoviedb.org/tv/1418-the-big-bang-theory/season/11")]
        [TestCase("https://www.themoviedb.org/movie/321612-beauty-and-the-beast")]
        [Timeout(20000)]
        public void TMDbItemInfoProvider_CanOpenFromLinkTest(string link)
        {
            var provider = new TMDbItemInfoProvider(new TMDbSiteProvider(Test.ProviderConfigService, new AppLanguageServiceStub()),
                new CacheServiceStub((_, __) => Task.FromResult((object)Array.Empty<TitledTag>())));
            Assert.That(provider.CanOpenFromLink(new Uri(link)), Is.True);
        }

        [TestCase("http://ex-fs.net/series/-britaniya.html")]
        [TestCase("https://ex-fs/82870-ы.html")]
        [TestCase("http://SeasonVar.me/series/82870-britaniya.html")]
        [Timeout(20000)]
        public void TMDbItemInfoProvider_CanOpenFromLink_InvalidTest(string link)
        {
            var provider = new TMDbItemInfoProvider(new TMDbSiteProvider(Test.ProviderConfigService, new AppLanguageServiceStub()),
                new CacheServiceStub((_, __) => Task.FromResult((object)Array.Empty<TitledTag>())));
            Assert.That(provider.CanOpenFromLink(new Uri(link)), Is.False);
        }

        [TestCase("/tv/1418-the-big-bang-theory", SectionModifiers.Serial)]
        [TestCase("/tv/456-the-simpsons/season/29", SectionModifiers.Serial | SectionModifiers.Cartoon)]
        [TestCase("/movie/321612-beauty-and-the-beast", SectionModifiers.Film)]
        [Timeout(20000)]
        public async Task TMDbItemInfoProvider_OpenFromLinkAsyncTest(string link, SectionModifiers sectionModifier)
        {
            var provider = new TMDbItemInfoProvider(new TMDbSiteProvider(Test.ProviderConfigService, new AppLanguageServiceStub()),
                new CacheServiceStub((_, __) => Task.FromResult((object)Array.Empty<TitledTag>())));
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
                Assert.That(item.Details.Status.Type, Is.Not.EqualTo(StatusType.Unknown), "Item status type is unknown");
                Assert.That(item.Details.Status.CurrentEpisode, Is.Not.Null, "Item episode is null");
                Assert.That(item.Details.Status.CurrentSeason, Is.Not.Null, "Item season is null");
            }

            Assume.That(item.Details.LinkedIds.ContainsKey(Sites.IMDb), "IMDb id is missed");
        }

        [TestCase("/tv/1418-the-big-bang-theory")]
        [TestCase("/tv/456-the-simpsons/season/29")]
        [Timeout(20000)]
        public async Task TMDbItemInfoProvider_PreloadItemAsync_CalendarTest(string link)
        {
            var provider = new TMDbItemInfoProvider(new TMDbSiteProvider(Test.ProviderConfigService, new AppLanguageServiceStub()),
                new CacheServiceStub((_, __) => Task.FromResult((object)Array.Empty<TitledTag>())));
            var item = await provider.OpenFromLinkAsync(new Uri(link, UriKind.Relative), CancellationToken.None).ConfigureAwait(false);
            var success = await provider.PreloadItemAsync(item!, PreloadItemStrategy.Full, CancellationToken.None).ConfigureAwait(false);

            Assert.That(success, Is.True, "Item preload failed");

            var episodes = await item!.Details.EpisodesCalendar!.Take(16).ToListAsync().ConfigureAwait(false);

            Assert.That(episodes.Count, Is.Not.EqualTo(0), "No episodes in calendar page");
            CollectionAssert.AllItemsAreUnique(episodes, "Episodes is not unique");
            CollectionAssert.AllItemsAreNotNull(episodes, "Some episode is null");
        }

        [TestCase("/movie/321612-beauty-and-the-beast")]
        [Timeout(20000)]
        public async Task TMDbItemInfoProvider_PreloadItemAsync_Calendar_Is_Null_Test(string link)
        {
            var provider = new TMDbItemInfoProvider(new TMDbSiteProvider(Test.ProviderConfigService, new AppLanguageServiceStub()),
                new CacheServiceStub((_, __) => Task.FromResult((object)Array.Empty<TitledTag>())));
            var item = await provider.OpenFromLinkAsync(new Uri(link, UriKind.Relative), CancellationToken.None).ConfigureAwait(false);
            var success = await provider.PreloadItemAsync(item!, PreloadItemStrategy.Full, CancellationToken.None).ConfigureAwait(false);

            Assert.That(success, Is.True, "Item preload failed");

            Assert.That(item!.Details.EpisodesCalendar, Is.Null, "Calendar should be null for non-serials");
        }
    }
}
