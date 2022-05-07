namespace FSClient.UWP.Shared.Views.Pages
{
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

    using FSClient.Shared.Providers;
    using FSClient.Shared.Services;
    using FSClient.UWP.Shared.Extensions;
    using FSClient.UWP.Shared.Services;
    using FSClient.UWP.Shared.Views.Pages.Parameters;
    using FSClient.ViewModels;
    using FSClient.ViewModels.Items;

    public sealed partial class SearchPage : Page, IStateSaveableProvider
    {
        public SearchPage()
        {
            ViewModel = ViewModelLocator.Current.ResolveViewModel<SearchViewModel>();

            InitializeComponent();

            if (CompatExtension.KeyboardAcceleratorsAvailable)
            {
                var keyAccelerator = new KeyboardAccelerator
                {
                    IsEnabled = true, Key = VirtualKey.F, Modifiers = VirtualKeyModifiers.Control
                };
                keyAccelerator.Invoked += (s, a) =>
                {
                    SearchBox.Focus(FocusState.Programmatic);
                    a.Handled = true;
                };
                SearchBox.KeyboardAccelerators.Add(keyAccelerator);
            }
        }

        public SearchViewModel ViewModel { get; }

        public IStateSaveable StateSaveable => ViewModel;

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            if (e.Parameter is SearchPageParameter parameter)
            {
                await ViewModel.UpdateSourceCommand.ExecuteAsync();
                ViewModel.SearchRequest = parameter.SearchRequest;
                ViewModel.SetProviderCommand.Execute(parameter.Site);
                await ViewModel.SearchCommand.ExecuteAsync(true);
            }
            else if (ViewModel.ResultPages.Count == 0)
            {
                await ViewModel.UpdateSourceCommand.ExecuteAsync();
            }
        }

        private void NavigateToItem(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is ItemsListItemViewModel itemsListItemViewModel)
            {
                ViewModelLocator.Current.NavigationService.Navigate<ItemPage>(itemsListItemViewModel.ItemInfo);
            }
        }

        private void SearchBox_Loaded(object sender, RoutedEventArgs e)
        {
            SearchBox.Focus(FocusState.Pointer);
        }

        private void Page_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            switch (e.Key)
            {
                case VirtualKey.GamepadY:
                    SearchBox.Focus(FocusState.Programmatic);
                    break;
            }
        }

        public static double ConvertDisplayItemModeToWidth(DisplayItemMode mode)
        {
            return mode switch
            {
                (DisplayItemMode.Detailed) => 400d,
                (DisplayItemMode.Minimal) => 600d,
                (DisplayItemMode.Normal) => 180d,
                _ => 180d
            };
        }
    }
}
