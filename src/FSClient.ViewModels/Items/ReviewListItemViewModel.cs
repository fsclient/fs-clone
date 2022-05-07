namespace FSClient.ViewModels.Items
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    using FSClient.Shared.Helpers;
    using FSClient.Shared.Managers;
    using FSClient.Shared.Models;
    using FSClient.Shared.Mvvm;
    using FSClient.Shared.Providers;
    using FSClient.Shared.Services;

    using Humanizer;

    public class ReviewListItemViewModel : ViewModelBase
    {
        private readonly Review review;
        private readonly IReviewManager reviewManager;
        private readonly INotificationService notificationService;

        public ReviewListItemViewModel(
            Review review,
            IRating? rating,
            IReviewManager reviewManager,
            INotificationService notificationService)
        {
            Rating = rating;
            this.review = review;
            this.reviewManager = reviewManager;
            this.notificationService = notificationService;

            VoteCommand = new AsyncCommand<IRatingVote>(
                VoteAsync,
                _ => rating?.CanVote ?? false,
                AsyncCommandConflictBehaviour.Skip);
        }

        public Site Site => review.Site;

        public Uri? Avatar => review.Avatar;
        public DateTime? Date => review.Date;
        public bool? IsUserReview => review.IsUserReview;
        public string? Reviewer => review.Reviewer;

        public IRating? Rating
        {
            get => Get<IRating>();
            private set
            {
                if (Set(value))
                {
                    VoteCommand?.RaiseCanExecuteChanged();
                }
            }
        }

        public string? Description => review.Description;

        public AsyncCommand<IRatingVote> VoteCommand { get; }

        private async Task VoteAsync(IRatingVote ratingVote, CancellationToken cancellationToken)
        {
            var (newRating, result) = await reviewManager.VoteReviewAsync(review, Rating!, ratingVote, cancellationToken).ConfigureAwait(false);
            if (newRating != null)
            {
                Rating = newRating;
            }

            if (result != ProviderResult.Success
                && EnumHelper.GetDisplayDescription(result) is string errorMessage)
            {
                await notificationService.ShowAsync(errorMessage.FormatWith(review.Site.Title), NotificationType.Warning).ConfigureAwait(false);
            }
        }
    }
}
