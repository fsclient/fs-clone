namespace FSClient.UWP.Shared.Views.Pages
{
    using System.ComponentModel;
    using System.Threading;
    using System.Threading.Tasks;

    using Windows.Foundation.Metadata;
    using Windows.System;

#if WINUI3
    using Microsoft.UI.Xaml;
    using Microsoft.UI.Xaml.Input;
    using Microsoft.UI.Xaml.Controls;
#else
    using Windows.UI.Xaml;
    using Windows.UI.Xaml.Input;
    using Windows.UI.Xaml.Controls;
#endif

    using FSClient.Shared.Models;
    using FSClient.Shared.Services;
    using FSClient.UWP.Shared.Helpers;
    using FSClient.UWP.Shared.Services;
    using FSClient.UWP.Shared.Views.Controls;
    using FSClient.ViewModels;

    public sealed partial class FilesView : UserControl
    {
        private static readonly bool losingFocusAvailable =
            ApiInformation.IsEventPresent(typeof(UIElement).FullName, nameof(LosingFocus));

        public static readonly DependencyProperty CommandBarVisibilityProperty =
            DependencyProperty.Register(nameof(CommandBarVisibility), typeof(Visibility), typeof(FilesView),
                new PropertyMetadata(Visibility.Collapsed));

        public FilesView()
        {
            Application.Current.Resources["FileViewModel"] = FileViewModel =
                ViewModelLocator.Current.ResolveViewModel<FileViewModel>();

            InitializeComponent();

            FileViewModel.PropertyChanged += FilesViewModel_PropertyChanged;
        }

        public Visibility CommandBarVisibility
        {
            get => (Visibility)GetValue(CommandBarVisibilityProperty);
            set => SetValue(CommandBarVisibilityProperty, value);
        }

        public FileViewModel FileViewModel { get; }

        public bool HandleBackButton()
        {
            if (FilesSplitView.IsPaneOpen)
            {
                FilesSplitView.IsPaneOpen = false;
                return true;
            }

            return false;
        }

        private async void FilesViewModel_PropertyChanged(object _, PropertyChangedEventArgs args)
        {
            if (args.PropertyName == nameof(FileViewModel.SelectedNode)
                && WasInFocusedState()
                && FileViewModel.SelectedNode is ITreeNode node)
            {
                var result = await FilesGridView.ScrollAndFocusItemAsync(node).ConfigureAwait(true);
                if (!result)
                {
                    FolderHeaderControl.Focus(FocusState.Programmatic);
                }

                if (FilesGridView.SelectionMode == ListViewSelectionMode.Single)
                {
                    FilesGridView.SelectedItem = node;
                }
            }
        }

        private async void FileClick(object _, ItemClickEventArgs e)
        {
            if (e.ClickedItem is ITreeNode node)
            {
                if (WasInFocusedState())
                {
                    FolderHeaderControl.Focus(FocusState.Programmatic);
                }

                await FileViewModel.OpenCommand.ExecuteAsync(node).ConfigureAwait(true);
            }
        }

        private bool WasInFocusedState()
        {
            return FocusManager.GetFocusedElement() is Control focusedElement
                   && (focusedElement.FocusState == FocusState.Keyboard
                       || UWPAppInformation.Instance.DeviceFamily == DeviceFamily.Xbox);
        }

        private void FilesSplitView_GotFocus(object _, RoutedEventArgs e)
        {
            if (!FilesSplitView.IsPaneOpen
                && e.OriginalSource is FrameworkElement element
                && element.FindVisualAscendant<Grid>("FilesSplitViewContentGrid") == null)
            {
                FilesGridView.Focus(FilesSplitView.FocusState);
            }
        }

        private async void VideoListFlyoutOpening(object sender, object _)
        {
            if (((ListFlyout)sender).Tag is File file)
            {
                await file.PreloadAsync(CancellationToken.None).ConfigureAwait(false);
                await file.PreloadVideosSizeAsync(CancellationToken.None).ConfigureAwait(false);
            }
        }

        private void UserControl_Loaded(object _, object __)
        {
            if (losingFocusAvailable)
            {
                FilesSplitViewGrid.LosingFocus += FilesSplitViewGrid_LosingFocus;
            }

            KeyDown += FilesView_KeyDown;
        }

        private void UserControl_Unloaded(object _, object __)
        {
            if (losingFocusAvailable)
            {
                FilesSplitViewGrid.LosingFocus -= FilesSplitViewGrid_LosingFocus;
            }

            KeyDown -= FilesView_KeyDown;
        }

        private async void FilesSplitViewGrid_LosingFocus(UIElement sender, LosingFocusEventArgs args)
        {
            var lostToChild =
                ((FrameworkElement)args.NewFocusedElement).IsChildOf((FrameworkElement)sender, isDirect: false);
            if (!lostToChild)
            {
                // Wait for losing focus completed
                await Task.Yield();

                FilesSplitView.IsPaneOpen = false;
            }
        }

        private async void FilesView_KeyDown(object sender, KeyRoutedEventArgs args)
        {
            switch (args.Key)
            {
                case VirtualKey.GamepadView:
                    if (FilesSplitView.IsPaneOpen)
                    {
                        FilesSplitView.IsPaneOpen = false;
                        args.Handled = FilesGridView.Focus(FilesSplitView.FocusState);
                    }
                    else
                    {
                        FilesSplitView.IsPaneOpen = true;
                        args.Handled = true;
                        // Wait for Pane Opened
                        await Task.Yield();

                        FilesSplitViewGrid.FindVisualChild<Control>()?.Focus(FocusState.Programmatic);
                    }

                    break;
                case VirtualKey.F5:
                    args.Handled = true;
                    await FileViewModel.RefreshFolderCommand.ExecuteAsync().ConfigureAwait(true);
                    break;
            }
        }
    }
}
