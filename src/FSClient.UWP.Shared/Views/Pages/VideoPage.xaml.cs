namespace FSClient.UWP.Shared.Views.Pages
{
    using System;
    using System.IO;
    using System.Threading.Tasks;

#if WINUI3
    using Microsoft.UI.Xaml;
    using Microsoft.UI.Xaml.Controls;
    using Microsoft.UI.Xaml.Navigation;
#else
    using Windows.UI.Xaml;
    using Windows.UI.Xaml.Controls;
    using Windows.UI.Xaml.Navigation;
#endif

    using FSClient.Localization.Resources;
    using FSClient.Shared.Models;
    using FSClient.Shared.Services;
    using FSClient.UWP.Shared.Helpers;
    using FSClient.UWP.Shared.Services;
    using FSClient.UWP.Shared.Views.Controls;
    using FSClient.ViewModels;

    using File = FSClient.Shared.Models.File;

    public sealed partial class VideoPage : Page, IStateSaveableProvider
    {
        private static readonly Lazy<ManagedWindow> videoWindow =
            new Lazy<ManagedWindow>(ManagedWindow.GetOrCreate<VideoPage>);

        public static ManagedWindow VideoWindow => videoWindow.Value;

        public VideoPage()
        {
            MediaViewModel = ViewModelLocator.Current.ResolveViewModel<MediaViewModel>();
            FileViewModel = ViewModelLocator.Current.ResolveViewModel<FileViewModel>();
            InitializeComponent();
        }

        public FileViewModel FileViewModel { get; }

        public MediaViewModel MediaViewModel { get; }

        public IStateSaveable StateSaveable => MediaViewModel;

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            await InitFromParameterAsync(e.Parameter).ConfigureAwait(false);
        }

        private Task InitFromParameterAsync(object? parameter)
        {
            switch (parameter)
            {
                case Video video:
                    MediaViewModel.CurrentVideo = video;
                    break;
                case File file:
                    MediaViewModel.CurrentFile = file;
                    break;
            }
            return Task.CompletedTask;
        }

        private void PlayerControl_PlaylistEnded(object sender, EventArgs e)
        {
            if (PlayerControl.WindowMode != WindowMode.CompactOverlay)
            {
                PlayerControl.WindowMode = WindowMode.None;
            }
        }

        private async void InNewWindowButton_Click(object sender, RoutedEventArgs e)
        {
            var parameter = (object?)MediaViewModel.CurrentFile ?? MediaViewModel.CurrentVideo;
            var result = await VideoWindow.ShowAsync(parameter).ConfigureAwait(true);
            if (result)
            {
                await ViewModelLocator.Current.NavigationService.GoBack(false);
            }
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                if (ManagedWindow.GetCurrent(Window.Current) is ManagedWindow window)
                {
                    window.Destroying -= Window_Destroying;
                    window.Destroying += Window_Destroying;
                    window.Showed -= Window_Showed;
                    window.Showed += Window_Showed;
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.LogError(ex);
            }
        }

        private async void Window_Showed(object? sender, WindowShowedEventArgs e)
        {
            if (sender is ManagedWindow window
                && window.IsActive
                && !window.IsMainWindow)
            {
                window.Title = Strings.Window_PlayerWindowTitle;
            }

            await InitFromParameterAsync(e.Parameter).ConfigureAwait(false);
        }

        private void Page_Unloaded(object sender, RoutedEventArgs e)
        {
            if (ManagedWindow.GetCurrent(Window.Current) is ManagedWindow window)
            {
                window.Destroying -= Window_Destroying;
                window.Showed -= Window_Showed;
            }

            Bindings.StopTracking();
        }

        private async void Window_Destroying(object? sender, Nito.AsyncEx.IDeferralSource e)
        {
            // Trigger PlayerControl.Unloaded event
            await RootGrid.Dispatcher.CheckBeginInvokeOnUI(() =>
            {
                RootGrid.Children.Clear();
                Bindings.StopTracking();
            });
        }

        private async void OpenRemoteButton_Click(object sender, RoutedEventArgs e)
        {
            await PlayerControl.PauseAsync();
        }

        private async void ShareGrabbedFrameButton_Click(object sender, RoutedEventArgs e)
        {
            if (PlayerControl.PlayingFile is not File playingFile)
            {
                return;
            }

            var frame = await PlayerControl.GrabCurrentFrameStreamAsync();
            if (frame == null)
            {
                return;
            }

            var streamForRead = frame.AsStreamForRead();

            var shareService = ViewModelLocator.Current.Resolve<IShareService>();
            await shareService.ShareFrameAsync(streamForRead, PlayerControl.Position, playingFile);
        }
    }
}
