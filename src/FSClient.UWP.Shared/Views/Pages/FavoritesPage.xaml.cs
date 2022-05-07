namespace FSClient.UWP.Shared.Views.Pages
{
    using System;
    using System.Linq;

    using Windows.System;

#if WINUI3
    using Microsoft.UI.Xaml;
    using Microsoft.UI.Xaml.Input;
    using Microsoft.UI.Xaml.Controls;
    using Microsoft.UI.Xaml.Navigation;
#else
    using Windows.UI.Xaml;
    using Windows.UI.Xaml.Input;
    using Windows.UI.Xaml.Controls;
    using Windows.UI.Xaml.Navigation;
#endif

    using FSClient.Shared.Helpers;
    using FSClient.Shared.Services;
    using FSClient.UWP.Shared.Extensions;
    using FSClient.UWP.Shared.Helpers;
    using FSClient.UWP.Shared.Services;
    using FSClient.UWP.Shared.Views.Controls;
    using FSClient.ViewModels;
    using FSClient.ViewModels.Items;

    public sealed partial class FavoritesPage : Page, IStateSaveable
    {
        private readonly FavMenuFlyout favMenuFlyout;

        public FavoritesPage()
        {
            ViewModel = ViewModelLocator.Current.ResolveViewModel<FavoriteViewModel>();
            favMenuFlyout = new FavMenuFlyout();

            InitializeComponent();

            if (CompatExtension.KeyboardAcceleratorsAvailable)
            {
                var keyAccelerator = new KeyboardAccelerator
                {
                    IsEnabled = true, Key = VirtualKey.F, Modifiers = VirtualKeyModifiers.Control
                };
                keyAccelerator.Invoked += (_, a) =>
                {
                    AutoSuggestBox.Focus(FocusState.Programmatic);
                    a.Handled = true;
                };
                AutoSuggestBox.KeyboardAccelerators.Add(keyAccelerator);
            }
        }

        public FavoriteViewModel ViewModel { get; }

        public Uri? SaveStateToUri()
        {
            return UriParserHelper.GetProtocolUriFromViewModel(NavigationPageType.Favorites);
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            await ViewModel.UpdateCommand.ExecuteAsync(default).ConfigureAwait(true);

            base.OnNavigatedTo(e);
        }

        private void NavigateToItem(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is ItemsListItemViewModel itemsListItemViewModel)
            {
                ViewModelLocator.Current.NavigationService.Navigate<ItemPage>(itemsListItemViewModel.ItemInfo);
            }
        }

        private void FavoritesPivot_Loaded(object sender, RoutedEventArgs e)
        {
            FavoritesPivot.PivotItemLoaded -= FavoritesPivot_PivotItemLoaded;
            FavoritesPivot.PivotItemLoaded += FavoritesPivot_PivotItemLoaded;
            FavoritesPivot.Focus(FocusState.Pointer);
        }

        private void FavoritesPivot_PivotItemLoaded(Pivot sender, PivotItemEventArgs args)
        {
            FavoritesPivot.Focus(FocusState.Pointer);
            FavoritesPivot.PivotItemLoaded -= FavoritesPivot_PivotItemLoaded;
        }

        private void Page_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            switch (e.Key)
            {
                case VirtualKey.GamepadY:
                    e.Handled = AutoSuggestBox.Focus(FocusState.Programmatic);
                    break;
            }
        }

        private void SelectionModeToggle_Click(object sender, object _)
        {
            var isChecked = (sender as AppBarToggleButton)?.IsChecked
                            ?? (sender as ToggleMenuFlyoutItem)?.IsChecked
                            ?? false;

            var pageLists = FavoritesPivot
                .Items
                .Select(i => FavoritesPivot.ContainerFromItem(i))
                .Select(c => c?.FindVisualChild<ListViewBase>("ItemsGridView"))
                .Where(l => l != null);

            foreach (var list in pageLists)
            {
                list!.SelectionMode = isChecked
                    ? ListViewSelectionMode.Multiple
                    : ListViewSelectionMode.None;
                list.IsItemClickEnabled = !isChecked;
            }
        }

        private void MoveButton_Click(object sender, object _)
        {
            favMenuFlyout.ItemInfoSource = FavoritesPivot
                .Items
                .Select(i => FavoritesPivot.ContainerFromItem(i))
                .Select(c => c?.FindVisualChild<ListViewBase>("ItemsGridView"))
                .Where(l => l?.SelectionMode == ListViewSelectionMode.Multiple)
                .SelectMany(l => l!.SelectedItems)
                .OfType<ItemsListItemViewModel>()
                .Select(i => i.ItemInfo)
                .ToList()
                .AsReadOnly();

            if (favMenuFlyout.ItemInfoSource.Count > 0)
            {
                favMenuFlyout.ShowAt((FrameworkElement)sender);
            }
        }

        private void ItemsGridView_SelectionChanged(object sender, object _)
        {
            var pageLists = FavoritesPivot
                .Items
                .Select(i => FavoritesPivot.ContainerFromItem(i))
                .Select(c => c?.FindVisualChild<ListViewBase>("ItemsGridView"))
                .Where(l => l != null)
                .ToList();

            var isEnabled = pageLists
                .Where(l => l!.SelectionMode == ListViewSelectionMode.Multiple)
                .SelectMany(l => l!.SelectedItems)
                .Any();
            if (MoveButton != null)
            {
                MoveButton.IsEnabled = isEnabled;
            }

            if (BottomMoveButton != null)
            {
                BottomMoveButton.IsEnabled = isEnabled;
            }

            var list = (ListViewBase)sender;
            if (list.SelectionMode == ListViewSelectionMode.None && ViewModel.IsInSelectionMode)
            {
                list.SelectionMode = ListViewSelectionMode.Multiple;
                list.IsItemClickEnabled = false;
            }
        }

        private void ItemsGridView_ChoosingItemContainer(ListViewBase sender, ChoosingItemContainerEventArgs args)
        {
            if (args.ItemContainer == null)
            {
                var container = new GridViewItem {ContentTemplate = sender.ItemTemplate};
                container.Loaded += ContainerLoaded;

                args.ItemContainer = container;
                args.IsContainerPrepared = true;

                void ContainerLoaded(object _, object __)
                {
                    container.Loaded -= ContainerLoaded;
                    if (container.FindVisualChild("RootGrid") is Grid root
                        && ContextMenuExtension.GetContextFlyout(root) is FavMenuFlyout flyout)
                    {
                        flyout.Opening += Flyout_Opening;
                    }
                }
            }
        }

        private void Flyout_Opening(object sender, object _)
        {
            var flyout = (FavMenuFlyout)sender;

            var items = FavoritesPivot
                .Items
                .Select(i => FavoritesPivot.ContainerFromItem(i))
                .Select(c => c?.FindVisualChild<ListViewBase>("ItemsGridView"))
                .Where(l => l?.SelectionMode == ListViewSelectionMode.Multiple)
                .SelectMany(l => l!.SelectedItems)
                .OfType<ItemsListItemViewModel>()
                .Select(i => i.ItemInfo)
                .ToList();

            if (flyout.ItemInfo != null
                && !items.Contains(flyout.ItemInfo))
            {
                var currentList = (FavoritesPivot
                        .ContainerFromItem(FavoritesPivot.SelectedItem) as PivotItem)?
                    .FindVisualChild<ListViewBase>("ItemsGridView");
                if (currentList?.Items.Contains(flyout.ItemInfo) == true)
                {
                    if (currentList.SelectionMode == ListViewSelectionMode.Multiple)
                    {
                        currentList.SelectedItems.Add(flyout.ItemInfo);
                    }

                    items.Insert(0, flyout.ItemInfo);
                }
            }

            ((FavMenuFlyout)sender).ItemInfoSource = items.AsReadOnly();
        }
    }
}
