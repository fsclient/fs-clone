namespace FSClient.UWP.Shared.Views.Controls
{
    using System;
    using System.Linq;

#if UNO
    using Windows.UI.Text;
#elif WINUI3
    using Microsoft.UI.Text;
#endif
#if WINUI3
    using Microsoft.UI.Xaml;
    using Microsoft.UI.Xaml.Controls;
    using Microsoft.UI.Xaml.Controls.Primitives;
    using Microsoft.UI.Xaml.Documents;
#else
    using Windows.UI.Text;
    using Windows.UI.Xaml;
    using Windows.UI.Xaml.Controls;
    using Windows.UI.Xaml.Controls.Primitives;
    using Windows.UI.Xaml.Documents;
#endif

    using FSClient.Localization.Resources;
    using FSClient.Shared.Helpers;
    using FSClient.Shared.Models;
    using FSClient.UWP.Shared.Helpers;

    using Humanizer;

    public partial class MediaHeader : Control
    {
        private readonly Run seasonTitleRun;
        private readonly Run episodeTitleRun;
        private readonly Run itemTitleRun;

        private TextBlock? titleBlock;

        public MediaHeader()
        {
            DefaultStyleKey = nameof(MediaHeader);
            seasonTitleRun = new Run() {FontWeight = FontWeights.Bold};
            episodeTitleRun = new Run() {FontWeight = FontWeights.Bold};
            itemTitleRun = new Run();
        }

        protected override void OnApplyTemplate()
        {
            titleBlock = GetTemplateChild("TitleBlock") as TextBlock;

            var groups = VisualStateManager.GetVisualStateGroups(GetTemplateChild("TopPanelGrid") as Grid);
            var windowWidthStates = groups
                .FirstOrDefault(g => g.Name == "WindowWidthStates");
            if (windowWidthStates != null)
            {
                windowWidthStates.CurrentStateChanged += WindowWidthStates_CurrentStateChanged;
            }

            if (titleBlock != null)
            {
                if (windowWidthStates?.CurrentState?.Name == "LargeState")
                {
                    titleBlock.Inlines.Add(seasonTitleRun);
                }

                titleBlock.Inlines.Add(episodeTitleRun);
                titleBlock.Inlines.Add(itemTitleRun);
            }

            VisualStateManager.GoToState(this, "NoPlaylistState", false);
            base.OnApplyTemplate();
        }

        protected override async void OnGotFocus(RoutedEventArgs e)
        {
            if (FocusState == FocusState.Keyboard
                && GetTemplateChild("PlaylistButton") is Control playlistButton
                && playlistButton.Visibility != Visibility.Collapsed
                && !((FrameworkElement)e.OriginalSource).IsChildOf(this))
            {
                await playlistButton.TryFocusAsync(FocusState.Programmatic).ConfigureAwait(true);
            }

            base.OnGotFocus(e);
        }

        private void WindowWidthStates_CurrentStateChanged(object sender, VisualStateChangedEventArgs e)
        {
            if (titleBlock == null)
            {
                return;
            }

            if (e.NewState.Name == "LargeState")
            {
                if (!titleBlock.Inlines.Contains(seasonTitleRun))
                {
                    titleBlock.Inlines.Insert(0, seasonTitleRun);
                }
            }
            else if (titleBlock.Inlines.Contains(seasonTitleRun))
            {
                titleBlock.Inlines.Remove(seasonTitleRun);
            }
        }

        private static void MoreFlyoutChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var mediaHeader = (MediaHeader)d;
            if (mediaHeader.GetTemplateChild("MoreButton") is AppBarButton button)
            {
                button.Flyout = e.NewValue as FlyoutBase;
                button.Visibility = button.Flyout != null ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        private static void CurrentFileChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var header = (MediaHeader)d;
            if (header.titleBlock == null)
            {
                return;
            }

            if (e.NewValue is File file)
            {
                if (file.Playlist.Count > 1)
                {
                    VisualStateManager.GoToState(header, "PlaylistState", false);
                }
                else
                {
                    VisualStateManager.GoToState(header, "NoPlaylistState", false);
                }

                header.titleBlock.Visibility = Visibility.Visible;

                header.seasonTitleRun.Text = file.Season is int season
                    ? Strings.File_SeasonWithNumber.FormatWith(season) + " "
                    : string.Empty;

                header.episodeTitleRun.Text = file.Episode is Range episode
                    ? Strings.File_EpisodeWithNumber.FormatWith(episode.ToFormattedString()) + " "
                    : string.Empty;

                header.itemTitleRun.Text = file.ItemTitle ?? string.Empty;
            }
            else
            {
                VisualStateManager.GoToState(header, "NoPlaylistState", false);
                header.titleBlock.Visibility = Visibility.Collapsed;
            }
        }
    }
}
