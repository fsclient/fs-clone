namespace FSClient.UWP.Shared.Views.Dialogs
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

#if WINUI3
    using Microsoft.UI;
    using Microsoft.UI.Xaml;
    using Microsoft.UI.Xaml.Media;
    using Microsoft.UI.Xaml.Controls;
#else
    using Windows.UI;
    using Windows.UI.Xaml;
    using Windows.UI.Xaml.Media;
    using Windows.UI.Xaml.Controls;
#endif

    using FSClient.Shared;
    using FSClient.Shared.Services;
    using FSClient.UWP.Shared.Helpers;

    public sealed partial class MarginCalibrationDialog : ContentDialog, IContentDialog
    {
        public MarginCalibrationDialog()
        {
            InitializeComponent();
        }

        public Settings Settings => Settings.Instance;

        Task IContentDialog.ShowAsync(CancellationToken cancellationToken)
        {
            return Dispatcher.CheckBeginInvokeOnUI(async () =>
            {
                var rootGrid = Window.Current.Content.FindVisualChild<Grid>()
                               ?? throw new InvalidOperationException("Root Grid is missed.");

                var border = new Border
                {
                    BorderThickness = new Thickness(2),
                    BorderBrush = new SolidColorBrush(Colors.White),
                    Margin = new Thickness(
                        Settings.ApplicationMarginLeft,
                        Settings.ApplicationMarginTop,
                        Settings.ApplicationMarginRight,
                        Settings.ApplicationMarginBottom)
                };
                rootGrid.Children.Add(border);
                Settings.PropertyChanged += Settings_PropertyChanged;

                await this.ShowAsync(cancellationToken).ConfigureAwait(true);

                Settings.PropertyChanged -= Settings_PropertyChanged;
                rootGrid.Children.Remove(border);

                void Settings_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
                {
                    switch (e.PropertyName)
                    {
                        case nameof(Settings.ApplicationMarginLeft):
                        case nameof(Settings.ApplicationMarginTop):
                        case nameof(Settings.ApplicationMarginRight):
                        case nameof(Settings.ApplicationMarginBottom):
                            border.Margin = new Thickness(
                                Settings.ApplicationMarginLeft,
                                Settings.ApplicationMarginTop,
                                Settings.ApplicationMarginRight,
                                Settings.ApplicationMarginBottom);
                            break;
                    }
                }
            });
        }
    }
}
