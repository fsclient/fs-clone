namespace FSClient.UWP.Shared.Views.Pages
{
    using System;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

#if WINUI3
    using Microsoft.UI.Xaml;
    using Microsoft.UI.Xaml.Markup;
    using Microsoft.UI.Xaml.Controls;
    using Microsoft.UI.Xaml.Navigation;
#else
    using Windows.UI.Xaml;
    using Windows.UI.Xaml.Markup;
    using Windows.UI.Xaml.Controls;
    using Windows.UI.Xaml.Navigation;
#endif

    using FSClient.Localization.Resources;
    using FSClient.Shared.Helpers;
    using FSClient.Shared.Services;
    using FSClient.UWP.Shared.Helpers;
    using FSClient.UWP.Shared.Services;
    using FSClient.UWP.Shared.Views.Controls;
    using FSClient.ViewModels;

    public sealed partial class SettingsPage : Page, IStateSaveable
    {
        public SettingsPage()
        {
            ViewModel = ViewModelLocator.Current.ResolveViewModel<SettingViewModel>();

            InitializeComponent();

            switch (UWPAppInformation.Instance.DeviceFamily)
            {
                case DeviceFamily.Desktop:
                    ShowPCSettings = true;
                    CanOpenVPNSettings = CanOpenProxySettings = true;
                    break;
                case DeviceFamily.Xbox:
                    ShowXBoxSettings = true;
                    break;
                case DeviceFamily.Mobile:
                    CanOpenVPNSettings = true;
                    break;
            }
        }

        public SettingViewModel ViewModel { get; }

        public bool CanOpenVPNSettings { get; }

        public bool CanOpenProxySettings { get; }

        public bool ShowPCSettings { get; }

        public bool ShowXBoxSettings { get; }

        public bool CompactOverlayAllowed => ManagedWindow.OverlaySupported;

        public bool ProjectRomeAllowed => true;

        public Uri? SaveStateToUri()
        {
            return UriParserHelper.GetProtocolUriFromViewModel(NavigationPageType.Settings);
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            var openDonatePage = e.Parameter != null
                                 && (bool)XamlBindingHelper.ConvertValue(typeof(bool), e.Parameter);

            await Task.WhenAll(
                ViewModel.UpdateSourceCommand.ExecuteAsync(),
                InitMasterDetailsViewAsync(openDonatePage));
        }

        private async Task InitMasterDetailsViewAsync(bool openDonatePage)
        {
            await MasterDetailsView.WaitForLoadedAsync();
            if (openDonatePage)
            {
                MasterDetailsView.SelectedItem = MasterDetailsView.Items.LastOrDefault();
            }
            else if (MasterDetailsView.ViewState == MasterDetailsViewState.Both)
            {
                MasterDetailsView.SelectedItem = MasterDetailsView.Items.FirstOrDefault();
            }

            MasterDetailsView.Focus(FocusState.Pointer);
        }

        private async void ClearCacheButton_Click(object sender, RoutedEventArgs e)
        {
            var result = await CacheHelper.ClearCacheAsync(true);
            var message = result
                ? Strings.SettingsViewModel_ClearCacheSuccessfully
                : Strings.SettingsViewModel_ClearCacheFailed;
            var messageType = result ? NotificationType.Completed : NotificationType.Error;
            await ViewModelLocator.Current.Resolve<INotificationService>().ShowAsync(message, messageType);
        }

        private async void MarginCalibrationLink_Click(object sender, RoutedEventArgs e)
        {
            await ViewModelLocator.Current.Resolve<MarginCalibrationService>()
                .EnsureMarginCalibratedAsync(true, CancellationToken.None)
                .ConfigureAwait(false);
        }
    }
}
