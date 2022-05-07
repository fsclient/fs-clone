namespace FSClient.UWP.Shared.Views.Controls
{
    using System;

    using Windows.ApplicationModel.Core;
    using Windows.Foundation;
    using Windows.Foundation.Metadata;
    using Windows.UI.Core;
    using Windows.UI.ViewManagement;

    using FSClient.Shared;
    using FSClient.Shared.Services;

    public partial class ManagedWindow
    {
        public static readonly bool OverlaySupported =
            ApiInformation.IsMethodPresent(typeof(ApplicationView).FullName,
                nameof(ApplicationView.IsViewModeSupported))
            && ApplicationView.GetForCurrentView().IsViewModeSupported(ApplicationViewMode.CompactOverlay);

        public static CoreDispatcher? CurrentWindowDispather
            => CoreApplication.GetCurrentView()?.CoreWindow.Dispatcher;

        protected ApplicationView? ApplicationView { get; private set; }

        public WindowMode WindowMode { get; private set; }

        public bool IsActive { get; private set; }

        public bool IsMainWindow => CoreApplication.GetCurrentView() == CoreApplication.MainView;

        public string Title
        {
            get => ApplicationView?.Title ?? throw new InvalidOperationException("Window is not inited yet");
            set
            {
                if (ApplicationView == null)
                {
                    throw new InvalidOperationException("Window is not inited yet");
                }

                ApplicationView.Title = value;
            }
        }

        public Size? OverlaySize
        {
            get => overlaySize ?? (overlaySize = ParseSize(settingService
                .GetSetting(Settings.InternalSettingsContainer, "VideoViewOverlaySize", null,
                    SettingStrategy.Local)));
            set
            {
                if (value != overlaySize)
                {
                    overlaySize = value;
                    if (value != null)
                    {
                        settingService.SetSetting(
                            Settings.InternalSettingsContainer,
                            "VideoViewOverlaySize",
                            value.Value.Width + " " + value.Value.Height,
                            SettingStrategy.Local);
                    }
                    else
                    {
                        settingService.DeleteSetting(
                            Settings.InternalSettingsContainer,
                            "VideoViewOverlaySize",
                            SettingStrategy.Local);
                    }
                }
            }
        }

        private static Size? ParseSize(string? v)
        {
            var parts = v?.Split(' ');

            if (parts == null
                || parts.Length < 2
                || !double.TryParse(parts[0], out var width)
                || !double.TryParse(parts[1], out var height))
            {
                return null;
            }

            return new Size(width, height);
        }
    }
}
