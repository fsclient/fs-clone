namespace FSClient.Providers.Test.Shikimori
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;

    using FSClient.Providers.Test.Helpers;
    using FSClient.Providers.Test.Stubs;
    using FSClient.Shared.Models;
    using FSClient.Shared.Providers;

    using NUnit.Framework;

    [TestFixture(Category = TestCategories.SearchProvider)]
    public class ShikiSearchProviderTest
    {
        [OneTimeSetUp]
        public Task OneTimeSetUp()
        {
            return new ShikiSiteProvider(Test.ProviderConfigService).CheckIsAvailableAsync();
        }

        [TestCase("Гин", true)]
        [TestCase("Kimi no Na wa", false)]
        [Timeout(20000)]
        public async Task ShikimoriSearchProvider_GetFullResultAsync(string query, bool isTv)
        {
            var provider = new ShikiSearchProvider(new ShikiSiteProvider(Test.ProviderConfigService),
                new CacheServiceStub((_, __) => Task.FromResult((object)Array.Empty<TitledTag>())));

            var section = provider.Sections.First(s => s != Section.Any && (isTv ? s.Value == "tv" : s.Value == "movie"));

            var page = await provider.GetSearchPageParamsAsync(section, default);
            var filter = new SearchPageFilter(page!, query);

            var resultItems = await provider.GetFullResult(filter).Take(30).ToListAsync();

            Assert.That(resultItems, Is.Not.Empty, "Zero items count on page");

            CollectionAssert.AllItemsAreNotNull(resultItems, "Some item is null");
            CollectionAssert.AllItemsAreUnique(resultItems, "Items is not unique");

            foreach (var item in resultItems)
            {
                Assert.That(item.Site, Is.EqualTo(provider.Site), "Item site is invalid");
                Assert.That(item.Link, Is.Not.Null, "Item link is null");
                Assert.That(item.Details.Status.Type, Is.Not.EqualTo(StatusType.Unknown), "Item status is unknown");
                Assert.That(item.Poster, Is.Not.Empty, "Item posters is empty");
                Assert.That(item.Poster.All(p => p.Value.IsAbsoluteUri), Is.True, "Item poster image is not absolute uri");
                Assert.That(string.IsNullOrWhiteSpace(item.Title), Is.False, "Item title is empty");
            }

            if (resultItems.All(item => item.Details.Year is null))
            {
                Assert.Fail("All item years are null");
            }
            if (resultItems.All(item => item.Section.Modifier.HasFlag(SectionModifiers.Serial) != isTv))
            {
                Assert.Fail("All item sections don't match expected");
            }
        }
    }
}
