namespace FSClient.Providers
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Runtime.CompilerServices;
    using System.Threading;
    using System.Threading.Tasks;

    using AngleSharp.Dom;

    using FSClient.Shared.Helpers.Web;
    using FSClient.Shared.Models;
    using FSClient.Shared.Providers;

    /// <inheritdoc/>
    public class ExFSReviewProvider : IReviewProvider
    {
        private readonly CultureInfo ruCultureInfo;
        private readonly ExFSSiteProvider siteProvider;

        public ExFSReviewProvider(ExFSSiteProvider siteProvider)
        {
            this.siteProvider = siteProvider;

            ruCultureInfo = new CultureInfo("ru-RU");
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
            var domain = await siteProvider.GetMirrorAsync(cancellationToken).ConfigureAwait(false);
            var itemLink = itemInfo.Link ?? new Uri(domain, $"/section/{itemInfo.SiteId}-item.html");

            var html = await siteProvider
                .HttpClient
                .GetBuilder(itemLink)
                .WithTimeSpanSemaphore(siteProvider.RequestSemaphore)
                .SendAsync(cancellationToken)
                .AsHtml(cancellationToken)
                .ConfigureAwait(false);
            var array = html?
                .QuerySelectorAll("#dle-comments-list .comment")
                .Select(el =>
                {
                    Uri.TryCreate(domain, el.QuerySelector("img")?.GetAttribute("src"), out var avatar);

                    var l = el.QuerySelector(".easylike_count")?.TextContent;
                    _ = int.TryParse(l?.Split(new[] { ' ', '(', ')' }, StringSplitOptions.RemoveEmptyEntries).LastOrDefault(),
                        out var likes);

                    var id = (el.Parent as IElement)?.Id?.Split('-').LastOrDefault();
                    if (id == null)
                    {
                        return default;
                    }

                    var dateNode = el.QuerySelector(".comment-right")?.Children?.Skip(1).FirstOrDefault()?.ChildNodes.Skip(2).FirstOrDefault()?.TextContent;

                    var reviewer = el.QuerySelector("a")?.TextContent?.Trim();
                    var description = el.QuerySelector(".comment-right")?.Children?.Skip(2)
                            .FirstOrDefault()?.TextContent?.Trim();
                    var isUserReview = reviewer == siteProvider.CurrentUser?.Nickname;

                    var review = new Review(Site, id, description, reviewer, isUserReview, avatar, ParseDate(dateNode));
                    var rating = new UpDownRating(likes, 0, false, false, DownVoteVisible: false, CanVote: true);

                    return (review, (IRating?)rating);
                })
                .Where(t => t.review != null)
                .ToArray() ?? Array.Empty<(Review, IRating?)>();

            foreach (var reviewItem in array)
            {
                yield return reviewItem!;
            }
        }

        private DateTime? ParseDate(string? content)
        {
            if (string.IsNullOrWhiteSpace(content))
            {
                return null;
            }

            content = content!.Trim(',').Trim();
            return DateTime.TryParse(content, ruCultureInfo, DateTimeStyles.None, out var result)
                ? result
                : (DateTime?)null;
        }

        public async Task<ProviderResult> SendReviewAsync(ItemInfo item, string review, CancellationToken cancellationToken)
        {
            if (item.SiteId == null)
            {
                return ProviderResult.Failed;
            }

            var nickname = siteProvider.CurrentUser?.Nickname;
            if (nickname == null)
            {
                return ProviderResult.NeedLogin;
            }

            var domain = await siteProvider.GetMirrorAsync(cancellationToken).ConfigureAwait(false);
            var response = await siteProvider
                .HttpClient
                .PostBuilder(new Uri(domain, "/engine/ajax/addcomments.php"))
                .WithBody(new Dictionary<string, string>
                {
                    ["post_id"] = item.SiteId,
                    ["comments"] = review,
                    ["level"] = "minus",
                    ["name"] = nickname,
                    ["mail"] = string.Empty,
                    ["editor_mode"] = string.Empty,
                    ["skin"] = "ex-fs",
                    ["sec_code"] = string.Empty,
                    ["question_answer"] = string.Empty,
                    ["recaptcha_response_field"] = string.Empty,
                    ["recaptcha_challenge_field"] = string.Empty,
                    ["allow_subscribe"] = "0"
                })
                .WithAjax()
                .WithTimeSpanSemaphore(siteProvider.RequestSemaphore)
                .SendAsync(cancellationToken)
                .ConfigureAwait(false);

            return cancellationToken.IsCancellationRequested ? ProviderResult.Canceled
                : response?.IsSuccessStatusCode == false ? ProviderResult.Failed
                : ProviderResult.Success;
        }

        public async Task<(IRating? rating, ProviderResult result)> VoteReviewAsync(Review review, IRating previousRating, IRatingVote ratingVote, CancellationToken cancellationToken)
        {
            if (siteProvider.CurrentUser == null)
            {
                return (null, ProviderResult.NeedLogin);
            }
            if (ratingVote is not UpDownRatingVote upDownRatingVote
                || previousRating is not UpDownRating upDownRating
                || !upDownRatingVote.UpVoted.HasValue
                || !upDownRatingVote.UpVoted.Value)
            {
                return (null, ProviderResult.NotSupported);
            }

            var domain = await siteProvider.GetMirrorAsync(cancellationToken).ConfigureAwait(false);
            var text = await siteProvider
                .HttpClient
                .PostBuilder(new Uri(domain, "/engine/ajax/easylike.php"))
                .WithBody(new Dictionary<string, string>
                {
                    ["comment_id"] = review.Id
                })
                .WithAjax()
                .WithTimeSpanSemaphore(siteProvider.RequestSemaphore)
                .SendAsync(cancellationToken)
                .AsText()
                .ConfigureAwait(false);

            var upCount = int.TryParse(text, out var votesCount) ? votesCount
                : text == ":-)" ? upDownRating.UpCount
                : (int?)null;
            if (upCount == null)
            {
                return (null, ProviderResult.Failed);
            }

            var newRating = new UpDownRating(upCount.Value, upDownRating.DownCount, true, false, DownVoteVisible: false, CanVote: false);

            return (newRating, ProviderResult.Success);
        }

        public async Task<(IRating? rating, ProviderResult result)> VoteItemAsync(ItemInfo itemInfo, IRating previousRating, IRatingVote ratingVote, CancellationToken cancellationToken)
        {
            if (siteProvider.CurrentUser == null)
            {
                return (null, ProviderResult.NeedLogin);
            }
            if (itemInfo.SiteId == null)
            {
                return (null, ProviderResult.Failed);
            }
            if (ratingVote is not UpDownRatingVote upDownRatingVote
                || previousRating is not UpDownRating upDownRating
                || upDownRatingVote.UpVoted == upDownRatingVote.DownVoted)
            {
                return (null, ProviderResult.NotSupported);
            }

            var domain = await siteProvider.GetMirrorAsync(cancellationToken).ConfigureAwait(false);

            var text = await siteProvider
                .HttpClient
                .PostBuilder(new Uri(domain, "engine/ajax/nrating.php"))
                .WithBody(new Dictionary<string, string>
                {
                    ["news_id"] = itemInfo.SiteId,
                    ["skin"] = "ex-fs",
                    ["go_rate"] = upDownRatingVote.UpVoted == true ? "1" : "-1"
                })
                .WithAjax()
                .WithTimeSpanSemaphore(siteProvider.RequestSemaphore)
                .SendAsync(cancellationToken)
                .AsText()
                .ConfigureAwait(false);

            var upCount = upDownRating.UpCount;
            var downCount = upDownRating.DownCount;
            if (int.TryParse(text, out var temp))
            {
                if (temp == 0)
                {
                    // User already voted
                }
                else if (temp == 1)
                {
                    if (upDownRatingVote.UpVoted == true)
                    {
                        upCount++;
                    }
                    else if (upDownRatingVote.DownVoted == true)
                    {
                        downCount++;
                    }
                }
                else
                {
                    return (null, ProviderResult.Failed);
                }
            }
            else
            {
                return (null, ProviderResult.Failed);
            }

            var newRating = new UpDownRating(
                upCount, downCount,
                upDownRatingVote.UpVoted ?? upDownRating.UpVoted,
                upDownRatingVote.DownVoted ?? upDownRating.DownVoted,
                DownVoteVisible: upDownRating.DownVoteVisible,
                CanVote: false);

            return (newRating, ProviderResult.Success);
        }
    }
}
