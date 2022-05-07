namespace FSClient.Providers
{
    using System;
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
    public class FilmixReviewProvider : IReviewProvider
    {
        private readonly FilmixSiteProvider siteProvider;

        public FilmixReviewProvider(
            FilmixSiteProvider siteProvider)
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
            var page = 1;

            var section = FilmixSiteProvider.GetSectionStringFromLink(itemInfo.Link) ?? "drama";

            var domain = await siteProvider.GetMirrorAsync(cancellationToken).ConfigureAwait(false);

            var baseLink = new Uri(domain, $"{section}/{itemInfo.SiteId}/commentary");

            while (true)
            {
                var link = page == 1
                    ? baseLink
                    : new Uri(baseLink.ToString() + $"/page/{page}/");
                page++;

                var response = await siteProvider
                    .HttpClient
                    .GetBuilder(link)
                    .WithTimeSpanSemaphore(siteProvider.RequestSemaphore)
                    .SendAsync(cancellationToken)
                    .ConfigureAwait(false);

                var html = await response
                    .AsHtml(cancellationToken)
                    .ConfigureAwait(false);

                var items = html?.QuerySelectorAll(".comment-box");
                if (items == null)
                {
                    yield break;
                }

                if (response?.RequestMessage.RequestUri?.ToString() is { } requestUri)
                {
                    var pageIndex = requestUri.IndexOf("/page");
                    baseLink = pageIndex >= 0
                        ? new Uri(requestUri.Substring(0, pageIndex))
                        : new Uri(requestUri);
                }

                var reviews = items
                    .Select(i => (node: i, id: i.GetAttribute("data-id")))
                    .Where(t => t.id != null)
                    .Select(t => (node: t.node, review: new Review(
                        Site, t.id!,
                        Description: t.node.QuerySelector(".comment-text")?.FirstElementChild?.TextContent,
                        Reviewer: t.node.QuerySelector(".comment-name")?.TextContent,
                        IsUserReview: t.node.ClassList.Contains("my"),
                        Avatar: Uri.TryCreate(domain, t.node.QuerySelector(".avatar")?.GetAttribute("src"), out var avatar) ? avatar : null,
                        Date: ParseDate(t.node.QuerySelector(".comment-date")?.TextContent))))
                    .Where(t => t.review?.Id != null)
                    .Select(t =>
                    {
                        var (i, r) = t;

                        var rating = new UpDownRating(
                            int.TryParse(i.QuerySelector(".comment-rating .like")?.TextContent.TrimStart('+'), out var votesUp) ? votesUp : 0,
                            int.TryParse(i.QuerySelector(".comment-rating .dislike")?.TextContent.TrimStart('-'), out var votesDown) ? votesDown : 0,
                            i.QuerySelector(".comment-rating .like .active") != null,
                            i.QuerySelector(".comment-rating .dislike .active") != null,
                            CanVote: r.IsUserReview == false);

                        return (r, (IRating?)rating);
                    })
                    .ToArray();

                foreach (var reviewItem in reviews)
                {
                    yield return reviewItem;
                }

                if (reviews.Length == 0)
                {
                    yield break;
                }
            }
        }

        public async Task<ProviderResult> SendReviewAsync(ItemInfo item, string review, CancellationToken cancellationToken)
        {
            var user = siteProvider.CurrentUser;

            if (string.IsNullOrWhiteSpace(item.SiteId))
            {
                return ProviderResult.Failed;
            }
            if (string.IsNullOrWhiteSpace(user?.Nickname))
            {
                return ProviderResult.NeedLogin;
            }

            var domain = await siteProvider.GetMirrorAsync(cancellationToken).ConfigureAwait(false);

            var result = await siteProvider
                .HttpClient
                .PostBuilder(new Uri(domain, "engine/ajax/comments_handler.php"))
                .WithTimeSpanSemaphore(siteProvider.RequestSemaphore)
                .WithHeader("Origin", domain.GetOrigin())
                .WithHeader("Referer", item.Link?.ToString() ?? string.Empty)
                .WithBody(new Dictionary<string, string>
                {
                    ["action"] = "commentAdd",
                    ["name"] = user!.Nickname ?? string.Empty,
                    ["post_id"] = item.SiteId ?? string.Empty,
                    ["comments"] = review
                })
                .WithAjax()
                .SendAsync(cancellationToken)
                .AsNewtonsoftJson<JObject>()
                .ConfigureAwait(false);

            return cancellationToken.IsCancellationRequested ? ProviderResult.Canceled
                : result?["type"]?.ToString() == "success" ? ProviderResult.Success
                : ProviderResult.Failed;
        }

        public async Task<(IRating? rating, ProviderResult result)> VoteReviewAsync(Review review, IRating previousRating, IRatingVote ratingVote, CancellationToken cancellationToken)
        {
            if (siteProvider.CurrentUser == null)
            {
                return (null, ProviderResult.NeedLogin);
            }

            if (string.IsNullOrEmpty(review.Id)
                || ratingVote is not UpDownRatingVote upDownRatingVote
                || previousRating is not UpDownRating upDownRating
                || upDownRatingVote.UpVoted == upDownRatingVote.DownVoted)
            {
                return (null, ProviderResult.NotSupported);
            }

            if ((upDownRatingVote.UpVoted == upDownRating.UpVoted)
                || (upDownRatingVote.DownVoted == upDownRating.DownVoted))
            {
                return (upDownRating, ProviderResult.Success);
            }

            var domain = await siteProvider.GetMirrorAsync(cancellationToken).ConfigureAwait(false);

            var result = await siteProvider
                .HttpClient
                .PostBuilder(new Uri(domain, "engine/ajax/comm_rating.php"))
                .WithTimeSpanSemaphore(siteProvider.RequestSemaphore)
                .WithBody(new Dictionary<string, string>
                {
                    ["id"] = review.Id ?? string.Empty,
                    ["action"] = upDownRating.UpVoted == true ? "like" : "dislike"
                })
                .WithAjax()
                .SendAsync(cancellationToken)
                .AsNewtonsoftJson<JObject>()
                .ConfigureAwait(false);

            if (result == null)
            {
                return (null, ProviderResult.Failed);
            }

            var upCount = result["likes"]?.ToIntOrNull() ?? upDownRating.UpCount;
            var downCount = result["dislikes"]?.ToIntOrNull() ?? upDownRating.DownCount;

            var newRating = new UpDownRating(
                upCount, downCount,
                upDownRatingVote.UpVoted ?? false,
                upDownRatingVote.DownVoted ?? false,
                CanVote: upDownRating.CanVote);

            return (newRating, ProviderResult.Success);
        }

        public async Task<(IRating? rating, ProviderResult result)> VoteItemAsync(ItemInfo itemInfo, IRating previousRating, IRatingVote ratingVote, CancellationToken cancellationToken)
        {
            if (siteProvider.CurrentUser == null)
            {
                return (null, ProviderResult.NeedLogin);
            }

            if (string.IsNullOrEmpty(itemInfo.SiteId)
                || ratingVote is not UpDownRatingVote upDownRatingVote
                || previousRating is not UpDownRating upDownRating
                || upDownRatingVote.UpVoted == upDownRatingVote.DownVoted)
            {
                return (null, ProviderResult.NotSupported);
            }

            if ((upDownRatingVote.UpVoted == upDownRating.UpVoted)
                || (upDownRatingVote.DownVoted == upDownRating.DownVoted))
            {
                return (upDownRating, ProviderResult.Success);
            }

            var domain = await siteProvider.GetMirrorAsync(cancellationToken).ConfigureAwait(false);

            var result = await siteProvider
                .HttpClient
                .GetBuilder(new Uri(domain, "engine/ajax/rating.php"))
                .WithTimeSpanSemaphore(siteProvider.RequestSemaphore)
                .WithArguments(new Dictionary<string, string?>
                {
                    ["go_rate"] = upDownRating.UpVoted == true ? "1" : "-1",
                    ["news_id"] = itemInfo.SiteId,
                    ["skin"] = "Filmix",
                    ["module"] = "showfull"
                })
                .WithAjax()
                .SendAsync(cancellationToken)
                .AsNewtonsoftJson<JObject>()
                .ConfigureAwait(false);

            if (result == null
                || result["type"]?.ToString() is not string type
                || type != "success")
            {
                return (null, ProviderResult.Failed);
            }

            // Votes count is stored in "error" object for some reason.
            var upCount = result["error"]?["p_votes"]?.ToIntOrNull() ?? upDownRating.UpCount;
            var downCount = result["error"]?["n_votes"]?.ToIntOrNull() ?? upDownRating.DownCount;

            var newRating = new UpDownRating(
                upCount, downCount,
                upDownRatingVote.UpVoted ?? false,
                upDownRatingVote.DownVoted ?? false,
                CanVote: upDownRating.CanVote);

            return (newRating, ProviderResult.Success);
        }

        private static readonly string[] Monthes =
        {
            "янв", "фев", "мар", "апр", "мая", "июн", "июл",
            "авг", "сен", "окт", "ноя", "дек"
        };

        private static DateTime? ParseDate(string? dateString)
        {
            // Сегодня, 00:10
            // Вчера, 00:10
            // 27 фев 2017 22:56

            try
            {
                if (string.IsNullOrEmpty(dateString))
                {
                    return null;
                }

                var strParts = dateString!.Split(',', ' ', ':');
                if (strParts.Length < 2)
                {
                    return null;
                }

                var today = DateTime.Today;

                _ = int.TryParse(strParts[^2], out var hour);
                _ = int.TryParse(strParts[^1], out var minutes);

                if (strParts.Length == 2 || strParts[0] == "Сегодня")
                {
                    return new DateTime(today.Year, today.Month, today.Day, hour, minutes, 0);
                }

                if (strParts.Length >= 5)
                {
                    var month = Array.FindIndex(Monthes, m => strParts[1]
                        .StartsWith(m, StringComparison.OrdinalIgnoreCase)) + 1;
                    if (month == 0)
                    {
                        return null;
                    }

                    _ = int.TryParse(strParts[0], out var day);
                    _ = int.TryParse(strParts[2], out var year);

                    return new DateTime(year, month, day, hour, minutes, 0);
                }

                if (strParts[0] == "Вчера")
                {
                    var yesterday = today - TimeSpan.FromDays(1);
                    return new DateTime(yesterday.Year, yesterday.Month, yesterday.Day, hour, minutes, 0);
                }

                return null;
            }
            catch
            {
                return null;
            }
        }
    }
}
