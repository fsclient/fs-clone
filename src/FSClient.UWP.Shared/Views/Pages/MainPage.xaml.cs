namespace FSClient.UWP.Shared.Views.Pages
{
    using System;
    using System.Collections.ObjectModel;
    using System.ComponentModel;
    using System.Linq;
    using System.Threading.Tasks;

    using Windows.System;
    using Windows.UI.Core;
#if WINUI3
    using Microsoft.UI.Xaml;
    using Microsoft.UI.Xaml.Media;
    using Microsoft.UI.Xaml.Input;
    using Microsoft.UI.Xaml.Controls;
#else
    using Windows.UI.Xaml;
    using Windows.UI.Xaml.Media;
    using Windows.UI.Xaml.Input;
    using Windows.UI.Xaml.Controls;
    using NavigationViewDisplayMode = Microsoft.UI.Xaml.Controls.NavigationViewDisplayMode;
    using NavigationViewDisplayModeChangedEventArgs = Microsoft.UI.Xaml.Controls.NavigationViewDisplayModeChangedEventArgs;
    using NavigationView = Microsoft.UI.Xaml.Controls.NavigationView;
    using NavigationViewItem = Microsoft.UI.Xaml.Controls.NavigationViewItem;
    using NavigationViewItemInvokedEventArgs = Microsoft.UI.Xaml.Controls.NavigationViewItemInvokedEventArgs;
#endif

    using FSClient.Localization.Resources;
    using FSClient.Shared;
    using FSClient.Shared.Services;
    using FSClient.UWP.Shared.Extensions;
    using FSClient.UWP.Shared.Services;
    using FSClient.UWP.Shared.Helpers;

    public partial class MainPage
    {
        private readonly NavigationViewItem hiddenButton;
        private readonly SystemNavigationManager manager;
        private readonly Frame frame;

        private bool? isExpandedPaneOpen;

        public MainPage()
        {
            var oldStyle = Application.Current.Resources["NavigationViewTitleHeaderContentControlTextStyle"] as Style;
            Application.Current.Resources["NavigationViewTitleHeaderContentControlTextStyle"] = new Style
            {
                BasedOn = oldStyle,
                TargetType = typeof(ContentControl),
                Setters = {new Setter(FontSizeProperty, 16), new Setter(MarginProperty, new Thickness()),}
            };

            frame = ViewModelLocator.Current.NavigationService.RootFrame;
            hiddenButton = new NavigationViewItem {Visibility = Visibility.Collapsed};

            Items = new ObservableCollection<NavigationViewItem>
            {
                CreateNavigationViewItem<HomePage>(Strings.NavigationPageType_Home, Symbol.Home, VirtualKey.M),
                CreateNavigationViewItem<SearchPage>(Strings.NavigationPageType_Search, Symbol.Find, VirtualKey.S),
                CreateNavigationViewItem<FavoritesPage>(Strings.NavigationPageType_Favorites, Symbol.Favorite,
                    VirtualKey.F),
                CreateNavigationViewItem<HistoryPage>(Strings.NavigationPageType_History, Symbol.Clock, VirtualKey.H),
                CreateNavigationViewItem<DownloadsPage>(Strings.NavigationPageType_Downloads, Symbol.Download,
                    VirtualKey.D),
                hiddenButton
            };

            InitializeComponent();

            manager = SystemNavigationManager.GetForCurrentView();
            manager.BackRequested += System_BackRequested;

            EnsureApplicationMargins();
            FrameNavigated(frame);

            frame.Navigated += (s, e) =>
            {
                UpdatePane(((Frame)s).CurrentSourcePageType);
                NavigationView.Header = PageAppBarExtension.GetTop((DependencyObject)e.Content);

                FrameNavigated((Frame)s);
            };

            Loaded += (s, e) => UpdatePane(frame.CurrentSourcePageType);

            Settings.Instance.PropertyChanged += Settings_PropertyChanged;
            ContentGrid.Content = frame;
        }

        public Settings Settings => Settings.Instance;

        public ObservableCollection<NavigationViewItem> Items { get; }

        public bool IsExpandedPaneOpen
        {
            get => isExpandedPaneOpen ?? (isExpandedPaneOpen = ViewModelLocator.Current.Resolve<ISettingService>()
                .GetSetting(Settings.StateSettingsContainer, "IsPaneOpen", NavigationView.IsPaneOpen)).Value;
            set => ViewModelLocator.Current.Resolve<ISettingService>()
                .SetSetting(Settings.StateSettingsContainer, "IsPaneOpen", isExpandedPaneOpen = value);
        }

        private void FrameNavigated(Frame frame)
        {
            if (NavigationView.IsBackButtonVisible ==
                Microsoft.UI.Xaml.Controls.NavigationViewBackButtonVisible.Visible)
            {
                NavigationView.IsBackEnabled = frame.CanGoBack;
            }
            else
            {
                manager.AppViewBackButtonVisibility =
                    frame.CanGoBack
                        ? AppViewBackButtonVisibility.Visible
                        : AppViewBackButtonVisibility.Collapsed;
            }
        }

        private void Settings_PropertyChanged(object sender, PropertyChangedEventArgs args)
        {
            switch (args.PropertyName)
            {
                case nameof(Settings.Instance.ApplicationMarginLeft):
                case nameof(Settings.Instance.ApplicationMarginTop):
                case nameof(Settings.Instance.ApplicationMarginRight):
                case nameof(Settings.Instance.ApplicationMarginBottom):
                    EnsureApplicationMargins();
                    break;
            }
        }

        private void EnsureApplicationMargins()
        {
            NavigationView.Margin = new Thickness(
                Settings.Instance.ApplicationMarginLeft,
                Settings.Instance.ApplicationMarginTop,
                Settings.Instance.ApplicationMarginRight,
                Settings.Instance.ApplicationMarginBottom);

            var height = (Application.Current.Resources["AppNavigationViewHeaderHeight"] as double? ?? 0) + 1;
            TopBorder.Height = Math.Max(0, Settings.Instance.ApplicationMarginTop + height);
        }

        public async void NavigationView_BackRequested(NavigationView sender, object args)
        {
            await ViewModelLocator.Current.NavigationService.GoBack(true);
        }

        private async void System_BackRequested(object sender, BackRequestedEventArgs e)
        {
            e.Handled = await ViewModelLocator.Current.NavigationService.GoBack(true);
        }

        private NavigationViewItem CreateNavigationViewItem<TPage>(string title, Symbol icon, VirtualKey key)
            where TPage : Page
        {
            var navItem = new NavigationViewItem
            {
                Content = new NavigationItem<TPage>(title), Tag = typeof(TPage), Icon = new SymbolIcon(icon)
            };

            if (key != VirtualKey.None
                && CompatExtension.KeyboardAcceleratorsAvailable)
            {
                var keyAccelerator = new KeyboardAccelerator
                {
                    IsEnabled = true, Modifiers = VirtualKeyModifiers.Control | VirtualKeyModifiers.Shift, Key = key
                };
                navItem.KeyboardAccelerators.Add(keyAccelerator);
                ToolTipService.SetToolTip(navItem, $"{title} (Ctrl+Shift+{keyAccelerator.Key})");
            }

            return navItem;
        }

        private void UpdatePane(Type current)
        {
            if (current != null
                && current.FullName == typeof(SettingsPage).FullName)
            {
                NavigationView.SelectedItem = NavigationView.SettingsItem;
            }
            else if (current != null
                     && Items.FirstOrDefault(i => (i.Tag as Type)?.FullName == current.FullName) is NavigationViewItem
                         item)
            {
                NavigationView.SelectedItem = item;
            }
            else
            {
                NavigationView.SelectedItem = hiddenButton;
            }
        }

        public void UIElement_OnManipulationCompleted(object sender, ManipulationCompletedRoutedEventArgs e)
        {
            if (e.Cumulative.Translation.X > 48)
            {
                NavigationView.IsPaneOpen = true;
                e.Handled = true;
            }
            else
            {
                e.Handled = false;
            }
        }

        private void Page_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            switch (e.Key)
            {
                case VirtualKey.GamepadView:
                {
                    if (NavigationView.IsPaneOpen)
                    {
                        NavigationView.IsPaneOpen = false;
                        ((Frame)ContentGrid.Content).Focus(FocusState.Programmatic);
                    }
                    else
                    {
                        NavigationView.IsPaneOpen = true;
                        NavigationView.Focus(FocusState.Programmatic);
                    }

                    e.Handled = true;
                    break;
                }
            }
        }

        private void NavigationView_ItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
        {
            if (args.IsSettingsInvoked)
            {
                ViewModelLocator.Current.NavigationService.Navigate<SettingsPage>();
            }
            else if ((args.InvokedItem as NavigationItem)?.Type is Type type)
            {
                ViewModelLocator.Current.NavigationService.Navigate(type);
            }
        }

        private void NavigationView_Loaded(object sender, RoutedEventArgs e)
        {
            if (NavigationView.FindVisualChild<Grid>("PaneContentGrid") is Grid pageRootGrid)
            {
                var border = new Border
                {
                    Background = Application.Current.Resources["TopBarBackgroundBrush"] as Brush,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    VerticalAlignment = VerticalAlignment.Top,
                    Height = (double)Application.Current.Resources["AppNavigationViewHeaderHeight"]
                };
                Grid.SetRowSpan(border, pageRootGrid.RowDefinitions.Count);
                pageRootGrid.Children.Add(border);
            }
#if UAP10_0_14393
            // Fix for rs2 special winui styles
            if (NavigationView.FindVisualChild<Grid>("RootGrid") is Grid rootGrid
                && VisualStateManager.GetVisualStateGroups(rootGrid).FirstOrDefault(g => g.Name == "DisplayModeGroup") is VisualStateGroup displayModeGroup
                && displayModeGroup.States.FirstOrDefault(s => s.Name == nameof(NavigationViewDisplayMode.Minimal)) is VisualState minimalState
                && minimalState.Setters.FirstOrDefault(s => (s as Setter)?.Target.Path.Path == nameof(Margin)) is Setter marginSetter
                && marginSetter.Value is Thickness marginValue)
            {
                marginSetter.Value = new Thickness(marginValue.Left, 0, marginValue.Right, marginValue.Bottom);
            }
#endif
        }

        public void NavigationView_PaneToggled(NavigationView sender, object args)
        {
            if (sender.DisplayMode == NavigationViewDisplayMode.Expanded)
            {
                IsExpandedPaneOpen = sender.IsPaneOpen;
            }
        }

        private async void NavigationView_DisplayModeChanged(NavigationView sender,
            NavigationViewDisplayModeChangedEventArgs args)
        {
            if (args.DisplayMode == NavigationViewDisplayMode.Expanded)
            {
                // DisplayModeChanged is called first, so we need to allow PaneToggled fire first and override value
                var newValue = IsExpandedPaneOpen;
                await Task.Yield();
                sender.IsPaneOpen = newValue;
            }
        }
    }
}
