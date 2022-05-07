namespace FSClient.UWP.Shared.Views.Pages
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    using Windows.UI.Core;

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
    using FSClient.Shared;
    using FSClient.Shared.Managers;
    using FSClient.Shared.Models;
    using FSClient.Shared.Services;
    using FSClient.UWP.Shared.Extensions;
    using FSClient.UWP.Shared.Helpers;
    using FSClient.UWP.Shared.Services;
    using FSClient.UWP.Shared.Views.Controls;
    using FSClient.UWP.Shared.Views.Dialogs;
    using FSClient.ViewModels;

    using Microsoft.Extensions.Logging;

    public sealed partial class ItemPage : Page, IStateSaveableProvider
    {
        private bool isActive;

        private readonly ILogger logger;
        private readonly LazyDialog<AddReviewDialog, string> reviewDialog;

        public ItemPage()
        {
            ItemViewModel = ViewModelLocator.Current.ResolveViewModel<ItemViewModel>();
            FileViewModel = ViewModelLocator.Current.ResolveViewModel<FileViewModel>();
            ReviewViewModel = ViewModelLocator.Current.ResolveViewModel<ReviewViewModel>();
            MediaViewModel = ViewModelLocator.Current.ResolveViewModel<MediaViewModel>();

            reviewDialog = new LazyDialog<AddReviewDialog, string>();

            InitializeComponent();

            logger = Logger.Instance;

            ViewModelLocator.Current.Resolve<IFileManager>().VideoOpened += FileManager_VideoOpened;
        }

        public ItemViewModel ItemViewModel { get; }
        public FileViewModel FileViewModel { get; }
        public MediaViewModel MediaViewModel { get; }
        public ReviewViewModel ReviewViewModel { get; }

        public bool IsXbox => UWPAppInformation.Instance.DeviceFamily == DeviceFamily.Xbox;

        public IStateSaveable StateSaveable => FileViewModel;

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            try
            {
                isActive = true;
                HeaderedWithInfoReviews.FindAscendant<ScrollViewer>()?.ChangeView(0, 0, 1, true);

                BackgroundImage.Source = null;

                ViewModelLocator.Current.NavigationService.GoBackRequested += OnBackRequested;

                await HidePlayerControlAsync().ConfigureAwait(true);

                var historyItem = e.Parameter as HistoryItem;
                if (historyItem is HistoryItemRange historyItemRange)
                {
                    historyItem = historyItemRange.LatestItem ?? historyItemRange;
                }

                var itemInfo = historyItem?.ItemInfo ?? (e.Parameter as ItemInfo);
                await ItemViewModel.RefreshCurrentItemCommand.ExecuteAsync(itemInfo).ConfigureAwait(true);

                FileViewModel.CurrentItem = ReviewViewModel.CurrentItem = ItemViewModel.CurrentItem;
                FileViewModel.HistoryItem = historyItem;

                if (!isActive
                    || ItemViewModel.CurrentItem == null)
                {
                    return;
                }

                if (ItemViewModel.IsPreloaded)
                {
                    if (!ItemPivot.Items.Contains(DescriptionPage))
                    {
                        ItemPivot.Items.Insert(0, DescriptionPage);
                    }

                    ItemPageDetails.CurrentItem = ItemViewModel.CurrentItem;
                    ItemPageDetails.SimilarItems = ItemViewModel.SimilarItems;
                    ItemPageDetails.FranchiseItems = ItemViewModel.FranchiseItems;
                }
                else if (ItemPivot.Items.Contains(DescriptionPage))
                {
                    ItemPivot.Items.Remove(DescriptionPage);
                }

                FileViewModel.UpdateProvidersCommand.Execute();

                await Task
                    .WhenAll(
                        NavigatePivotPageIfNeed(ItemViewModel.CurrentItem, FileViewModel.HistoryItem,
                            e.NavigationMode),
                        FileViewModel.LoadLastCommand.ExecuteAsync(),
                        LoadBackgroundImage())
                    .ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                logger?.LogError(ex);
            }
        }

        private async Task NavigatePivotPageIfNeed(
            ItemInfo infoInfo, HistoryItem? historyItem,
            NavigationMode navigationMode)
        {
            try
            {
                if (navigationMode != NavigationMode.New)
                {
                    return;
                }

                if (!ItemPivot.Items.Contains(DescriptionPage))
                {
                    return;
                }

                var filesPage = false;
                if (infoInfo != null
                    && ItemViewModel.IsInAnyList
                    && Settings.Instance.OpenFavItemsOnFilesPage)
                {
                    filesPage = true;
                }

                if ((historyItem != null || ItemViewModel.IsInHistory)
                    && Settings.Instance.OpenHistoryItemsOnFilesPage)
                {
                    filesPage = true;
                }

                if (filesPage
                    && ItemPivot.SelectedItem == FilesPage)
                {
                    return;
                }

                if (!filesPage
                    && ItemPivot.SelectedItem == DescriptionPage)
                {
                    return;
                }

                await Dispatcher.RunIdleAsync(args =>
                {
                    if (filesPage && ItemPivot.Items.Contains(FilesPage))
                    {
                        ItemPivot.SelectedItem = FilesPage;
                    }
                    else if (ItemPivot.Items.Contains(DescriptionPage))
                    {
                        ItemPivot.SelectedItem = DescriptionPage;
                    }
                });
            }
            catch (Exception ex)
            {
                logger?.LogError(ex);
            }
        }

        private async Task LoadBackgroundImage()
        {
            try
            {
                if (!Settings.Instance.ShowImageBackground)
                {
                    return;
                }

                var uri = ItemViewModel.CurrentItem?.Details.Images?.FirstOrDefault()[ImageSize.Original];
                if (uri == null)
                {
                    return;
                }

                var image = await BlurHelper.BlurFromUri(uri).ConfigureAwait(true);
                if (image == null)
                {
                    return;
                }

                BackgroundAppear.Begin();
                BackgroundImage.Source = image;
            }
            catch (Exception ex)
            {
                logger?.LogError(ex);
            }
        }

        protected override async void OnNavigatedFrom(NavigationEventArgs e)
        {
            isActive = false;

            if (ItemPivot.Items.Contains(DescriptionPage))
            {
                ItemPivot.SelectedItem = DescriptionPage;
            }

            ViewModelLocator.Current.NavigationService.GoBackRequested -= OnBackRequested;

            await HidePlayerControlAsync().ConfigureAwait(true);

            base.OnNavigatedFrom(e);
        }

        private async void OnBackRequested(object sender, GoBackRequestedEventArgs e)
        {
            if (!isActive
                || e.Handled)
            {
                return;
            }

            if (ImageViewer.IsOpened)
            {
                ImageViewer.Close();
                e.Handled = true;
                return;
            }

            using var _ = e.DeferralSource.GetDeferral();
            if (ItemPivot.SelectedItem == VideoPage
                && PlayerControl != null
                && await PlayerControl.HandleKeyBindingActionAsync(PlayerControlKeyBindingAction.OnGoBackRequested)
                    .ConfigureAwait(true))
            {
                e.Handled = true;
            }
            else if (FilesView.HandleBackButton()
                     && ItemPivot.SelectedItem == FilesPage)
            {
                e.Handled = true;
            }
            else if (ItemPivot.SelectedItem == VideoPage)
            {
                ItemPivot.SelectedItem = FilesPage;
                e.Handled = true;
            }
            else if (ItemPivot.SelectedItem == FilesPage)
            {
                if (FileViewModel?.GoUpCommand.CanExecute() == true)
                {
                    FileViewModel.GoUpCommand.Execute();
                    e.Handled = true;
                }
                else if (ItemPivot.Items.Contains(DescriptionPage))
                {
                    ItemPivot.SelectedItem = DescriptionPage;
                    e.Handled = true;
                }
            }
        }

        private async void ShowAddReviewDialog(object sender, RoutedEventArgs e)
        {
            if (await reviewDialog.ShowAsync(CancellationToken.None).ConfigureAwait(true) is string review
                && !string.IsNullOrEmpty(review))
            {
                await ReviewViewModel.SendReviewCommand.ExecuteAsync(review).ConfigureAwait(true);
            }
        }

        private async void InNewWindowButton_Click(object sender, RoutedEventArgs e)
        {
            await HidePlayerControlAsync().ConfigureAwait(true);

            var result = await Pages.VideoPage.VideoWindow.ShowAsync().ConfigureAwait(true);

            if (result)
            {
                Pages.VideoPage.VideoWindow.Destroyed += OnViewClosed;
            }
            else
            {
                OnViewClosed(null, null);
            }
        }

        private async void PlayerControl_PlaylistEnded(object sender, EventArgs e)
        {
            await Dispatcher.RunIdleAsync(t =>
            {
                if (!isActive)
                {
                    return;
                }

                PlayerControl.WindowMode = WindowMode.None;

                if (ItemPivot.SelectedItem == VideoPage)
                {
                    ItemPivot.SelectedItem = ItemPivot.Items.FirstOrDefault();
                }
            });
        }

        private async void OnViewClosed(object? sender, EventArgs? args)
        {
            Pages.VideoPage.VideoWindow.Destroyed -= OnViewClosed;
            if (isActive)
            {
                await ItemPivot.Dispatcher.TryRunAsync(CoreDispatcherPriority.Normal, async () =>
                {
                    await SetupPlayerControlAsync().ConfigureAwait(true);
                    ItemPivot.SelectedItem = VideoPage;
                });
            }
        }

        private async void FileManager_VideoOpened(Video video, NodeOpenWay openWay)
        {
            if (!isActive)
            {
                return;
            }

            try
            {
                await Dispatcher.CheckBeginInvokeOnUI(async () =>
                {
                    if (!Pages.VideoPage.VideoWindow.IsActive
                        && isActive)
                    {
                        switch (openWay)
                        {
                            case NodeOpenWay.InApp:
                                await SetupPlayerControlAsync().ConfigureAwait(true);
                                break;

                            case NodeOpenWay.InSeparatedWindow:
                                if (await Pages.VideoPage.VideoWindow.ShowAsync().ConfigureAwait(true))
                                {
                                    await HidePlayerControlAsync().ConfigureAwait(true);
                                }

                                break;
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                logger.LogError(ex);
            }
        }

        private async Task SetupPlayerControlAsync()
        {
            try
            {
                if (!ItemPivot.Items.Contains(VideoPage))
                {
                    ItemPivot.Items.Add(VideoPage);
                }

                FindName("PlayerControl");

                await PlayerControl.PlayAsync().ConfigureAwait(true);

                if (!isActive)
                {
                    return;
                }

                var oldPage = ItemPivot.SelectedItem;

                await Dispatcher.YieldIdle();

                if (ItemPivot.SelectedItem == VideoPage
                    || !isActive)
                {
                    return;
                }

                if (Settings.Instance.MoveToVideoPage
                    && ItemPivot.Items.Contains(VideoPage))
                {
                    ItemPivot.SelectedItem = VideoPage;
                }

                using (var source = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
                {
                    await PlayerControl.WaitForLoadedAsync(source.Token);
                }

                await Dispatcher.YieldIdle();

                if (isActive
                    && oldPage != VideoPage
                    && Settings.Instance.OpenInFullScreen)
                {
                    PlayerControl.WindowMode = WindowMode.FullScreen;
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                logger?.LogError(ex);
                await ViewModelLocator.Current.Resolve<INotificationService>()
                    .ShowAsync(Strings.PlayerControl_InitFailed, NotificationType.Error)
                    .ConfigureAwait(true);

                await HidePlayerControlAsync().ConfigureAwait(true);
            }
        }

        private async ValueTask HidePlayerControlAsync()
        {
            try
            {
                if (PlayerControl != null)
                {
                    await PlayerControl.StopAsync().ConfigureAwait(true);
                }

                if (ItemPivot.SelectedItem == VideoPage)
                {
                    ItemPivot.SelectedItem = FilesPage;
                }

                if (ItemPivot.Items.Contains(VideoPage))
                {
                    ItemPivot.Items.Remove(VideoPage);
                }

                // Ensure normal window mode on navigated from
                VisualStateManager.GoToState(this, "NormalMode", false);
            }
            catch (Exception ex)
            {
                logger?.LogError(ex);
            }
        }

        private void Pivot_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (PlayerControl != null)
            {
                if (ItemPivot.SelectedItem != VideoPage
                    && PlayerControl.WindowMode != WindowMode.None)
                {
                    PlayerControl.WindowMode = WindowMode.None;
                }
            }
        }

        private async void Pivot_Loaded(object sender, RoutedEventArgs e)
        {
            if (ItemPivot.SelectedItem is not PivotItem pivotItem)
            {
                return;
            }

            await pivotItem.TryFocusAsync(FocusState.Programmatic);
        }

        private async void OpenRemoteButton_Click(object sender, RoutedEventArgs e)
        {
            if (PlayerControl != null)
            {
                await PlayerControl.PauseAsync();
            }
        }

        private void FullWindowGroup_CurrentStateChanging(object sender, VisualStateChangedEventArgs e)
        {
            if (e.NewState?.Name == "FullWindowMode")
            {
                PivotExtension.SetHeaderVisibility(ItemPivot, Visibility.Collapsed);
            }
            else
            {
                PivotExtension.SetHeaderVisibility(ItemPivot, Visibility.Visible);
            }
        }

        private async void ShareGrabbedFrameButton_Click(object sender, RoutedEventArgs e)
        {
            if (PlayerControl?.PlayingFile is not FSClient.Shared.Models.File playingFile)
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
