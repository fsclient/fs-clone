namespace FSClient.Providers.Test.UASerials
{
    using System.Linq;
    using System.Threading.Tasks;

    using FSClient.Providers.Test.Helpers;
    using FSClient.Shared.Models;

    using NUnit.Framework;

    [TestFixture(Category = TestCategories.SearchProvider)]
    public class UASerialsSearchProviderTest
    {
        [OneTimeSetUp]
        public Task OneTimeSetUp()
        {
            return new UASerialsSiteProvider(Test.ProviderConfigService).CheckIsAvailableAsync();
        }

        [TestCase("Game of thrones", false)]
        [TestCase("Gravity Falls", true)]
        [Timeout(20000)]
        public async Task UASerialsSearchProvider_GetShortResultAsync(string query, bool cartoon)
        {
            var provider = new UASerialsSearchProvider(new UASerialsSiteProvider(Test.ProviderConfigService), null!);

            var section = provider.Sections.First(s => s.Modifier.HasFlag(SectionModifiers.Cartoon) == cartoon);
            var resultItems = await provider.GetShortResult(query, section)
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
                Assert.That(string.IsNullOrWhiteSpace(item.Details.TitleOrigin), Is.False, "Item title is empty");
                Assert.That(item.Section.Modifier.HasFlag(section.Modifier), Is.True, "Item Section is invalid");
            }
        }
    }
}
