namespace FSClient.Providers.Test.KinoUkr
{
    using System.Linq;
    using System.Threading.Tasks;

    using FSClient.Providers.Test.Helpers;
    using FSClient.Shared.Models;
    using FSClient.Shared.Providers;

    using NUnit.Framework;

    [TestFixture(Category = TestCategories.SearchProvider)]
    public class KinoUkrSearchProviderTest
    {
        [OneTimeSetUp]
        public Task OneTimeSetUp()
        {
            return new KinoUkrSiteProvider(Test.ProviderConfigService).CheckIsAvailableAsync();
        }

        [TestCase("Вікінги", SectionModifiers.Serial)]
        [TestCase("Воно", SectionModifiers.Film)]
        [Timeout(10000)]
        public async Task KinoUkrSearchProvider_GetFullResultAsync(string query, SectionModifiers sectionModifier)
        {
            var provider = new KinoUkrSearchProvider(new KinoUkrSiteProvider(Test.ProviderConfigService));

            var section = provider.Sections.First(s => s.Modifier.HasFlag(sectionModifier));

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
                Assert.That(string.IsNullOrWhiteSpace(item.Title), Is.False, "Item title is empty");
                Assert.That(item.Section.Modifier.HasFlag(section.Modifier), Is.True, "Item section is invalid");
                Assert.That(item.Poster, Is.Not.Empty, "Item posters is empty");
                Assert.That(item.Poster.All(p => p.Value.IsAbsoluteUri), Is.True, "Item poster image is not absolute uri");
                Assert.That(string.IsNullOrWhiteSpace(item.Details.Description), Is.False, "Item title is empty");
            }
        }

        [TestCase("Друзі")]
        [TestCase("Game of thrones")]
        [TestCase("Вікінги")]
        [Timeout(10000)]
        public async Task KinoUkrSearchProvider_GetShortResultAsync(string query)
        {
            var provider = new KinoUkrSearchProvider(new KinoUkrSiteProvider(Test.ProviderConfigService));

            var resultItems = await provider.GetShortResult(query, provider.Sections.First())
                .Take(10)
                .ToListAsync();

            Assert.That(resultItems.Count > 0, Is.True, "Zero items count on first page");
            CollectionAssert.AllItemsAreNotNull(resultItems, "Some item is null");
            CollectionAssert.AllItemsAreUnique(resultItems, "Items is not unique");

            foreach (var item in resultItems)
            {
                Assert.That(item.Site, Is.EqualTo(provider.Site), "Item site is invalid");
                Assert.That(item.Link, Is.Not.Null, "Item link is null");
                Assert.That(string.IsNullOrWhiteSpace(item.Title), Is.False, "Item title is empty");
                Assert.That(string.IsNullOrWhiteSpace(item.Details.Description), Is.False, "Item description is empty");
            }
        }
    }
}
