namespace FSClient.UWP.Shared.Views.Pages
{
    using System;

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
    using FSClient.UWP.Shared.Views.Pages.Parameters;
    using FSClient.ViewModels;
    using FSClient.ViewModels.Items;

    public sealed partial class HomePage : Page, IStateSaveable
    {
        public HomePage()
        {
            ViewModel = ViewModelLocator.Current.ResolveViewModel<HomeViewModel>();

            InitializeComponent();

            if (CompatExtension.KeyboardAcceleratorsAvailable)
            {
                var keyAccelerator = new KeyboardAccelerator
                {
                    IsEnabled = true, Key = VirtualKey.F, Modifiers = VirtualKeyModifiers.Control
                };
                keyAccelerator.Invoked += (s, a) =>
                {
                    AutoSuggestBox.Focus(FocusState.Programmatic);
                    a.Handled = true;
                };
                AutoSuggestBox.KeyboardAccelerators.Add(keyAccelerator);
                ToolTipService.SetToolTip(
                    AutoSuggestBox,
                    ToolTipService.GetToolTip(AutoSuggestBox) + $" (Ctrl+{keyAccelerator.Key})");
            }
        }

        public HomeViewModel ViewModel { get; }

        public Uri? SaveStateToUri()
        {
            return UriParserHelper.GetProtocolUriFromViewModel(NavigationPageType.Home);
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            await ViewModel.UpdateSourceCommand.ExecuteAsync();
        }

        private void NavigateToItem(object sender, object args)
        {
            var item = args is ItemClickEventArgs itemClickArgs ? itemClickArgs.ClickedItem : args;
            if (item is ItemsListItemViewModel itemsListItemViewModel)
            {
                ViewModelLocator.Current.NavigationService.Navigate<ItemPage>(itemsListItemViewModel.ItemInfo);
            }
        }

        private void AutoSuggestBox_OnQuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
        {
            if (args.ChosenSuggestion is ItemInfo itemInfo)
            {
                ViewModelLocator.Current.NavigationService.Navigate<ItemPage>(itemInfo);
            }
            else
            {
                ViewModelLocator.Current.NavigationService.Navigate<SearchPage>(
                    new SearchPageParameter(args.QueryText));
            }
        }

        private void HomePivot_Loaded(object sender, RoutedEventArgs e)
        {
            HomePivot.Focus(FocusState.Pointer);
        }

        private async void Page_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            switch (e.Key)
            {
                case VirtualKey.GamepadY:
                    e.Handled = AutoSuggestBox.Focus(FocusState.Programmatic);
                    break;
                case VirtualKey.F5
                    when ViewModel.CurrentPage != null:
                    e.Handled = true;
                    await ViewModel.CurrentPage.UpdateCommand.ExecuteAsync(false).ConfigureAwait(false);
                    break;
            }
        }
    }
}
