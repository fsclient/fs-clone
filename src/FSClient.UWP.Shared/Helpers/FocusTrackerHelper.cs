namespace FSClient.UWP.Shared.Helpers
{
    using System;

    using Windows.UI.ViewManagement;

#if WINUI3
    using Microsoft.UI.Xaml;
    using Microsoft.UI.Xaml.Input;
    using Microsoft.UI.Xaml.Media;
#else
    using Windows.UI.Xaml;
    using Windows.UI.Xaml.Input;
    using Windows.UI.Xaml.Media;
#endif

    public class FocusTrackerEventArgs : EventArgs
    {
        public string? FocusedElementName { get; }
        public Type? FocusedElementType { get; }
        public string? FirstNamedParentName { get; }

        public FocusTrackerEventArgs(string? name, Type? type, string? parent)
        {
            FocusedElementName = name;
            FocusedElementType = type;
            FirstNamedParentName = parent;
        }
    }

    public class FocusTrackerHelper
    {
        private DispatcherTimer? updateTimer;

        public event EventHandler<FocusTrackerEventArgs>? FocusTracked;

        public bool IsActive
        {
            get => updateTimer?.IsEnabled ?? false;
            set
            {
                if (value == updateTimer?.IsEnabled)
                {
                    return;
                }

                if (value)
                {
                    Start();
                }
                else
                {
                    Stop();
                }
            }
        }

        public void BindToTitleBar()
        {
            var appView = ApplicationView.GetForCurrentView();
            if (appView == null)
            {
                return;
            }

            IsActive = true;
            FocusTracked += (s, a) =>
            {
                appView.Title = a.FocusedElementType?.Name
                                + " \"" + a.FocusedElementName
                                + "\" from " + a.FirstNamedParentName;
            };
        }

        private void Start()
        {
            if (updateTimer == null)
            {
                updateTimer = new DispatcherTimer();
                updateTimer.Tick += UpdateTimer_Tick;
            }

            updateTimer.Start();
        }

        private void Stop()
        {
            updateTimer?.Stop();
        }

        private void UpdateTimer_Tick(object sender, object e)
        {
            var focusedControl = FocusManager.GetFocusedElement() as FrameworkElement;

            var parentWithName = FindVisualAscendantWithName(focusedControl)?.Name ?? string.Empty;

            FocusTracked?.Invoke(this, new FocusTrackerEventArgs(
                focusedControl?.Name,
                focusedControl?.GetType(),
                parentWithName));
        }

        private FrameworkElement? FindVisualAscendantWithName(FrameworkElement? element)
        {
            if (element == null)
            {
                return null;
            }

            if (VisualTreeHelper.GetParent(element) is not FrameworkElement parent)
            {
                return null;
            }

            if (!string.IsNullOrEmpty(parent.Name))
            {
                return parent;
            }

            return FindVisualAscendantWithName(parent);
        }
    }
}
