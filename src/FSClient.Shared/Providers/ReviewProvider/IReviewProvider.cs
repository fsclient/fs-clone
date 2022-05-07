namespace FSClient.Shared.Providers
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    using FSClient.Shared.Models;

    /// <summary>
    /// User reviews provider.
    /// </summary>
    public interface IReviewProvider : IProviderWithRequirements
    {
        /// <summary>
        /// Gets reviews' paged async enumerable by specific item.
        /// </summary>
        /// <param name="itemInfo">Item info to filter reviews.</param>
        /// <returns><see cref="IAsyncEnumerable{T}"/> of tuple of <see cref="Review"/> and <see cref="IRating"/>.</returns>
        IAsyncEnumerable<(Review review, IRating? rating)> GetReviews(
            ItemInfo itemInfo);

        /// <summary>
        /// Sends review via provider specific for item from current user.
        /// </summary>
        /// <param name="itemInfo">Item to review.</param>
        /// <param name="reviewDescription">User review text.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Review sending result returned from provider.</returns>
        Task<ProviderResult> SendReviewAsync(
            ItemInfo itemInfo,
            string reviewDescription,
            CancellationToken cancellationToken);

        /// <summary>
        /// Vote a review.
        /// </summary>
        /// <param name="review">Review to vote.</param>
        /// <param name="previousRating">Previous rating.</param>
        /// <param name="ratingVote">Rating vote command that specifics vote action.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Tuple of new rating and result returned from provider. Rating is null, if failed.</returns>
        Task<(IRating? rating, ProviderResult result)> VoteReviewAsync(
            Review review,
            IRating previousRating,
            IRatingVote ratingVote,
            CancellationToken cancellationToken);

        /// <summary>
        /// Vote an item.
        /// </summary>
        /// <param name="itemInfo">Item to vote.</param>
        /// <param name="previousRating">Previous rating.</param>
        /// <param name="ratingVote">Rating vote command that specifics vote action.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Tuple of new rating and result returned from provider. Rating is null, if failed.</returns>
        Task<(IRating? rating, ProviderResult result)> VoteItemAsync(
            ItemInfo itemInfo,
            IRating previousRating,
            IRatingVote ratingVote,
            CancellationToken cancellationToken);
    }
}
