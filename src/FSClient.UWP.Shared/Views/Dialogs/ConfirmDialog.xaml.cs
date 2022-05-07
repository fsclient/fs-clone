namespace FSClient.UWP.Shared.Views.Dialogs
{
    using System.Threading;
    using System.Threading.Tasks;

#if WINUI3
    using Microsoft.UI.Xaml.Controls;
#else
    using Windows.UI.Xaml.Controls;
#endif

    using FSClient.Shared.Services;
    using FSClient.UWP.Shared.Helpers;

    public sealed partial class ConfirmDialog : ContentDialog, IContentDialog<string, bool>
    {
        public ConfirmDialog()
        {
            InitializeComponent();
        }

        Task<bool> IContentDialog<string, bool>.ShowAsync(string arg, CancellationToken cancellationToken)
        {
            return Dispatcher.CheckBeginInvokeOnUI(async () =>
            {
                ConfirmationText.Text = arg;
                return await this.ShowAsync(cancellationToken).ConfigureAwait(true) switch
                {
                    ContentDialogResult.Primary => true,
                    _ => false,
                };
            });
        }
    }
}
