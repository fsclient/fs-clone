namespace FSClient.ViewModels
{
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    using FSClient.Localization.Resources;
    using FSClient.Shared.Helpers;
    using FSClient.Shared.Managers;
    using FSClient.Shared.Models;
    using FSClient.Shared.Mvvm;
    using FSClient.Shared.Services;
    using FSClient.ViewModels.Items;

    public class ReviewViewModel : ViewModelBase
    {
        private readonly IIncrementalCollectionFactory collectionFactory;
        private readonly IReviewManager reviewManager;
        private readonly INotificationService notificationService;

        public ReviewViewModel(
            IUserManager userManager,
            IReviewManager reviewManager,
            IIncrementalCollectionFactory collectionFactory,
            INotificationService notificationService)
        {
            this.collectionFactory = collectionFactory;
            this.reviewManager = reviewManager;
            this.notificationService = notificationService;

            userManager.UserLoggedOut += site =>
            {
                if (site == CurrentItem?.Site)
                {
                    UpdateCanSendReview();
                }
            };
            userManager.UserLoggedIn += (site, _) =>
            {
                if (site == CurrentItem?.Site)
                {
                    UpdateCanSendReview();
                }
            };

            SendReviewCommand = new AsyncCommand<string>(
                SendReviewAsync,
                _ => CanSendReview && CurrentItem != null);
        }

        public IIncrementalCollection<ReviewListItemViewModel> ReviewsSource
        {
            get => Get(IncrementalLoadingCollection.Empty<ReviewListItemViewModel>);
            private set
            {
                if (Set(value))
                {
                    HasAnyReview = false;
                }
            }
        }

        public bool HasAnyReview
        {
            get => Get<bool>();
            private set => Set(value);
        }

        public bool CanSendReview
        {
            get => Get(false);
            private set => Set(value);
        }

        public bool IsSupportedByItem
        {
            get => Get(false);
            private set => Set(value);
        }

        public ItemInfo? CurrentItem
        {
            get => Get<ItemInfo?>();
            set
            {
                if (Set(value))
                {
                    if (value != null)
                    {
                        IsSupportedByItem = true;

                        ReviewsSource = collectionFactory.Create(reviewManager.GetReviews(value)
                            .Select(tuple =>
                            {
                                if (!HasAnyReview)
                                {
                                    HasAnyReview = true;
                                }
                                return new ReviewListItemViewModel(tuple.review, tuple.rating, reviewManager, notificationService);
                            }));
                    }
                    else
                    {
                        IsSupportedByItem = false;
                        ReviewsSource = IncrementalLoadingCollection.Empty<ReviewListItemViewModel>();
                    }
                    UpdateCanSendReview();
                }
            }
        }

        public AsyncCommand<string> SendReviewCommand { get; set; }

        private async void UpdateCanSendReview()
        {
            CanSendReview = CurrentItem is ItemInfo itemInfo
                && await reviewManager.CanSendReviewForItemAsync(itemInfo, default).ConfigureAwait(false);
            SendReviewCommand.RaiseCanExecuteChanged();
        }

        private async Task SendReviewAsync(string reviewDescription, CancellationToken cancellationToken)
        {
            var currentItem = CurrentItem;
            if (currentItem == null)
            {
                return;
            }
            if (string.IsNullOrWhiteSpace(reviewDescription))
            {
                await notificationService.ShowAsync(Strings.ReviewsViewModel_EmptyReviewIsNotValid, NotificationType.Warning).ConfigureAwait(false);
                return;
            }

            var result = await reviewManager.SendReviewAsync(currentItem, reviewDescription, cancellationToken).ConfigureAwait(false);

            if (result == ProviderResult.Success)
            {
                if (CurrentItem != currentItem)
                {
                    return;
                }
                ReviewsSource.Reset();
            }

            if (result != ProviderResult.Canceled
                && result.GetDisplayDescription() is string description)
            {
                await notificationService
                    .ShowAsync(
                        string.Format(description, currentItem.Site.Title ?? "'-'"),
                        result == ProviderResult.Failed ? NotificationType.Error : NotificationType.Warning)
                    .ConfigureAwait(false);
            }
        }
    }
}
