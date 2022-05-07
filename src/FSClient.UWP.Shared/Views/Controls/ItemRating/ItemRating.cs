namespace FSClient.UWP.Shared.Views.Controls
{
    using System.Windows.Input;

#if WINUI3
    using Microsoft.UI.Xaml;
    using Microsoft.UI.Xaml.Controls;
    using Microsoft.UI.Xaml.Data;
#else
    using Windows.UI.Xaml;
    using Windows.UI.Xaml.Controls;
    using Windows.UI.Xaml.Data;
#endif

    using FSClient.Shared.Providers;

    public class ItemRating : UserControl
    {
        public bool IsReadOnly
        {
            get => (bool)GetValue(IsReadOnlyProperty);
            set => SetValue(IsReadOnlyProperty, value);
        }

        public static readonly DependencyProperty IsReadOnlyProperty =
            DependencyProperty.Register(nameof(IsReadOnly), typeof(bool), typeof(ItemRating), new PropertyMetadata(false));

        public bool ShowRatingProgress
        {
            get => (bool)GetValue(ShowRatingProgressProperty);
            set => SetValue(ShowRatingProgressProperty, value);
        }

        public static readonly DependencyProperty ShowRatingProgressProperty =
            DependencyProperty.Register(nameof(ShowRatingProgress), typeof(bool), typeof(ItemRating),
                new PropertyMetadata(true));

        public IRating? Rating
        {
            get => (IRating)GetValue(RatingProperty);
            set => SetValue(RatingProperty, value);
        }

        public static readonly DependencyProperty RatingProperty =
            DependencyProperty.Register(nameof(Rating), typeof(IRating), typeof(ItemRating),
                new PropertyMetadata(null, OnRatingPropertyChanged));

        public ICommand? VoteCommand
        {
            get => (ICommand)GetValue(VoteCommandProperty);
            set => SetValue(VoteCommandProperty, value);
        }

        public static readonly DependencyProperty VoteCommandProperty =
            DependencyProperty.Register(nameof(VoteCommand), typeof(ICommand), typeof(ItemRating), new PropertyMetadata(null));

        private static void OnRatingPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (ItemRating)d;

            if (e.NewValue is null)
            {
                control.Content = null;
            }
            else if (e.OldValue?.GetType() != e.NewValue?.GetType())
            {
                if (e.NewValue is UpDownRating)
                {
                    var rating = new ItemUpDownRating();
                    rating.SetBinding(FontSizeProperty, new Binding
                    {
                        Source = control,
                        Path = new PropertyPath(nameof(FontSize)),
                        Mode = BindingMode.TwoWay
                    });
                    rating.SetBinding(BackgroundProperty, new Binding
                    {
                        Source = control,
                        Path = new PropertyPath(nameof(Background)),
                        Mode = BindingMode.TwoWay
                    });
                    rating.SetBinding(ItemUpDownRating.ShowRatingProgressProperty, new Binding
                    {
                        Source = control,
                        Path = new PropertyPath(nameof(ShowRatingProgress)),
                        Mode = BindingMode.TwoWay
                    });
                    rating.SetBinding(ItemUpDownRating.IsReadOnlyProperty, new Binding
                    {
                        Source = control,
                        Path = new PropertyPath(nameof(IsReadOnly)),
                        Mode = BindingMode.TwoWay
                    });
                    rating.SetBinding(ItemUpDownRating.VoteCommandProperty, new Binding
                    {
                        Source = control,
                        Path = new PropertyPath(nameof(VoteCommand)),
                        Mode = BindingMode.TwoWay
                    });
                    rating.SetBinding(ItemUpDownRating.RatingProperty, new Binding
                    {
                        Source = control,
                        Path = new PropertyPath(nameof(Rating)),
                        Mode = BindingMode.TwoWay
                    });
                    control.Content = rating;
                }
                else if (e.NewValue is NumberBasedRating)
                {
                    var rating = new ItemNumberBasedRating();
                    rating.SetBinding(ItemNumberBasedRating.VoteCommandProperty, new Binding
                    {
                        Source = control,
                        Path = new PropertyPath(nameof(VoteCommand)),
                        Mode = BindingMode.TwoWay
                    });
                    rating.SetBinding(ItemNumberBasedRating.RatingProperty, new Binding
                    {
                        Source = control,
                        Path = new PropertyPath(nameof(Rating)),
                        Mode = BindingMode.TwoWay
                    });
                    control.Content = rating;
                }
            }
        }
    }
}
