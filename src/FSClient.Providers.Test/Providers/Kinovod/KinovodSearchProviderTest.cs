namespace FSClient.Providers.Test.Kinovod
{
    using System.Linq;
    using System.Threading.Tasks;

    using FSClient.Providers.Test.Helpers;
    using FSClient.Shared.Models;

    using NUnit.Framework;

    [TestFixture(Category = TestCategories.SearchProvider)]
    public class KinovodSearchProviderTest
    {
        [OneTimeSetUp]
        public Task OneTimeSetUp()
        {
            return new KinovodSiteProvider(Test.ProviderConfigService).CheckIsAvailableAsync();
        }

        [TestCase("Игра престолов")]
        [TestCase("Game of thrones")]
        [Timeout(10000)]
        public async Task KinovodSearchProvider_GetShortResultAsync(string query)
        {
            var provider = new KinovodSearchProvider(new KinovodSiteProvider(Test.ProviderConfigService));

            var resultItems = await provider.GetShortResult(query, default).Take(30).ToListAsync();
            Assert.That(resultItems, Is.Not.Empty, "Zero items count on page");

            CollectionAssert.AllItemsAreNotNull(resultItems, "Some item is null");
            CollectionAssert.AllItemsAreUnique(resultItems, "Items is not unique");

            foreach (var item in resultItems)
            {
                Assert.That(item.Site, Is.EqualTo(provider.Site), "Item site is invalid");
                Assert.That(item.Link, Is.Not.Null, "Item link is null");
                Assert.That(item.Details.Year, Is.Not.Null, "Item year is null");
                Assert.That(string.IsNullOrWhiteSpace(item.Title), Is.False, "Item title is empty");
                Assert.That(item.Section, Is.Not.EqualTo(Section.Any), "Item section is not match");
            }
        }
    }
}
