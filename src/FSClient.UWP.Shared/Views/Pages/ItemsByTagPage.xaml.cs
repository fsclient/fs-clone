namespace FSClient.UWP.Shared.Views.Pages
{
#if WINUI3
    using Microsoft.UI.Xaml;
    using Microsoft.UI.Xaml.Controls;
    using Microsoft.UI.Xaml.Navigation;
#else
    using Windows.UI.Xaml;
    using Windows.UI.Xaml.Controls;
    using Windows.UI.Xaml.Navigation;
#endif

    using FSClient.Shared.Models;
    using FSClient.Shared.Services;
    using FSClient.UWP.Shared.Services;
    using FSClient.ViewModels;
    using FSClient.ViewModels.Items;

    public sealed partial class ItemsByTagPage : Page, IStateSaveableProvider
    {
        public ItemsByTagPage()
        {
            ViewModel = ViewModelLocator.Current.ResolveViewModel<ItemByTagViewModel>();

            InitializeComponent();
        }

        public ItemByTagViewModel ViewModel { get; }

        public IStateSaveable StateSaveable => ViewModel;

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            if (e.Parameter is TitledTag titledTag)
            {
                ViewModel.CurrentTag = titledTag;
                await ViewModel.UpdateSourceCommand.ExecuteAsync();
                if (ViewModel.CurrentPage != null)
                {
                    await ViewModel.CurrentPage.UpdateCommand.ExecuteAsync(true);
                }
            }
        }

        private void NavigateToItem(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is ItemsListItemViewModel itemsListItemViewModel)
            {
                ViewModelLocator.Current.NavigationService.Navigate<ItemPage>(itemsListItemViewModel.ItemInfo);
            }
        }

        private void ItemsGrid_Loaded(object sender, RoutedEventArgs e)
        {
            Pivot.Focus(FocusState.Pointer);
        }
    }
}
