namespace FSClient.UWP.Shared.Views.Controls
{
    using System.Windows.Input;

#if WINUI3
    using Microsoft.UI.Xaml;
    using Microsoft.UI.Xaml.Documents;
    using Microsoft.UI.Xaml.Controls;
    using Microsoft.UI.Xaml.Controls.Primitives;
#else
    using Windows.UI.Xaml;
    using Windows.UI.Xaml.Documents;
    using Windows.UI.Xaml.Controls;
    using Windows.UI.Xaml.Controls.Primitives;
#endif

    using FSClient.Shared.Providers;

    public class ItemUpDownRating : Control
    {
        public ItemUpDownRating()
        {
            DefaultStyleKey = nameof(ItemUpDownRating);
        }

        public bool IsReadOnly
        {
            get => (bool)GetValue(IsReadOnlyProperty);
            set => SetValue(IsReadOnlyProperty, value);
        }

        public static readonly DependencyProperty IsReadOnlyProperty =
            DependencyProperty.Register(nameof(IsReadOnly), typeof(bool), typeof(ItemUpDownRating), new PropertyMetadata(false));

        public bool ShowRatingProgress
        {
            get => (bool)GetValue(ShowRatingProgressProperty);
            set => SetValue(ShowRatingProgressProperty, value);
        }

        public static readonly DependencyProperty ShowRatingProgressProperty =
            DependencyProperty.Register(nameof(ShowRatingProgress), typeof(bool), typeof(ItemUpDownRating),
                new PropertyMetadata(true, OnShowRatingProgressChanged));

        public UpDownRating? Rating
        {
            get => GetValue(RatingProperty) as UpDownRating;
            set => SetValue(RatingProperty, value);
        }

        public static readonly DependencyProperty RatingProperty =
            DependencyProperty.Register(nameof(Rating), typeof(UpDownRating), typeof(ItemUpDownRating),
                new PropertyMetadata(null, OnRatingPropertyChanged));

        public ICommand? VoteCommand
        {
            get => (ICommand)GetValue(VoteCommandProperty);
            set => SetValue(VoteCommandProperty, value);
        }

        public static readonly DependencyProperty VoteCommandProperty =
            DependencyProperty.Register(nameof(VoteCommand), typeof(ICommand), typeof(ItemUpDownRating), new PropertyMetadata(null));

        protected override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            if (GetTemplateChild("VoteUpItemButton") is ButtonBase voteUpButton)
            {
                voteUpButton.Click += VoteUpButton_Click;
            }
            if (GetTemplateChild("VoteDownItemButton") is ButtonBase voteDownButton)
            {
                voteDownButton.Click += VoteDownButton_Click;
            }

            UpdateStateTriggers();
            UpdateValues();
        }

        private void VoteUpButton_Click(object sender, RoutedEventArgs e)
        {
            var ratingVote = new UpDownRatingVote(true, false);
            if (VoteCommand is ICommand command
                && command.CanExecute(ratingVote))
            {
                command.Execute(ratingVote);
            }
        }

        private void VoteDownButton_Click(object sender, RoutedEventArgs e)
        {
            var ratingVote = new UpDownRatingVote(false, true);
            if (VoteCommand is ICommand command
                && command.CanExecute(ratingVote))
            {
                command.Execute(ratingVote);
            }
        }

        private void UpdateStateTriggers()
        {
            var ratingVisibilityState = ShowRatingProgress && Rating != null && Rating.HasAnyVote
                ? "RatingVisible" : "RatingHidden";
            VisualStateManager.GoToState(this, ratingVisibilityState, false);

            var upDownOrUpOnlyState = Rating == null ? "Hidden"
                : Rating.DownVoteVisible ? "UpDown"
                : "UpOnly";
            VisualStateManager.GoToState(this, upDownOrUpOnlyState, false);

            var voteState = IsReadOnly ? "ReadOnly"
                : Rating == null ? "Normal"
                : Rating.UpVoted ? "UpVoted"
                : Rating.DownVoted ? "DownVoted"
                : "Normal";
            VisualStateManager.GoToState(this, voteState, true);
        }

        private void UpdateValues()
        {
            if (Rating is not UpDownRating rating)
            {
                return;
            }

            if (ShowRatingProgress && rating.HasAnyVote
                && GetTemplateChild("RatingProgressBar") is ProgressBar ratingProgressBar)
            {
                ratingProgressBar.Value = rating.Value * 100;
            }

            if (GetTemplateChild("UpCountRun") is Run upCountRun)
            {
                upCountRun.Text = rating.UpCount.ToString();
            }

            if (GetTemplateChild("DownCountRun") is Run downCountRun)
            {
                downCountRun.Text = rating.DownCount.ToString();
            }
        }

        private static void OnRatingPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (ItemUpDownRating)d;
            control.UpdateStateTriggers();
            control.UpdateValues();
        }

        private static void OnShowRatingProgressChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (ItemUpDownRating)d;
            control.UpdateStateTriggers();
            control.UpdateValues();
        }
    }
}
