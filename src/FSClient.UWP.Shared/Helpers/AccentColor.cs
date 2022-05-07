namespace FSClient.UWP.Shared.Helpers
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;

    using Windows.UI;
    using Windows.UI.ViewManagement;

#if WINUI3
    using Microsoft.UI;
    using Microsoft.UI.Xaml;
    using Microsoft.UI.Xaml.Media;
    using Colors = Microsoft.UI.Colors;
#else
    using Windows.UI.Xaml;
    using Windows.UI.Xaml.Media;
#endif

    using FSClient.Shared;
    using FSClient.Shared.Services;
    using FSClient.UWP.Shared.Services;
    public class AccentColor : FrameworkElement
    {
        public static readonly DependencyProperty IsSystemColorProperty =
            DependencyProperty.Register("IsSystemColor", typeof(bool), typeof(AccentColor),
                PropertyMetadata.Create(() => GetDefaultIsSystemColorProperty(), IsSystemColorChangedCallback));

        public static readonly DependencyProperty ColorProperty =
            DependencyProperty.Register("Color", typeof(Color), typeof(AccentColor),
                PropertyMetadata.Create(() => GetDefaultColor(), PropertyChangedCallback));

        private readonly UISettings uiSettings;

        private Color DefaultSystemAccentColor => uiSettings.GetColorValue(UIColorType.Accent);

        private const string AccentForegroundBrushKey = "AccentForegroundBrush";

        private const string SystemAccentColorKey = "SystemAccentColor";

        private static readonly List<string> BrushKeysList = new List<string>
        {
            "SystemControlBackgroundAccentBrush",
            "SystemControlDisabledAccentBrush",
            "SystemControlForegroundAccentBrush",
            "SystemControlHighlightAccentBrush",
            "SystemControlHighlightAltAccentBrush",
            "SystemControlHighlightAltListAccentHighBrush",
            "SystemControlHighlightAltListAccentLowBrush",
            "SystemControlHighlightAltListAccentMediumBrush",
            "SystemControlHighlightListAccentHighBrush",
            "SystemControlHighlightListAccentLowBrush",
            "SystemControlHighlightListAccentMediumBrush",
            "SystemControlHyperlinkTextBrush",
            "ContentDialogBorderThemeBrush",
            "JumpListDefaultEnabledBackground"
        };

        public AccentColor()
        {
            uiSettings = new UISettings();
            uiSettings.ColorValuesChanged += UiSettings_ColorValuesChanged;
        }

        public bool IsSystemColor
        {
            get => (bool)GetValue(IsSystemColorProperty);
            set => SetValue(IsSystemColorProperty, value);
        }

        public Color? UserColor
        {
            get => GetValue(ColorProperty) as Color?;
            set => SetValue(ColorProperty, value);
        }

        public IEnumerable<Color> AvailableColors { get; } = new[]
        {
            Color.FromArgb(255, 255, 140, 0), Color.FromArgb(255, 202, 80, 16), Color.FromArgb(255, 239, 106, 80),
            Color.FromArgb(255, 255, 67, 67), Color.FromArgb(255, 232, 17, 35), Color.FromArgb(255, 107, 105, 214),
            Color.FromArgb(255, 116, 77, 169), Color.FromArgb(255, 154, 0, 137), Color.FromArgb(255, 191, 0, 119),
            Color.FromArgb(255, 195, 0, 82), Color.FromArgb(255, 0, 99, 177), Color.FromArgb(255, 0, 120, 215),
            Color.FromArgb(255, 0, 153, 188), Color.FromArgb(255, 45, 125, 154), Color.FromArgb(255, 0, 178, 148),
            Color.FromArgb(255, 93, 90, 88), Color.FromArgb(255, 81, 92, 107), Color.FromArgb(255, 72, 104, 96),
            Color.FromArgb(255, 16, 137, 62), Color.FromArgb(255, 1, 133, 116)
        };

        public void Setup()
        {
            try
            {
                var color = !IsSystemColor && UserColor.HasValue
                    ? UserColor.Value
                    : DefaultSystemAccentColor;

                Application.Current.Resources[SystemAccentColorKey] = color;
                foreach (var key in BrushKeysList)
                {
                    if (Application.Current.Resources.TryGetValue(key, out var resource)
                        && resource is SolidColorBrush brush)
                    {
                        brush.Color = color;
                    }
                }

                InitBars();

                Application.Current.Resources[AccentForegroundBrushKey] = IsColorDark(color)
                    ? new SolidColorBrush(Colors.White)
                    : new SolidColorBrush(Colors.Black);
            }
            catch (Exception ex)
            {
                Logger.Instance.LogError(ex);
            }
        }

        private async void UiSettings_ColorValuesChanged(UISettings sender, object args)
        {
            try
            {
                await Dispatcher
                    .CheckBeginInvokeOnUI(() =>
                    {
                        if (IsSystemColor)
                        {
                            Setup();
                        }
                    })
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Logger.Instance.LogWarning(ex);
            }
        }

        private void InitBars()
        {
            var color = (Color)Application.Current.Resources[SystemAccentColorKey];

            var v = ApplicationView.GetForCurrentView();
            var titleBar = v?.TitleBar;
            if (titleBar != null)
            {
                byte r = color.R, g = color.G, b = color.B;

                if (Application.Current.Resources["SystemControlHighlightListAccentHighBrush"] is SolidColorBrush
                    pressedBrush)
                {
                    r = (byte)(color.R * pressedBrush.Opacity);
                    g = (byte)(color.G * pressedBrush.Opacity);
                    b = (byte)(color.B * pressedBrush.Opacity);
                }

                var pressedColor = Color.FromArgb(255, r, g, b);

                if (Application.Current.Resources["SystemControlHighlightListAccentMediumBrush"] is SolidColorBrush
                    inactiveBrush)
                {
                    r = (byte)(color.R * inactiveBrush.Opacity);
                    g = (byte)(color.G * inactiveBrush.Opacity);
                    b = (byte)(color.B * inactiveBrush.Opacity);
                }

                var inactiveColor = Color.FromArgb(255, r, g, b);

                titleBar.ButtonBackgroundColor = color;
                titleBar.BackgroundColor = color;
                titleBar.InactiveBackgroundColor = inactiveColor;
                titleBar.ButtonHoverBackgroundColor = inactiveColor;
                titleBar.ButtonInactiveBackgroundColor = inactiveColor;
                titleBar.ButtonPressedBackgroundColor = pressedColor;
            }
        }

        private static void PropertyChangedCallback(DependencyObject dependencyObject,
            DependencyPropertyChangedEventArgs args)
        {
            ViewModelLocator.Current.Resolve<ISettingService>().SetSetting(Settings.UserSettingsContainer,
                "AccentColor", args.NewValue.ToString());
            (dependencyObject as AccentColor)?.Setup();
        }

        private static void IsSystemColorChangedCallback(DependencyObject dependencyObject,
            DependencyPropertyChangedEventArgs args)
        {
            ViewModelLocator.Current.Resolve<ISettingService>().SetSetting(Settings.UserSettingsContainer,
                "IsSystemColor", (bool)args.NewValue);
            (dependencyObject as AccentColor)?.Setup();
        }

        private static object GetDefaultIsSystemColorProperty()
        {
            return ViewModelLocator.Current.Resolve<ISettingService>()
                .GetSetting(Settings.UserSettingsContainer, "IsSystemColor", true);
        }

        private static Color GetDefaultColor()
        {
            var colorStr = ViewModelLocator.Current.Resolve<ISettingService>()
                .GetSetting(Settings.UserSettingsContainer, "AccentColor", null);
            return FromString(colorStr) ?? Color.FromArgb(255, 1, 133, 116);
        }

        private static bool IsColorDark(Color color)
        {
            return ((5 * color.G) + (2 * color.R) + color.B) <= 8 * 128;
        }

        private static Color? FromString(string? s)
        {
            if (s?[0] != '#')
            {
                return null;
            }

            s = s[1..];

            var colorInt = int.Parse(s, NumberStyles.HexNumber);

            var a = s.Length == 8
                ? (byte)((colorInt >> 24) & 0xFF)
                : (byte)255;
            var r = (byte)((colorInt >> 16) & 0xFF);
            var g = (byte)((colorInt >> 8) & 0xFF);
            var b = (byte)((colorInt >> 0) & 0xFF);

            return Color.FromArgb(a, r, g, b);
        }
    }
}
