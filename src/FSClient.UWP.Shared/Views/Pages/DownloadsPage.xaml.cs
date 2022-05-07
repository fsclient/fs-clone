namespace FSClient.UWP.Shared.Views.Pages
{
    using System;

#if WINUI3
    using Microsoft.UI.Xaml;
    using Microsoft.UI.Xaml.Controls;
    using Microsoft.UI.Xaml.Navigation;
#else
    using Windows.UI.Xaml;
    using Windows.UI.Xaml.Controls;
    using Windows.UI.Xaml.Navigation;
#endif

    using FSClient.Shared.Helpers;
    using FSClient.Shared.Services;
    using FSClient.UWP.Shared.Services;
    using FSClient.ViewModels;
    using FSClient.ViewModels.Items;

    public sealed partial class DownloadsPage : Page, IStateSaveable
    {
        public DownloadsPage()
        {
            ViewModel = ViewModelLocator.Current.ResolveViewModel<DownloadViewModel>();

            InitializeComponent();
        }

        public DownloadViewModel ViewModel { get; }

        public Uri? SaveStateToUri()
        {
            return UriParserHelper.GetProtocolUriFromViewModel(NavigationPageType.Downloads);
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            await ViewModel.UpdateSourceCommand.ExecuteAsync();
        }

        public static Symbol ConvertStatusToIcon(DownloadStatus status, bool pauseSupported)
        {
            return status switch
            {
                DownloadStatus.Paused => Symbol.Play,
                DownloadStatus.Running when !pauseSupported => Symbol.Stop,
                DownloadStatus.Running => Symbol.Pause,
                DownloadStatus.Resuming => Symbol.Pause,
                DownloadStatus.FileMissed => Symbol.Cancel,
                DownloadStatus.Canceled => Symbol.Cancel,
                DownloadStatus.Completed => Symbol.Accept,
                DownloadStatus.Idle => Symbol.Clock,
                DownloadStatus.Error => Symbol.ReportHacked,
                DownloadStatus.NoNetwork => Symbol.ReportHacked,
                _ => Symbol.Sync,
            };
        }

        private void DownloadsGrid_Loaded(object sender, RoutedEventArgs e)
        {
            DownloadsGrid.Focus(FocusState.Pointer);
        }

        private void DownloadsGrid_ItemClick(object _, ItemClickEventArgs e)
        {
            if (e.ClickedItem is DownloadFileViewModel downloadFileViewModel
                && downloadFileViewModel.OpenFileCommand.CanExecute())
            {
                downloadFileViewModel.OpenFileCommand.Execute();
            }
        }
    }
}
