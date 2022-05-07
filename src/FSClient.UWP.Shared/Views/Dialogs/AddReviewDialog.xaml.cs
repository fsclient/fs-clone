namespace FSClient.UWP.Shared.Views.Dialogs
{
    using System.Threading;
    using System.Threading.Tasks;

    using Windows.System;
    using Windows.UI.Core;

#if WINUI3
    using Microsoft.UI.Xaml.Input;
    using Microsoft.UI.Xaml.Controls;
#else
    using Windows.UI.Xaml.Input;
    using Windows.UI.Xaml.Controls;
#endif

    using FSClient.Shared.Services;
    using FSClient.UWP.Shared.Helpers;

    public sealed partial class AddReviewDialog : ContentDialog, IContentDialog<string>
    {
        private readonly CoreWindow coreWindow;

        public AddReviewDialog()
        {
            coreWindow = CoreWindow.GetForCurrentThread();
            InitializeComponent();
        }

        public string UserReview => ReviewTextBox.Text;

        private void ContentDialog_SecondaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            ReviewTextBox.Text = string.Empty;
        }

        private void TextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            IsPrimaryButtonEnabled = !string.IsNullOrEmpty(ReviewTextBox.Text);
        }

        private void ReviewTextBox_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            var shiftDown = coreWindow.GetKeyState(VirtualKey.Shift).HasFlag(CoreVirtualKeyStates.Down);
            if (shiftDown && e.Key == VirtualKey.Enter)
            {
                Hide();
            }
        }

        Task<string> IContentDialog<string>.ShowAsync(CancellationToken cancellationToken)
        {
            return Dispatcher.CheckBeginInvokeOnUI(async () => (await this
                    .ShowAsync(cancellationToken).ConfigureAwait(true)) switch
                {
                    ContentDialogResult.Primary => UserReview,
                    _ => string.Empty,
                });
        }
    }
}
