namespace FSClient.Providers
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Runtime.CompilerServices;
    using System.Threading;
    using System.Threading.Tasks;

    using FSClient.Shared.Helpers;
    using FSClient.Shared.Helpers.Web;
    using FSClient.Shared.Models;
    using FSClient.Shared.Providers;

    using Newtonsoft.Json.Linq;

    /// <inheritdoc/>
    public class TMDbReviewProvider : IReviewProvider
    {
        private readonly TMDbSiteProvider siteProvider;

        public TMDbReviewProvider(
            TMDbSiteProvider siteProvider)
        {
            this.siteProvider = siteProvider;
        }

        public Site Site => siteProvider.Site;

        public ProviderRequirements ReadRequirements => ProviderRequirements.None;

        public IAsyncEnumerable<(Review, IRating?)> GetReviews(ItemInfo itemInfo)
        {
            var ruSource = GetReviewsFromApiForLanugage(itemInfo, "ru-RU");
            var enSource = GetReviewsFromApiForLanugage(itemInfo, "en-US");

            return ruSource.Concat(enSource);
        }

        private async IAsyncEnumerable<(Review, IRating?)> GetReviewsFromApiForLanugage(ItemInfo itemInfo, string lang,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var section = itemInfo.Section.Modifier.HasFlag(SectionModifiers.Serial) ? "tv" : "movie";

            var page = 1;
            while (true)
            {
                var currentPage = page++;

                var json = await siteProvider
                    .GetFromApiAsync(
                        $"{section}/{itemInfo.SiteId}/reviews",
                        new Dictionary<string, string?>
                        {
                            ["page"] = currentPage.ToString(),
                            ["language"] = lang
                        },
                        cancellationToken)
                    .AsNewtonsoftJson<JObject>()
                    .ConfigureAwait(false);

                if (json?["results"] == null)
                {
                    yield break;
                }

                var reviews = json["results"]
                    .Where(i => i["id"] != null)
                    .Select(i => new Review(
                        Site, i["id"]!.ToString(),
                        Description: i["content"]?.ToString(),
                        Reviewer: i["author"]?.ToString()))
                    .ToArray();

                foreach (var reviewItem in reviews)
                {
                    yield return (reviewItem, null);
                }

                var hasNextPage = (json["page"]?.ToIntOrNull() ?? currentPage) < json["total_pages"]?.ToIntOrNull();
                if (!hasNextPage)
                {
                    yield break;
                }
            }
        }

        public Task<ProviderResult> SendReviewAsync(ItemInfo item, string review, CancellationToken cancellationToken)
        {
            return Task.FromResult(ProviderResult.NotSupported);
        }

        public Task<(IRating? rating, ProviderResult result)> VoteReviewAsync(Review review, IRating previousRating, IRatingVote ratingVote, CancellationToken cancellationToken)
        {
            return Task.FromResult(((IRating?)null, ProviderResult.NotSupported));
        }

        public Task<(IRating? rating, ProviderResult result)> VoteItemAsync(ItemInfo itemInfo, IRating previousRating, IRatingVote ratingVote, CancellationToken cancellationToken)
        {
            return Task.FromResult(((IRating?)null, ProviderResult.NotSupported));
        }
    }
}
