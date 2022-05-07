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
    using FSClient.Shared.Models;
    using FSClient.Shared.Services;
    using FSClient.UWP.Shared.Extensions;
    using FSClient.UWP.Shared.Services;
    using FSClient.UWP.Shared.Helpers;
    using FSClient.ViewModels;
    using FSClient.ViewModels.Items;

    public sealed partial class HistoryPage : Page, IStateSaveable
    {
        private HistoryItem? lastFocusedItem;

        public HistoryPage()
        {
            ViewModel = ViewModelLocator.Current.ResolveViewModel<HistoryViewModel>();

            InitializeComponent();

            ViewModel.PropertyChanged += (s, a) =>
            {
                if (a.PropertyName == nameof(ViewModel.HistorySource))
                {
                    ScrollIntoLastFocused();
                }
            };
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

        public HistoryViewModel ViewModel { get; }

        public Uri? SaveStateToUri()
        {
            return UriParserHelper.GetProtocolUriFromViewModel(NavigationPageType.History);
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            await ViewModel.UpdateSourceCommand.ExecuteAsync();
        }

        private void NavigateToItem(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is HistoryListItemViewModel itemsListItemViewModel)
            {
                ViewModelLocator.Current.NavigationService.Navigate<ItemPage>(itemsListItemViewModel.HistoryItem);
            }
        }

        private async void Page_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            switch (e.Key)
            {
                case VirtualKey.GamepadY:
                    e.Handled = AutoSuggestBox.Focus(FocusState.Programmatic);
                    break;
                case VirtualKey.F5:
                    e.Handled = true;
                    await ViewModel.UpdateSourceCommand.ExecuteAsync().ConfigureAwait(false);
                    break;
            }
        }

        private void HistoryList_Loaded(object sender, RoutedEventArgs e)
        {
            HistoryList.Focus(FocusState.Pointer);
        }

        private void SelectionModeToggle_Click(object sender, RoutedEventArgs e)
        {
            SaveLastFocused();

            ViewModel.UpdateSourceCommand.Execute();
        }

        private void SaveLastFocused()
        {
            try
            {
                var historyItem = FocusManager.GetFocusedElement() is DependencyObject inFocus
                    ? HistoryList.ItemFromContainer(inFocus) as HistoryItem
                    : null;

                lastFocusedItem = historyItem ?? HistoryList
                    .Items
                    .Select(i => new
                    {
                        Item = i, Container = HistoryList.ContainerFromItem(i) as FrameworkElement
                    })
                    .FirstOrDefault(i => HistoryList.IsChildVisibileToUser(i.Container))?
                    .Item as HistoryItem;
            }
            catch (Exception ex)
            {
                Logger.Instance.LogError(ex);
            }
        }

        private void ScrollIntoLastFocused()
        {
            try
            {
                if (lastFocusedItem == null)
                {
                    return;
                }

                lastFocusedItem = HistoryList.Items
                    .Select(i => i as HistoryItem)
                    .FirstOrDefault(i => i != null && lastFocusedItem.IsSimilar(i));

                if (lastFocusedItem != null)
                {
                    HistoryList.ScrollIntoView(lastFocusedItem, ScrollIntoViewAlignment.Leading);
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.LogError(ex);
            }
        }

        private void Page_Unloaded(object sender, RoutedEventArgs e)
        {
            ViewModel.IsInSelectionMode = false;
        }
    }
}
