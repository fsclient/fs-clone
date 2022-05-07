namespace FSClient.UWP.Shared.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    using Windows.Devices.Input;
    using Windows.Foundation.Metadata;
    using Windows.System;
    using Windows.UI.Core;

#if WINUI3
    using Microsoft.UI.Xaml;
    using Microsoft.UI.Xaml.Controls;
    using Microsoft.UI.Xaml.Input;
    using Microsoft.UI.Xaml.Media;
    using Microsoft.UI.Xaml.Media.Animation;
    using Microsoft.UI.Xaml.Navigation;
#else
    using Windows.UI.Xaml;
    using Windows.UI.Xaml.Controls;
    using Windows.UI.Xaml.Media;
    using Windows.UI.Xaml.Media.Animation;
    using Windows.UI.Xaml.Input;
    using Windows.UI.Xaml.Navigation;
#endif

    using FSClient.Shared.Services;
    using FSClient.UWP.Shared.Helpers;

    using Microsoft.Extensions.Logging;

    using Nito.AsyncEx;
    using FSClient.Shared.Models;
    using FSClient.UWP.Shared.Views.Pages;

    public class NavigationService : IWindowsNavigationService
    {
        private List<HistoryNavigationItem> pagesHistory;
        private Lazy<Frame> frameFactory;
        private readonly ILogger logger;

#nullable disable
        public NavigationService(ILogger log)
#nullable restore
        {
            logger = log;

            ResetFrame();
        }

        public IReadOnlyCollection<HistoryNavigationItem> PagesHistory => pagesHistory;

        public bool HasAnyPage => frameFactory.IsValueCreated && (PagesHistory.Count > 0 || frameFactory.Value.CurrentSourcePageType != null);

        public Frame RootFrame => frameFactory.Value;

        public event EventHandler<GoBackRequestedEventArgs>? GoBackRequested;

        public void ResetFrame()
        {
            frameFactory = new Lazy<Frame>(() =>
            {
                var rootFrame = new Frame
                {
                    ContentTransitions = new TransitionCollection {new NavigationThemeTransition()}
                };

                rootFrame.Loaded += RootFrame_Loaded;
                return rootFrame;
            });

            pagesHistory = new List<HistoryNavigationItem>();
        }

        public async Task<bool> GoBack(bool exitAllowed)
        {
            if (RootFrame == null)
            {
                return false;
            }

            var deferralManager = new DeferralManager();
            var args = new GoBackRequestedEventArgs(deferralManager.DeferralSource);
            GoBackRequested?.Invoke(this, args);
            await deferralManager.WaitForDeferralsAsync();

            if (args.Handled)
            {
                return true;
            }

            if (RootFrame.CanGoBack)
            {
                RootFrame.GoBack();
                args.Handled = true;
            }
            else if (RootFrame.BackStackDepth == 0)
            {
                if (UWPAppInformation.Instance.DeviceFamily == DeviceFamily.Xbox)
                {
                    return args.Handled;
                }

                if (exitAllowed)
                {
                    args.Handled = true;
                    Application.Current.Exit();
                }
            }

            return args.Handled;
        }

        public bool GoForward()
        {
            if (RootFrame?.CanGoForward != true)
            {
                return false;
            }

            RootFrame.GoForward();
            return true;
        }

        public bool Navigate<TPage>(object? parameter = null) where TPage : Page
        {
            return Navigate(typeof(TPage), parameter);
        }

        public bool Navigate(NavigationPageType pageType, object? parameter = null)
        {
            return Navigate(GetTypeFromNavigationPageType(pageType, parameter != null), parameter);
        }

        public bool Navigate(Type pageType, object? parameter = null)
        {
            if (RootFrame == null)
            {
                return false;
            }

            if (PagesHistory.LastOrDefault() is NavigationItem lastItem
                && lastItem.Type?.Name == pageType.Name
                && lastItem.Parameter == parameter)
            {
                return false;
            }

            return RootFrame.Navigate(pageType, parameter, new DrillInNavigationTransitionInfo());
        }

        private void RootFrame_Navigated(object sender, NavigationEventArgs e)
        {
            var item = new HistoryNavigationItem(e.SourcePageType.Name, e.SourcePageType)
            {
                Parameter = e.Parameter, Time = DateTimeOffset.Now, Mode = e.NavigationMode
            };

            if (e.NavigationMode == NavigationMode.New)
            {
                logger.Log(LogLevel.Information, default, item, null, (i, _) => "Navigation");
            }

            if (pagesHistory.Count > 100)
            {
                pagesHistory.RemoveRange(0, 10);
            }

            pagesHistory.Add(item);
        }

        private void RootFrame_Loaded(object sender, RoutedEventArgs args)
        {
            RootFrame.Loaded -= RootFrame_Loaded;

            RootFrame.Navigated += RootFrame_Navigated;

            var rootControl = (Control)RootFrame;
            var currentDepObj = (DependencyObject)RootFrame;
            while (currentDepObj != null)
            {
                currentDepObj = VisualTreeHelper.GetParent(currentDepObj);
                if (currentDepObj is Control control)
                {
                    rootControl = control;
                }
            }

            if (new MouseCapabilities().MousePresent > 0)
            {
                rootControl.PointerPressed += RootFrame_PointerPressed;
            }

            if (new KeyboardCapabilities().KeyboardPresent > 0)
            {
                if (ApiInformation.IsEventPresent(typeof(UIElement).FullName, nameof(UIElement.PreviewKeyDown)))
                {
                    rootControl.PreviewKeyDown += RootControl_PreviewKeyDown;
                }

                rootControl.KeyDown += RootControl_KeyDown;
            }

            if (rootControl != RootFrame)
            {
                rootControl.GotFocus += RootControl_GotFocus;
            }

            rootControl.Unloaded += RootControl_Unloaded;
        }

        private void RootControl_Unloaded(object sender, RoutedEventArgs e)
        {
            var rootControl = (Control)sender;
            rootControl.Unloaded -= RootControl_Unloaded;
            rootControl.PointerPressed -= RootFrame_PointerPressed;
            rootControl.GotFocus -= RootControl_GotFocus;
            rootControl.KeyDown -= RootControl_KeyDown;
            if (ApiInformation.IsEventPresent(typeof(UIElement).FullName, nameof(UIElement.PreviewKeyDown)))
            {
                rootControl.PreviewKeyDown -= RootControl_PreviewKeyDown;
            }
        }

        private void RootControl_GotFocus(object sender, RoutedEventArgs a)
        {
            if (a.OriginalSource == sender)
            {
                RootFrame.Focus(FocusState.Programmatic);
            }
        }

        private async void RootControl_PreviewKeyDown(object sender, KeyRoutedEventArgs args)
        {
            if (args.Handled)
            {
                return;
            }

            // Left and Right isn't fired from KeyDown event if Pivot is focused
            switch (args.Key)
            {
                case VirtualKey.Left when IsAltPressed():
                    args.Handled = await GoBack(false);
                    break;
                case VirtualKey.Right when IsAltPressed():
                    args.Handled = GoForward();
                    break;
            }
        }

        private async void RootControl_KeyDown(object sender, KeyRoutedEventArgs args)
        {
            if (args.Handled)
            {
                return;
            }

            switch (args.Key)
            {
                case VirtualKey.Back when IsValidSource(args.OriginalSource):
                    if ((args.OriginalSource as FrameworkElement)?.FindVisualAscendant<ContentDialog>() is { } dialog)
                    {
                        dialog.Hide();
                        args.Handled = true;
                    }
                    else
                    {
                        args.Handled = await GoBack(false);
                    }

                    break;
                case VirtualKey.GoBack:
                    args.Handled = await GoBack(false);
                    break;
                case VirtualKey.GoForward:
                    args.Handled = GoForward();
                    break;
            }
        }

        private async void RootFrame_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            if (e.Pointer.PointerDeviceType == PointerDeviceType.Mouse)
            {
                var properties = e.GetCurrentPoint(RootFrame).Properties;
                if (properties.IsXButton1Pressed)
                {
                    e.Handled = await GoBack(true);
                }
                else if (properties.IsXButton2Pressed)
                {
                    e.Handled = GoForward();
                }
            }
        }

        private static bool IsAltPressed()
        {
            return CoreWindow.GetForCurrentThread().GetKeyState(VirtualKey.Menu).HasFlag(CoreVirtualKeyStates.Down);
        }

        private static bool IsValidSource(object source)
        {
            return !(source is TextBox || source is PasswordBox || source is RichEditBox);
        }

        private static Type GetTypeFromNavigationPageType(NavigationPageType pageType, bool hasParameter)
        {
            return pageType switch
            {
                NavigationPageType.Home => typeof(HomePage),
                NavigationPageType.Search => typeof(SearchPage),
                NavigationPageType.Favorites => typeof(FavoritesPage),
                NavigationPageType.History => typeof(HistoryPage),
                NavigationPageType.LastWatched => typeof(ItemPage),
                NavigationPageType.Settings => typeof(SettingsPage),
                NavigationPageType.Downloads => typeof(DownloadsPage),
                NavigationPageType.ItemInfo when hasParameter => typeof(ItemPage),
                NavigationPageType.Files when hasParameter => typeof(ItemPage),
                NavigationPageType.Video when hasParameter => typeof(ItemPage),
                NavigationPageType.ItemsByTag when hasParameter => typeof(ItemsByTagPage),                
                _ => typeof(HomePage),
            };
        }
    }
}
