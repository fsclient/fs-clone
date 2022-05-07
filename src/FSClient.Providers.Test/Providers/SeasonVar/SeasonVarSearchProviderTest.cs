namespace FSClient.Providers.Test.SeasonVar
{
    using System.Linq;
    using System.Threading.Tasks;

    using FSClient.Providers.Test.Helpers;
    using FSClient.Shared.Models;
    using FSClient.Shared.Providers;

    using NUnit.Framework;

    [TestFixture(Category = TestCategories.SearchProvider)]
    public class SeasonVarSearchProviderTest
    {
        [OneTimeSetUp]
        public Task OneTimeSetUp()
        {
            return new SeasonVarSiteProvider(Test.ProviderConfigService).CheckIsAvailableAsync();
        }

        [TestCase("Игра")]
        [Timeout(20000)]
        public async Task SeasonVarSearchProvider_GetFullResultAsync(string query)
        {
            var provider = new SeasonVarSearchProvider(new SeasonVarSiteProvider(Test.ProviderConfigService));

            var section = provider.Sections[0];

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
                Assert.That(item.Section.Modifier.HasFlag(SectionModifiers.Serial), Is.True, "Item section is invalid");

                if (!resultItems.First().Link!.Host.Contains("seasonhit-api", System.StringComparison.Ordinal))
                {
                    Assert.That(item.Poster, Is.Not.Empty, "Item posters is empty");
                    Assert.That(item.Poster.All(p => p.Value.IsAbsoluteUri), Is.True, "Item poster image is not absolute uri");
                    Assert.That(string.IsNullOrWhiteSpace(item.Details.Description), Is.False, "Item title is empty");
                }
            }
            if (resultItems.All(i => string.IsNullOrWhiteSpace(i.Title)))
            {
                Assert.Inconclusive("Every item's origin title is null or white space");
            }
        }

        [TestCase("Друзья")]
        [TestCase("Game of thrones")]
        [TestCase("Викинги")]
        [Timeout(20000)]
        public async Task SeasonVarSearchProvider_GetShortResultAsync(string query)
        {
            var provider = new SeasonVarSearchProvider(new SeasonVarSiteProvider(Test.ProviderConfigService));

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
                Assert.That(item.Section.Modifier.HasFlag(SectionModifiers.Serial), Is.True, "Item Section.IsSerial is invalid");
                Assert.That(item.Details.Status.CurrentSeason, Is.Not.Null, "Item season is null");
            }
        }
    }
}
