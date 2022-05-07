namespace FSClient.UWP.Shared.Views.Dialogs
{
    using System.Threading;
    using System.Threading.Tasks;

    using Windows.System;

#if WINUI3
    using Microsoft.UI.Xaml;
    using Microsoft.UI.Xaml.Input;
    using Microsoft.UI.Xaml.Controls;
#else
    using Windows.UI.Xaml;
    using Windows.UI.Xaml.Input;
    using Windows.UI.Xaml.Controls;
#endif

    using FSClient.Shared.Providers;
    using FSClient.Shared.Services;
    using FSClient.UWP.Shared.Helpers;

    public sealed partial class LoginDialog : ContentDialog, IContentDialog<LoginDialogOutput>
    {
        public LoginDialog()
        {
            InitializeComponent();
        }

        Task<LoginDialogOutput> IContentDialog<LoginDialogOutput>.ShowAsync(CancellationToken cancellationToken)
        {
            return Dispatcher.CheckBeginInvokeOnUI(async () =>
            {
                var result = await this.ShowAsync(cancellationToken).ConfigureAwait(true) switch
                {
                    ContentDialogResult.Secondary => new LoginDialogOutput {Status = AuthStatus.Canceled},
                    _ => new LoginDialogOutput
                    {
                        Status = cancellationToken.IsCancellationRequested
                            ? AuthStatus.Canceled
                            : AuthStatus.Success,
                        Login = LoginBox.Text,
                        Password = PasswordBox.Password
                    },
                };
                PasswordBox.Password = string.Empty;
                return result;
            });
        }

        private void PasswordBox_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            IsPrimaryButtonEnabled =
                PasswordBox.Password.Length > 1
                && LoginBox.Text.Length > 1;

            if (e.Key == VirtualKey.Enter && IsPrimaryButtonEnabled)
            {
                Hide();
            }
        }

        private void LoginBox_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            IsPrimaryButtonEnabled =
                PasswordBox.Password.Length > 1
                && LoginBox.Text.Length > 1;

            if (e.Key == VirtualKey.Enter)
            {
                PasswordBox.Focus(FocusState.Programmatic);
            }
        }
    }
}
