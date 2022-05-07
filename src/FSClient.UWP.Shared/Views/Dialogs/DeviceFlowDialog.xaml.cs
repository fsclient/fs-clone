namespace FSClient.UWP.Shared.Views.Dialogs
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

    using FSClient.Shared.Providers;
    using FSClient.Shared.Services;
    using FSClient.UWP.Shared.Helpers;

    public sealed partial class DeviceFlowDialog : ContentDialog, IContentDialog<DeviceFlowDialogInput, AuthStatus>
    {
        public static readonly DependencyProperty CodeProperty =
            DependencyProperty.Register(nameof(Code), typeof(string), typeof(DeviceFlowDialog),
                new PropertyMetadata(null));

        public static readonly DependencyProperty RemainingProgressProperty =
            DependencyProperty.Register(nameof(RemainingProgress), typeof(TimeSpan), typeof(DeviceFlowDialog),
                new PropertyMetadata(1d));

        public static readonly DependencyProperty VerificationUriProperty =
            DependencyProperty.Register(nameof(VerificationUri), typeof(Uri), typeof(DeviceFlowDialog),
                new PropertyMetadata(null));

        private readonly DispatcherTimer tickTimer;

        private DateTimeOffset codeShowedAt;
        private DateTimeOffset codeExpiresAt;

        public DeviceFlowDialog()
        {
            tickTimer = new DispatcherTimer();
            tickTimer.Interval = TimeSpan.FromMilliseconds(100);
            tickTimer.Tick += TickTimer_Tick;

            InitializeComponent();
        }

        public string Code
        {
            get => (string)GetValue(CodeProperty);
            set => SetValue(CodeProperty, value);
        }

        public double RemainingProgress
        {
            get => (double)GetValue(RemainingProgressProperty);
            set => SetValue(RemainingProgressProperty, value);
        }

        public Uri VerificationUri
        {
            get => (Uri)GetValue(VerificationUriProperty);
            set => SetValue(VerificationUriProperty, value);
        }

        private void TickTimer_Tick(object sender, object e)
        {
            if (codeExpiresAt < DateTimeOffset.Now)
            {
                Hide();
            }

            RemainingProgress = CalculateRemainingProgress();
        }

        private void ContentDialog_Loaded(object _, object __)
        {
            tickTimer.Start();
        }

        private void ContentDialog_Unloaded(object _, object __)
        {
            tickTimer.Stop();
        }

        private double CalculateRemainingProgress()
        {
            var now = DateTimeOffset.Now;
            return 1 - ((codeShowedAt - now).TotalMilliseconds / (now - codeExpiresAt).TotalMilliseconds);
        }

        Task<AuthStatus> IContentDialog<DeviceFlowDialogInput, AuthStatus>.ShowAsync(DeviceFlowDialogInput arg,
            CancellationToken cancellationToken)
        {
            return Dispatcher.CheckBeginInvokeOnUI(async () =>
            {
                codeExpiresAt = arg.ExpiresAt;
                codeShowedAt = DateTimeOffset.Now;

                VerificationUri = arg.VerificationUri;
                Code = arg.Code;
                RemainingProgress = CalculateRemainingProgress();

                switch (await this.ShowAsync(cancellationToken).ConfigureAwait(true))
                {
                    case ContentDialogResult.Primary:
                        return AuthStatus.Success;
                    default:
                        return cancellationToken.IsCancellationRequested
                            ? AuthStatus.Canceled
                            : AuthStatus.Error;
                }
            });
        }
    }
}
