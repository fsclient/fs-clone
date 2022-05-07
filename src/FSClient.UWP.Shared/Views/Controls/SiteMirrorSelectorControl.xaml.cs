namespace FSClient.UWP.Shared.Views.Controls
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

#if WINUI3
    using Microsoft.UI.Xaml;
    using Microsoft.UI.Xaml.Controls;
#else
    using Windows.UI.Xaml;
    using Windows.UI.Xaml.Controls;
#endif

    using FSClient.Shared.Helpers;
    using FSClient.Shared.Helpers.Web;
    using FSClient.Shared.Models;
    using FSClient.ViewModels.Items;

    public sealed partial class SiteMirrorSelectorControl : UserControl
    {
        public static readonly DependencyProperty ProviderModelProperty =
            DependencyProperty.RegisterAttached(nameof(ProviderModel), typeof(ProviderViewModel),
                typeof(SiteMirrorSelectorControl),
                new PropertyMetadata(Site.Any, SiteChanged));

        private static void SiteChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((SiteMirrorSelectorControl)d).InputTextBox.Text =
                (e.NewValue as ProviderViewModel)?.UserMirror?.OriginalString ?? "";
        }

        private CancellationTokenSource? cancellationTokenSource;

        public SiteMirrorSelectorControl()
        {
            InitializeComponent();
        }

        public ProviderViewModel? ProviderModel
        {
            get => (ProviderViewModel?)GetValue(ProviderModelProperty);
            set => SetValue(ProviderModelProperty, value);
        }

        private async void TextBox_TextChanging(TextBox sender, TextBoxTextChangingEventArgs args)
        {
            cancellationTokenSource?.Cancel();

            if (string.IsNullOrWhiteSpace(sender.Text))
            {
                IsLoadingControl.Visibility
                    = IsFailledControl.Visibility
                        = IsSuccessControl.Visibility = Visibility.Collapsed;

                if (ProviderModel?.UserMirror != null)
                {
                    ProviderModel.UserMirror = null;
                    RestartRequiredBlock.Visibility = Visibility.Visible;
                }

                return;
            }

            if (!Uri.TryCreate(sender.Text, UriKind.Absolute, out var uri)
                || !uri.IsHttpUri())
            {
                IsFailledControl.Visibility = Visibility.Visible;
                IsLoadingControl.Visibility
                    = IsSuccessControl.Visibility = Visibility.Collapsed;
                return;
            }

            cancellationTokenSource = new CancellationTokenSource();
            var token = cancellationTokenSource.Token;

            IsLoadingControl.Visibility = Visibility.Visible;
            IsFailledControl.Visibility
                = IsSuccessControl.Visibility = Visibility.Collapsed;

            var available = await Task.Run(() => uri.IsAvailableAsync(token)).ConfigureAwait(true);
            if (token.IsCancellationRequested)
            {
                return;
            }

            IsLoadingControl.Visibility = Visibility.Collapsed;
            if (available)
            {
                IsFailledControl.Visibility = Visibility.Collapsed;
                IsSuccessControl.Visibility = Visibility.Visible;

                if (ProviderModel?.UserMirror?.OriginalString != sender.Text)
                {
                    ProviderModel!.UserMirror = uri;
                    RestartRequiredBlock.Visibility = Visibility.Visible;
                }
            }
            else
            {
                IsFailledControl.Visibility = Visibility.Visible;
                IsSuccessControl.Visibility = Visibility.Collapsed;
            }
        }
    }
}
