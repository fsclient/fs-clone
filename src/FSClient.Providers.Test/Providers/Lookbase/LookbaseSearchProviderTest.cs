namespace FSClient.Providers.Test.Lookbase
{
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    using FSClient.Providers.Test.Helpers;
    using FSClient.Shared.Helpers;
    using FSClient.Shared.Models;

    using NUnit.Framework;

    [TestFixture(Category = TestCategories.SearchProvider)]
    public class LookbaseSearchProviderTest
    {
        [OneTimeSetUp]
        public Task OneTimeSetUp()
        {
            return new LookbaseSiteProvider(Test.ProviderConfigService).CheckIsAvailableAsync();
        }

        [TestCase("1174226")]
        [Timeout(10000)]
        public async Task LookbaseSearchProvider_FindSimilarByKpIdAsync(string kpId)
        {
            var sourceItem = new ItemInfo(Site.Any, "kp" + kpId)
            {
                Details =
                {
                    LinkedIds =
                    {
                        [Sites.Kinopoisk] = kpId
                    }
                }
            };

            var provider = new LookbaseSearchProvider(new LookbaseSiteProvider(Test.ProviderConfigService), null);

            var resultItems = (await provider
                .FindSimilarAsync(sourceItem, CancellationToken.None)
                .ConfigureAwait(false))
                .ToList();

            Assert.That(resultItems.Count > 0, Is.True, "Zero items count");
            CollectionAssert.AllItemsAreNotNull(resultItems, "Some item is null");
            CollectionAssert.AllItemsAreUnique(resultItems, "Items is not unique");

            foreach (var item in resultItems)
            {
                Assert.That(item.Site, Is.EqualTo(provider.Site), "Item site is invalid");
                Assert.That(item.Link, Is.Not.Null, "Item link is null");
                Assert.That(string.IsNullOrWhiteSpace(item.Title), Is.False, "Item title is empty");
            }
            if (!resultItems.Any(i =>
                    i.Details.LinkedIds.TryGetValue(Sites.Kinopoisk, out var id)
                    && id?.ToIntOrNull().HasValue == true))
            {
                Assert.Inconclusive("No one item has kp id");
            }
        }

        [TestCase("tt8688634")]
        [Timeout(10000)]
        public async Task LookbaseSearchProvider_FindSimilarByIMDbIdAsync(string imdbId)
        {
            var sourceItem = new ItemInfo(Site.Any, "imdb" + imdbId)
            {
                Details =
                {
                    LinkedIds =
                    {
                        [Sites.IMDb] = imdbId
                    }
                }
            };

            var provider = new LookbaseSearchProvider(new LookbaseSiteProvider(Test.ProviderConfigService), null);

            var resultItems = (await provider
                .FindSimilarAsync(sourceItem, CancellationToken.None)
                .ConfigureAwait(false))
                .ToList();

            Assert.That(resultItems.Count > 0, Is.True, "Zero items count");
            CollectionAssert.AllItemsAreNotNull(resultItems, "Some item is null");
            CollectionAssert.AllItemsAreUnique(resultItems, "Items is not unique");

            foreach (var item in resultItems)
            {
                Assert.That(item.Site, Is.EqualTo(provider.Site), "Item site is invalid");
                Assert.That(item.Link, Is.Not.Null, "Item link is null");
                Assert.That(string.IsNullOrWhiteSpace(item.Title), Is.False, "Item title is empty");
            }
            if (!resultItems.Any(i => i.Details.LinkedIds.TryGetValue(Sites.IMDb, out var id)))
            {
                Assert.Inconclusive("No one item has imdb id");
            }
        }
    }
}
