namespace FSClient.Providers.Test.Anitube
{
    using System.Linq;
    using System.Threading.Tasks;

    using FSClient.Providers.Test.Helpers;

    using NUnit.Framework;

    [TestFixture(Category = TestCategories.SearchProvider)]
    public class AnitubeSarchProviderTest
    {
        [OneTimeSetUp]
        public Task OneTimeSetUp()
        {
            return new AnitubeSiteProvider(Test.ProviderConfigService).CheckIsAvailableAsync();
        }

        [TestCase("Гінтама")]
        [TestCase("Barefoot Gen")]
        [TestCase("Okami to Koshinryo")]
        [Timeout(10000)]
        public async Task AnitubeSearchProvider_GetShortResultAsync(string query)
        {
            var provider = new AnitubeSearchProvider(new AnitubeSiteProvider(Test.ProviderConfigService));

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
                Assert.That(item.Details.Year, Is.Not.Null, "Item link is null");
                Assert.That(item.Section.Modifier.HasFlag(Shared.Models.SectionModifiers.Anime), Is.True, "Invalid section");
                Assert.That(item.Poster, Is.Not.Empty, "Item posters is empty");
                Assert.That(item.Poster.All(p => p.Value.IsAbsoluteUri), Is.True, "Item poster image is not absolute uri");
            }
        }
    }
}
