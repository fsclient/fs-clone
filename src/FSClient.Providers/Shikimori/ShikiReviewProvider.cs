namespace FSClient.Providers
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Runtime.CompilerServices;
    using System.Threading;
    using System.Threading.Tasks;

    using FSClient.Shared.Helpers.Web;
    using FSClient.Shared.Models;
    using FSClient.Shared.Providers;

    using Newtonsoft.Json.Linq;

    /// <inheritdoc/>
    public class ShikiReviewProvider : IReviewProvider
    {
        private readonly ShikiSiteProvider siteProvider;

        public ShikiReviewProvider(
            ShikiSiteProvider siteProvider)
        {
            this.siteProvider = siteProvider;
        }

        public Site Site => siteProvider.Site;

        public ProviderRequirements ReadRequirements => ProviderRequirements.None;

        public IAsyncEnumerable<(Review, IRating?)> GetReviews(ItemInfo itemInfo)
        {
            return GetReviewsInternal(itemInfo);
        }

        private async IAsyncEnumerable<(Review, IRating?)> GetReviewsInternal(ItemInfo itemInfo,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var loadedCount = 0;

            var domain = await siteProvider.GetMirrorAsync(cancellationToken).ConfigureAwait(false);

            var itemInfoTask = await siteProvider
                .GetFromApiAsync($"/api/animes/{itemInfo.SiteId}", cancellationToken)
                .AsNewtonsoftJson<JObject>()
                .ConfigureAwait(false);
            var itemTopicId = itemInfoTask?["topic_id"]?.ToString();
            if (string.IsNullOrWhiteSpace(itemTopicId))
            {
                yield break;
            }

            var apiReviews = await siteProvider
                .GetFromApiAsync(
                    "/api/comments",
                    new Dictionary<string, string>
                    {
                        ["commentable_id"] = itemTopicId!,
                        ["commentable_type"] = "Topic",
                        ["limit"] = "1",
                        ["page"] = "1"
                    },
                    cancellationToken)
                .AsNewtonsoftJson<JArray>()
                .ConfigureAwait(false);
            var startId = (apiReviews?.FirstOrDefault() as JObject)?["id"]?.ToString();
            if (string.IsNullOrWhiteSpace(startId))
            {
                yield break;
            }

            while (true)
            {
                var link = new Uri(domain, $"comments/fetch/{startId}/Topic/{itemTopicId}/is_summary/{loadedCount}/10");
                loadedCount += 10;

                var json = await siteProvider
                    .HttpClient
                    .GetBuilder(link)
                    .WithTimeSpanSemaphore(siteProvider.RequestSemaphore)
                    .SendAsync(cancellationToken)
                    .AsNewtonsoftJson<JObject>()
                    .ConfigureAwait(false);
                var htmlStr = json?["content"]?.ToString();
                if (htmlStr == null)
                {
                    yield break;
                }

                var html = WebHelper.ParseHtml(htmlStr);
                if (html == null)
                {
                    yield break;
                }

                var reviews = html
                    .QuerySelectorAll("div.b-comment")
                    .Select(div =>
                    {
                        var id = div.GetAttribute("data-track_comment") ?? div.GetAttribute("id");
                        if (id == null)
                        {
                            return null;
                        }

                        var reviewer = div.QuerySelector("a[itemprop='creator']")?.TextContent;
                        var avatar = ShikiSiteProvider.GetImageLink(div.QuerySelector("header img[src]")?.GetAttribute("src"), domain);
                        var desc = ShikiSiteProvider.ParseShikiHtml(div.QuerySelector("[itemprop='commentText'],[itemprop='text']"));
                        var date = ParseDate(div.QuerySelector("header time")?.GetAttribute("datetime"));

                        return new Review(Site, id, desc, reviewer, IsUserReview: null, avatar, date);
                    })
                    .Where(r => r != null)
                    .Reverse()
                    .ToArray();

                foreach (var reviewItem in reviews)
                {
                    yield return (reviewItem!, null);
                }

                var hasNextPage = reviews.Length >= 10;
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

        private static DateTime? ParseDate(string? date)
        {
            if (date == null)
            {
                return null;
            }

            // "2018-02-18T20:25:05+03:00"
            if (DateTime.TryParse(date, CultureInfo.InvariantCulture, DateTimeStyles.None, out var result))
            {
                return result;
            }

            return null;
        }
    }
}
