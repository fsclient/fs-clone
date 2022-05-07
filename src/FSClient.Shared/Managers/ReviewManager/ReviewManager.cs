namespace FSClient.Shared.Managers
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    using FSClient.Shared.Models;
    using FSClient.Shared.Providers;

    public class ReviewManager : IReviewManager
    {
        private readonly Dictionary<Site, IReviewProvider> reviewProviders;
        private readonly IUserManager userManager;

        public ReviewManager(
            IEnumerable<IReviewProvider> reviewProviders,
            IUserManager userManager)
        {
            this.reviewProviders = reviewProviders.ToDictionary(p => p.Site, p => p);
            this.userManager = userManager;
        }

        public ValueTask<bool> CanSendReviewForItemAsync(ItemInfo itemInfo, CancellationToken cancellationToken)
        {
            if (!(itemInfo != null && reviewProviders.ContainsKey(itemInfo.Site)))
            {
                return new ValueTask<bool>(false);
            }

            return userManager.CheckRequirementsAsync(itemInfo.Site, ProviderRequirements.AccountForAny, cancellationToken);
        }

        public IAsyncEnumerable<(Review, IRating?)> GetReviews(ItemInfo itemInfo)
        {
            if (itemInfo == null)
            {
                throw new ArgumentNullException(nameof(itemInfo));
            }

            if (reviewProviders.TryGetValue(itemInfo.Site, out var provider))
            {
                return provider.GetReviews(itemInfo)
                    .TakeWhileAwaitWithCancellation((_, ct) => userManager
                        .CheckRequirementsAsync(provider.Site, provider.ReadRequirements, ct));
            }

            return AsyncEnumerable.Empty<(Review, IRating?)>();
        }

        public async Task<ProviderResult> SendReviewAsync(ItemInfo itemInfo, string reviewDescription, CancellationToken cancellationToken)
        {
            if (itemInfo == null)
            {
                throw new ArgumentNullException(nameof(itemInfo));
            }
            if (reviewDescription == null)
            {
                throw new ArgumentNullException(nameof(reviewDescription));
            }

            if (reviewProviders.TryGetValue(itemInfo.Site, out var provider))
            {
                var allowed = await userManager.CheckRequirementsAsync(provider.Site, provider.ReadRequirements, cancellationToken).ConfigureAwait(false);
                if (!allowed)
                {
                    return ProviderResult.NeedLogin;
                }

                return await provider.SendReviewAsync(itemInfo, reviewDescription, cancellationToken).ConfigureAwait(false);
            }
            return ProviderResult.NoValidProvider;
        }

        public async Task<(IRating? rating, ProviderResult result)> VoteItemAsync(ItemInfo itemInfo, IRating previousRating, IRatingVote ratingVote, CancellationToken cancellationToken)
        {
            if (itemInfo == null)
            {
                throw new ArgumentNullException(nameof(itemInfo));
            }
            if (ratingVote == null)
            {
                throw new ArgumentNullException(nameof(ratingVote));
            }

            if (reviewProviders.TryGetValue(itemInfo.Site, out var provider))
            {
                var allowed = await userManager.CheckRequirementsAsync(provider.Site, provider.ReadRequirements | ProviderRequirements.AccountForAny, cancellationToken).ConfigureAwait(false);
                if (!allowed)
                {
                    return (null, ProviderResult.NeedLogin);
                }

                return await provider.VoteItemAsync(itemInfo, previousRating, ratingVote, cancellationToken).ConfigureAwait(false);
            }
            return (null, ProviderResult.NoValidProvider);
        }

        public async Task<(IRating? rating, ProviderResult result)> VoteReviewAsync(Review review, IRating previousRating, IRatingVote ratingVote, CancellationToken cancellationToken)
        {
            if (review == null)
            {
                throw new ArgumentNullException(nameof(review));
            }
            if (ratingVote == null)
            {
                throw new ArgumentNullException(nameof(ratingVote));
            }

            if (reviewProviders.TryGetValue(review.Site, out var provider))
            {
                var allowed = await userManager.CheckRequirementsAsync(provider.Site, provider.ReadRequirements | ProviderRequirements.AccountForAny, cancellationToken).ConfigureAwait(false);
                if (!allowed)
                {
                    return (null, ProviderResult.NeedLogin);
                }

                return await provider.VoteReviewAsync(review, previousRating, ratingVote, cancellationToken).ConfigureAwait(false);
            }
            return (null, ProviderResult.NoValidProvider);
        }
    }
}
