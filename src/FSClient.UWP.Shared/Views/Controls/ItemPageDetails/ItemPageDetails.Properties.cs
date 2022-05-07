namespace FSClient.UWP.Shared.Views.Controls
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Windows.Input;

#if WINUI3
    using Microsoft.UI.Xaml;
#else
    using Windows.UI.Xaml;
#endif

    using FSClient.Shared.Models;
    using FSClient.Shared.Mvvm;
    using FSClient.Shared.Providers;
    using FSClient.UWP.Shared.Helpers;
    using FSClient.ViewModels.Items;

    public partial class ItemPageDetails
    {
        public ItemInfo CurrentItem
        {
            get => (ItemInfo)GetValue(CurrentItemProperty);
            set => SetValue(CurrentItemProperty, value);
        }

        public static readonly DependencyProperty CurrentItemProperty =
            DependencyProperty.Register(nameof(CurrentItem), typeof(ItemInfo), typeof(ItemPageDetails),
                new PropertyMetadata(null, CurrentItemChanged));

        public IRating? Rating
        {
            get => (IRating?)GetValue(RatingProperty);
            set => SetValue(RatingProperty, value);
        }

        public static readonly DependencyProperty RatingProperty =
            DependencyProperty.Register(nameof(Rating), typeof(IRating), typeof(ItemPageDetails),
                new PropertyMetadata(null));

        private static void CurrentItemChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var itemPageDetails = (ItemPageDetails)d;
            itemPageDetails.Loaded -= ItemPageDetails_Loaded;
            if (itemPageDetails.IsLoaded())
            {
                ItemPageDetails_Loaded(itemPageDetails, null);
            }
            else
            {
                itemPageDetails.Loaded += ItemPageDetails_Loaded;
            }

            static void ItemPageDetails_Loaded(object sender, object? e)
            {
                var itemPageDetails = (ItemPageDetails)sender;
                itemPageDetails.BuildInfoView(itemPageDetails.CurrentItem);
            }
        }

        public ICommand OpenItemInBrowserCommand
        {
            get => (ICommand)GetValue(OpenItemInBrowserCommandProperty);
            set => SetValue(OpenItemInBrowserCommandProperty, value);
        }

        public static readonly DependencyProperty OpenItemInBrowserCommandProperty =
            DependencyProperty.Register(nameof(OpenItemInBrowserCommand), typeof(ICommand), typeof(ItemPageDetails),
                new PropertyMetadata(null));

        public AsyncCommand OpenTrailerCommand
        {
            get => (AsyncCommand)GetValue(OpenTrailerCommandProperty);
            set => SetValue(OpenTrailerCommandProperty, value);
        }

        public static readonly DependencyProperty OpenTrailerCommandProperty =
            DependencyProperty.Register(nameof(OpenTrailerCommand), typeof(AsyncCommand), typeof(ItemPageDetails),
                new PropertyMetadata(null));

        public ICommand PreloadItemCommand
        {
            get => (ICommand)GetValue(PreloadItemCommandProperty);
            set => SetValue(PreloadItemCommandProperty, value);
        }

        public static readonly DependencyProperty PreloadItemCommandProperty =
            DependencyProperty.Register(nameof(PreloadItemCommand), typeof(ICommand), typeof(ItemPageDetails),
                new PropertyMetadata(null));

        public ICommand? VoteCommand
        {
            get => (ICommand)GetValue(VoteCommandProperty);
            set => SetValue(VoteCommandProperty, value);
        }

        public static readonly DependencyProperty VoteCommandProperty =
            DependencyProperty.Register(nameof(VoteCommand), typeof(ICommand), typeof(ItemUpDownRating), new PropertyMetadata(null));

        public IEnumerable<ItemsListItemViewModel> SimilarItems
        {
            get => (IEnumerable<ItemsListItemViewModel>)GetValue(SimilarItemsProperty);
            set => SetValue(SimilarItemsProperty, value);
        }

        public static readonly DependencyProperty SimilarItemsProperty =
            DependencyProperty.Register(nameof(SimilarItems), typeof(IEnumerable<ItemsListItemViewModel>),
                typeof(ItemPageDetails),
                new PropertyMetadata(Enumerable.Empty<ItemsListItemViewModel>()));

        public IEnumerable<ItemsListItemViewModel> FranchiseItems
        {
            get => (IEnumerable<ItemsListItemViewModel>)GetValue(FranchiseItemsProperty);
            set => SetValue(FranchiseItemsProperty, value);
        }

        public static readonly DependencyProperty FranchiseItemsProperty =
            DependencyProperty.Register(nameof(FranchiseItems), typeof(IEnumerable<ItemsListItemViewModel>),
                typeof(ItemPageDetails),
                new PropertyMetadata(Enumerable.Empty<ItemsListItemViewModel>()));
    }
}
