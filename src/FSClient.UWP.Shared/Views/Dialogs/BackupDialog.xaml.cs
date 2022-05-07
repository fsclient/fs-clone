namespace FSClient.UWP.Shared.Views.Dialogs
{
    using System.Threading;
    using System.Threading.Tasks;

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

    public sealed partial class BackupDialog : ContentDialog, IContentDialog<BackupDialogInput, BackupDialogOutput>
    {
        private int captionTapCount;

        public BackupDialog()
        {
            InitializeComponent();
        }

        public BackupDialogInput? DialogInput { get; private set; }

        Task<BackupDialogOutput> IContentDialog<BackupDialogInput, BackupDialogOutput>.ShowAsync(
            BackupDialogInput arg, CancellationToken cancellationToken)
        {
            DialogInput = arg;
            return Dispatcher.CheckBeginInvokeOnUI(async () =>
            {
                captionTapCount = 0;
                UserSettingsCheckBox.IsEnabled = arg.AllowedTypes.HasFlag(BackupDataTypes.UserSettings);
                StateSettingsCheckBox.IsEnabled = arg.AllowedTypes.HasFlag(BackupDataTypes.StateSettings);
                InternalSettingsCheckBox.IsEnabled = arg.AllowedTypes.HasFlag(BackupDataTypes.InternalSettings);
                HistoryCheckBox.IsEnabled = arg.AllowedTypes.HasFlag(BackupDataTypes.History);
                FavoritesCheckBox.IsEnabled = arg.AllowedTypes.HasFlag(BackupDataTypes.Favorites);

                return await this.ShowAsync(cancellationToken).ConfigureAwait(true) switch
                {
                    ContentDialogResult.Primary => new BackupDialogOutput(BackupDataTypes.None
                                                                          | (UserSettingsCheckBox.IsChecked == true
                                                                              ? BackupDataTypes.UserSettings
                                                                              : BackupDataTypes.None)
                                                                          | (StateSettingsCheckBox.IsChecked == true
                                                                              ? BackupDataTypes.StateSettings
                                                                              : BackupDataTypes.None)
                                                                          | (InternalSettingsCheckBox.IsChecked == true
                                                                              ? BackupDataTypes.InternalSettings
                                                                              : BackupDataTypes.None)
                                                                          | (HistoryCheckBox.IsChecked == true
                                                                              ? BackupDataTypes.History
                                                                              : BackupDataTypes.None)
                                                                          | (FavoritesCheckBox.IsChecked == true
                                                                              ? BackupDataTypes.Favorites
                                                                              : BackupDataTypes.None)),
                    _ => new BackupDialogOutput(BackupDataTypes.None),
                };
            });
        }

        private void ContentDialog_Tapped(object sender, TappedRoutedEventArgs e)
        {
            if ((e.OriginalSource as TextBlock)?.Text == DialogInput?.Caption
                && captionTapCount++ > 6)
            {
                InternalSettingsCheckBox.Visibility = Visibility.Visible;
            }
        }
    }
}
