namespace FSClient.UWP.Shared.Views.Controls
{
    using System.Windows.Input;

#if WINUI3
    using Microsoft.UI.Xaml;
    using Microsoft.UI.Xaml.Controls;
    using Microsoft.UI.Xaml.Controls.Primitives;
#else
    using Windows.UI.Xaml;
    using Windows.UI.Xaml.Controls;
    using Windows.UI.Xaml.Controls.Primitives;
#endif

    using FSClient.Shared.Models;
    using FSClient.Shared.Providers;
    using FSClient.UWP.Shared.Helpers;

    public partial class ItemsListItem : Control
    {
        public ICommand ItemPreloadCommand
        {
            get => (ICommand)GetValue(ItemPreloadCommandProperty);
            set => SetValue(ItemPreloadCommandProperty, value);
        }

        public static readonly DependencyProperty ItemPreloadCommandProperty =
            DependencyProperty.Register(nameof(ItemPreloadCommand), typeof(ICommand), typeof(ItemsListItem),
                new PropertyMetadata(null, OnItemPreloadCommandChanged));

        private static async void OnItemPreloadCommandChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var item = ((ItemsListItem)d);
            await item.WaitForLoadedAsync().ConfigureAwait(true);
            var selector = item.FindVisualAscendant<SelectorItem>();
            if (selector != null)
            {
                selector.GotFocus -= ItemContainer_GotFocus;
                selector.GotFocus += ItemContainer_GotFocus;

                selector.PointerEntered -= ItemContainer_PointerEntered;
                selector.PointerEntered += ItemContainer_PointerEntered;
            }
        }

        public ICommand DeleteFromHistoryCommand
        {
            get => (ICommand)GetValue(DeleteFromHistoryCommandProperty);
            set => SetValue(DeleteFromHistoryCommandProperty, value);
        }

        public static readonly DependencyProperty DeleteFromHistoryCommandProperty =
            DependencyProperty.Register(nameof(DeleteFromHistoryCommand), typeof(ICommand), typeof(ItemsListItem),
                new PropertyMetadata(null, EnsureStatesOnChanged));

        public ItemInfo ItemInfo
        {
            get => (ItemInfo)GetValue(ItemInfoProperty);
            set => SetValue(ItemInfoProperty, value);
        }

        public static readonly DependencyProperty ItemInfoProperty =
            DependencyProperty.Register(nameof(ItemInfo), typeof(ItemInfo), typeof(ItemsListItem),
                new PropertyMetadata(null));

        public HistoryItem HistoryItem
        {
            get => (HistoryItem)GetValue(HistoryItemProperty);
            set => SetValue(HistoryItemProperty, value);
        }

        public static readonly DependencyProperty HistoryItemProperty =
            DependencyProperty.Register(nameof(HistoryItem), typeof(HistoryItem), typeof(ItemsListItem),
                new PropertyMetadata(null, EnsureStatesOnChanged));

        public DisplayItemMode DisplayItemMode
        {
            get => (DisplayItemMode)GetValue(DisplayItemModeProperty);
            set => SetValue(DisplayItemModeProperty, value);
        }

        public static readonly DependencyProperty DisplayItemModeProperty =
            DependencyProperty.Register(nameof(DisplayItemMode), typeof(DisplayItemMode), typeof(ItemsListItem),
                new PropertyMetadata(DisplayItemMode.Normal, EnsureStatesOnChanged));

        public bool IsItemPreloading
        {
            get => (bool)GetValue(IsItemPreloadingProperty);
            set => SetValue(IsItemPreloadingProperty, value);
        }

        public static readonly DependencyProperty IsItemPreloadingProperty =
            DependencyProperty.Register(nameof(IsItemPreloading), typeof(bool), typeof(ItemsListItem),
                new PropertyMetadata(false));

        private static void EnsureStatesOnChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var sender = (ItemsListItem)d;
            sender.EnsureStates();
        }
    }
}
