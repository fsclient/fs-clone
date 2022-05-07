namespace FSClient.Providers.Test.ExFS
{
    using System.Linq;
    using System.Threading.Tasks;

    using FSClient.Providers.Test.Helpers;
    using FSClient.Shared.Models;
    using FSClient.Shared.Providers;

    using NUnit.Framework;

    [TestFixture(Category = TestCategories.SearchProvider)]
    public class ExFSSearchProviderTest
    {
        [OneTimeSetUp]
        public Task OneTimeSetUp()
        {
            return new ExFSSiteProvider(Test.ProviderConfigService).CheckIsAvailableAsync();
        }

        [TestCase("Кинг")]
        [TestCase("Игра")]
        [Timeout(60000)]
        public async Task ExFSSearchProvider_GetFullResultAsync(string query)
        {
            var provider = new ExFSSearchProvider(new ExFSSiteProvider(Test.ProviderConfigService));

            var section = provider.Sections.First(s => s != Section.Any);

            var page = await provider.GetSearchPageParamsAsync(section, default);
            var filter = new SearchPageFilter(page!, query);

            var resultItems = await provider.GetFullResult(filter).Take(8).ToListAsync();

            Assert.That(resultItems, Is.Not.Empty, "Zero items count on page");

            CollectionAssert.AllItemsAreNotNull(resultItems, "Some item is null");
            CollectionAssert.AllItemsAreUnique(resultItems, "Items is not unique");

            foreach (var item in resultItems)
            {
                Assert.That(item.Site, Is.EqualTo(provider.Site), "Item site is invalid");
                Assert.That(item.Link, Is.Not.Null, "Item link is null");
                Assert.That(item.Poster, Is.Not.Empty, "Item posters is empty");
                Assert.That(item.Poster.All(p => p.Value.IsAbsoluteUri), Is.True, "Item poster image is not absolute uri");
                Assert.That(string.IsNullOrWhiteSpace(item.Title), Is.False, "Item title is empty");
                Assert.That(item.Section, Is.EqualTo(section), "Item section is not match");
            }
        }

        [TestCase("Викинги")]
        [Timeout(60000)]
        public async Task ExFSSearchProvider_GetShortResultAsync(string query)
        {
            var provider = new ExFSSearchProvider(new ExFSSiteProvider(Test.ProviderConfigService));
            var resultItems = await provider.GetShortResult(query, Section.Any).Take(10).ToListAsync();

            Assert.That(resultItems.Count > 0, Is.True, "Zero items count");

            CollectionAssert.AllItemsAreNotNull(resultItems, "Some item is null");
            CollectionAssert.AllItemsAreUnique(resultItems, "Items is not unique");

            foreach (var item in resultItems)
            {
                Assert.That(item.Site, Is.EqualTo(provider.Site), "Item site is invalid");
                Assert.That(item.Link, Is.Not.Null, "Item link is null");
                Assert.That(item.Details.Year, Is.Not.Null, "Item year is not setted");
                Assert.That(item.Poster, Is.Not.Empty, "Item posters is empty");
                Assert.That(item.Poster.All(p => p.Value.IsAbsoluteUri), Is.True, "Item poster image is not absolute uri");
                Assert.That(string.IsNullOrWhiteSpace(item.Title), Is.False, "Item title is empty");
                Assert.That(item.Section, Is.Not.EqualTo(Section.Any), "Item section is undefined");
            }
        }
    }
}
