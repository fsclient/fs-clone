namespace FSClient.Providers.Test.FindAnime
{
    using System.Linq;
    using System.Threading.Tasks;

    using FSClient.Providers.Test.Helpers;

    using NUnit.Framework;

    [TestFixture(Category = TestCategories.SearchProvider)]
    public class FindAnimeSarchProviderTest
    {
        [OneTimeSetUp]
        public Task OneTimeSetUp()
        {
            return new FindAnimeSiteProvider(Test.ProviderConfigService).CheckIsAvailableAsync();
        }

        [TestCase("Гинтама")]
        [TestCase("Barefoot Gen")]
        [TestCase("Okami to Koshinryo")]
        [Timeout(10000)]
        public async Task FindAnimeSearchProvider_GetShortResultAsync(string query)
        {
            var provider = new FindAnimeSearchProvider(new FindAnimeSiteProvider(Test.ProviderConfigService));

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
                Assert.That(item.Section.Modifier.HasFlag(Shared.Models.SectionModifiers.Anime), Is.True, "Invalid section");
                Assert.That(string.IsNullOrWhiteSpace(item.Title), Is.False, "Item title is empty");
                Assert.That(item.Poster, Is.Not.Empty, "Item posters is empty");
                Assert.That(item.Poster.All(p => p.Value.IsAbsoluteUri), Is.True, "Item poster image is not absolute uri");
            }
            if (resultItems.All(i => string.IsNullOrWhiteSpace(i.Details.TitleOrigin)))
            {
                Assert.Fail("Item title origin is empty");
            }
        }
    }
}
