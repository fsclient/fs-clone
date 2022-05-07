namespace FSClient.Providers.Test.Zona
{
    using System.Linq;
    using System.Threading.Tasks;

    using FSClient.Providers.Test.Helpers;
    using FSClient.Shared.Helpers;
    using FSClient.Shared.Models;
    using FSClient.Shared.Providers;

    using NUnit.Framework;

    [TestFixture(Category = TestCategories.SearchProvider)]
    public class ZonaSearchProviderTest
    {
        [OneTimeSetUp]
        public Task OneTimeSetUp()
        {
            return new ZonaSiteProvider(Test.ProviderConfigService).CheckIsAvailableAsync();
        }

        [TestCase("Игра")]
        [TestCase("Game")]
        [Timeout(10000)]
        public async Task ZonaSearchProvider_GetFullResultAsync(string query)
        {
            var provider = new ZonaSearchProvider(new ZonaSiteProvider(Test.ProviderConfigService));

            var section = provider.Sections.First();

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
                Assert.That(item.Details.Year, Is.Not.Null, "Item year is null");
                Assert.That(item.Poster, Is.Not.Empty, "Item posters is empty");
                Assert.That(item.Poster.All(p => p.Value.IsAbsoluteUri), Is.True, "Item poster image is not absolute uri");
                Assert.That(string.IsNullOrWhiteSpace(item.Title), Is.False, "Item title is empty");
                Assert.That(item.Section, Is.Not.EqualTo(Section.Any), "Item section is not match");
                Assert.That(item.Details.LinkedIds.TryGetValue(Sites.Kinopoisk, out var id) && id != null && id.ToIntOrNull().HasValue, Is.True, "Item has not kp id");
            }
            if (resultItems.All(i => string.IsNullOrWhiteSpace(i.Details.TitleOrigin)))
            {
                Assert.Inconclusive("Every item's origin title is null or white space");
            }
        }
    }
}
