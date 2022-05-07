namespace FSClient.UWP.Shared.Views.Controls
{
    using System;
    using System.Collections.ObjectModel;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

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
#endif

    using FSClient.Shared.Services;
    using FSClient.UWP.Shared.Helpers;
    using FSClient.UWP.Shared.Services;

    public class NotificationItem
    {
        public IconElement? Icon { get; set; }
        public string? Text { get; set; }
        public DateTime AppearTime { get; set; }
        public bool IsFatal { get; set; }
        public CancellationToken CancellationToken { get; set; }
    }

    public sealed partial class InAppNotificationControl : UserControl
    {
        private readonly DispatcherTimer hideTimer;

        public InAppNotificationControl()
        {
            InitializeComponent();

            Items = new ObservableCollection<NotificationItem>();

            hideTimer = new DispatcherTimer();
            hideTimer.Interval = TimeSpan.FromMilliseconds(500);
            hideTimer.Tick += HideNotification;
        }

        public bool IsXbox => UWPAppInformation.Instance.DeviceFamily == DeviceFamily.Xbox &&
                              UWPAppInformation.Instance.IsXYModeEnabled;

        public ObservableCollection<NotificationItem> Items { get; }

        public Task ShowAsync(string text, NotificationType eventType, CancellationToken cancellationToken)
        {
            return Dispatcher?.CheckBeginInvokeOnUI(() =>
            {
                IconElement? icon;

                switch (eventType)
                {
                    case NotificationType.Debug:
                    case NotificationType.Warning:
                        icon = new FontIcon {Glyph = "", FontFamily = new FontFamily("Segoe MDL2 Assets")};
                        break;
                    case NotificationType.Error:
                    case NotificationType.Fatal:
                        icon = new FontIcon {Glyph = "", FontFamily = new FontFamily("Segoe MDL2 Assets")};
                        break;
                    case NotificationType.Information:
                        icon = new FontIcon {Glyph = "", FontFamily = new FontFamily("Segoe MDL2 Assets")};
                        break;
                    case NotificationType.Completed:
                        icon = new FontIcon {Glyph = "", FontFamily = new FontFamily("Segoe MDL2 Assets")};
                        break;
                    case NotificationType.Help:
                        icon = new FontIcon {Glyph = "", FontFamily = new FontFamily("Segoe MDL2 Assets")};
                        break;
                    default:
                        icon = null;
                        break;
                }

                return ShowAsync(text, icon, eventType == NotificationType.Fatal, cancellationToken);
            }) ?? Task.FromResult(false);
        }

        public Task ShowAsync(string text, IconElement? icon, bool isFatal, CancellationToken cancellationToken)
        {
            return Dispatcher?.CheckBeginInvokeOnUI(() =>
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                text = text.Trim();

                if (Items.FirstOrDefault(i => i.Text == text) != null)
                {
                    return;
                }

                if (Items.Count > 10)
                {
                    Items.RemoveAt(0);
                    Items.RemoveAt(1);
                }

                Items.Add(new NotificationItem
                {
                    Text = text,
                    Icon = icon,
                    AppearTime = DateTime.Now,
                    IsFatal = isFatal,
                    CancellationToken = cancellationToken
                });

                if (!hideTimer.IsEnabled)
                {
                    hideTimer.Start();
                }
            }).AsTask() ?? Task.FromResult(false);
        }

        private void HideNotification(object sender, object e)
        {
            foreach (var item in Items.ToArray())
            {
                if ((DateTime.Now - item.AppearTime) > TimeSpan.FromSeconds(4)
                    || item.CancellationToken.IsCancellationRequested)
                {
                    Items.Remove(item);
                }
            }

            if (Items.Count == 0)
            {
                hideTimer.Stop();
            }
        }

        private void HideNotificationButton_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button?.DataContext is NotificationItem item)
            {
                Items.Remove(item);
            }
        }

        private void NotificationGrid_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is not Grid grid)
            {
                return;
            }

            if (!IsXbox)
            {
                return;
            }

            var button = grid.FindVisualChild<Button>("CloseButton");
            if (button == null)
            {
                return;
            }

            button.Visibility = Visibility.Collapsed;
        }

        private void NotificationGrid_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            var grid = (Grid)sender;
            if (grid.DataContext is NotificationItem item)
            {
                item.AppearTime = DateTime.Now;
            }
        }
    }
}
