namespace FSClient.UWP.Shared.Views.Themes
{
    using System;

    using Windows.Foundation.Metadata;

#if WINUI3
    using Microsoft.UI.Xaml;
    using Microsoft.UI.Xaml.Media;
#else
    using Windows.UI.Xaml;
    using Windows.UI.Xaml.Media;
#endif

    using FSClient.Shared.Services;

    public partial class Brushes : ResourceDictionary
    {
        public Brushes()
        {
            InitializeComponent();

            SetupAcrylicForCurrentView();
        }

        private void SetupAcrylicForCurrentView()
        {
            try
            {
                if (ApiInformation.IsTypePresent(typeof(AcrylicBrush).FullName))
                {
                    if (this["ItemBackgroundBrush"] is SolidColorBrush itemBrush)
                    {
                        this["ItemBackgroundBrush"] = new AcrylicBrush
                        {
                            BackgroundSource = AcrylicBackgroundSource.Backdrop,
                            TintColor = itemBrush.Color,
                            FallbackColor = itemBrush.Color,
                            AlwaysUseFallback = false,
                            Opacity = 0.8
                        };
                    }

                    if (this["PlayerBackgroundBrush"] is SolidColorBrush playerBrush)
                    {
                        this["PlayerBackgroundBrush"] = new AcrylicBrush
                        {
                            BackgroundSource = AcrylicBackgroundSource.Backdrop,
                            TintColor = playerBrush.Color,
                            FallbackColor = playerBrush.Color,
                            AlwaysUseFallback = false,
                            Opacity = 0.6
                        };
                    }

                    if (this["PaneBackgroundBrush"] is SolidColorBrush paneBrush)
                    {
                        this["PaneBackgroundBrush"] = new AcrylicBrush
                        {
                            BackgroundSource = AcrylicBackgroundSource.Backdrop,
                            TintColor = paneBrush.Color,
                            FallbackColor = paneBrush.Color,
                            AlwaysUseFallback = false,
                            TintOpacity = 0.9
                        };
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.LogWarning(ex);
            }
        }
    }
}
