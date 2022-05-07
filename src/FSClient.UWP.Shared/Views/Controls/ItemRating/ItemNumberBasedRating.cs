namespace FSClient.UWP.Shared.Views.Controls
{
    using System.Threading.Tasks;
    using System.Windows.Input;

    using RatingControl = Microsoft.UI.Xaml.Controls.RatingControl;
#if WINUI3
    using Microsoft.UI.Xaml;
    using Microsoft.UI.Xaml.Controls;
#else
    using Windows.UI.Xaml;
    using Windows.UI.Xaml.Controls;
#endif

    using FSClient.Shared.Providers;
    using FSClient.UWP.Shared.Helpers;

    public class ItemNumberBasedRating : UserControl
    {
        private readonly RatingControl ratingControl;

        public ItemNumberBasedRating()
        {
            ratingControl = new RatingControl();
            ratingControl.MaxRating = 5;
            ratingControl.IsReadOnly = true;
            ratingControl.ValueChanged += ItemNumberBasedRating_ValueChanged;

            Content = new Viewbox
            {
                Child = ratingControl,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                MaxHeight = 68,
                Margin = new Thickness(12, 4, 12, 4)
            };
        }

        public NumberBasedRating? Rating
        {
            get => GetValue(RatingProperty) as NumberBasedRating;
            set => SetValue(RatingProperty, value);
        }

        public static readonly DependencyProperty RatingProperty =
            DependencyProperty.Register(nameof(Rating), typeof(NumberBasedRating), typeof(ItemUpDownRating),
                new PropertyMetadata(null, OnRatingChanged));

        public ICommand? VoteCommand
        {
            get => (ICommand)GetValue(VoteCommandProperty);
            set => SetValue(VoteCommandProperty, value);
        }

        public static readonly DependencyProperty VoteCommandProperty =
            DependencyProperty.Register(nameof(VoteCommand), typeof(ICommand), typeof(ItemUpDownRating), new PropertyMetadata(null));

        private async void ItemNumberBasedRating_ValueChanged(RatingControl sender, object args)
        {
            await Task.Yield();

            if (!sender.IsLoaded())
            {
                return;
            }

            // TODO Not implemented yet
        }

        private static void OnRatingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (ItemNumberBasedRating)d;

            if (e.NewValue is NumberBasedRating numberBasedRating)
            {
                control.ratingControl.Value = 5 * numberBasedRating.Value / numberBasedRating.BaseNumber;
            }
        }
    }
}
