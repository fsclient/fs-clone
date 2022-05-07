namespace FSClient.UWP.Shared.Views.Dialogs
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    using Windows.Media.Casting;
    using Windows.Media.Core;
    using Windows.Media.Playback;

#if WINUI3
    using Microsoft.UI.Xaml;
#else
    using Windows.UI.Xaml;
#endif

    using FSClient.Localization.Resources;
    using FSClient.Shared.Services;
    using FSClient.UWP.Shared.Helpers;
    using FSClient.UWP.Shared.Services;

    using Microsoft.Extensions.Logging;

    public sealed partial class
        LegacyRemoteLaunchDialog : IContentDialog<RemoteLaunchDialogInput, RemoteLaunchDialogOutput>
    {
        private readonly ILogger logger;
        private readonly INotificationService notificationService;
        private readonly Lazy<MediaPlayer> mediaPlayerFactory;

        public LegacyRemoteLaunchDialog()
        {
            logger = Logger.Instance;
            notificationService = ViewModelLocator.Current.Resolve<INotificationService>();
            mediaPlayerFactory = new Lazy<MediaPlayer>(() => new MediaPlayer());
        }

        Task<RemoteLaunchDialogOutput> IContentDialog<RemoteLaunchDialogInput, RemoteLaunchDialogOutput>.ShowAsync(
            RemoteLaunchDialogInput remoteLauncherInput,
            CancellationToken cancellationToken)
        {
            var tcs = new TaskCompletionSource<(CastingConnectionErrorStatus status, bool canceled)>();

            return DispatcherHelper.GetForCurrentOrMainView().CheckBeginInvokeOnUI(async () =>
            {
                var internalMPUsed = false;
                var result = default((CastingConnectionErrorStatus status, bool canceled));

                try
                {
                    var picker = new CastingDevicePicker();
                    picker.CastingDeviceSelected += Picker_CastingDeviceSelected;
                    picker.CastingDevicePickerDismissed += Picker_CastingDevicePickerDismissed;

                    if (!(remoteLauncherInput.MediaSource is CastingSource)
                        && remoteLauncherInput.UriSource != null
                        && remoteLauncherInput.UriSourceIsVideo)
                    {
                        remoteLauncherInput = new RemoteLaunchDialogInput(
                            remoteLauncherInput.FSProtocolSource,
                            remoteLauncherInput.UriSource,
                            remoteLauncherInput.UriSourceIsVideo,
                            await GetCastingSourceFromUriAsync(remoteLauncherInput.UriSource));
                        if (remoteLauncherInput.MediaSource != null)
                        {
                            internalMPUsed = true;
                        }
                    }

                    if (remoteLauncherInput.MediaSource is not CastingSource castingSource)
                    {
                        return new RemoteLaunchDialogOutput(false, true, null);
                    }

                    picker.Filter.SupportedCastingSources.Add(castingSource);

                    picker.Show(Window.Current.Bounds, Windows.UI.Popups.Placement.Default);

                    result = await tcs.Task;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex);
                }
                finally
                {
                    if (mediaPlayerFactory.IsValueCreated)
                    {
                        if (result.status != CastingConnectionErrorStatus.Succeeded)
                        {
                            mediaPlayerFactory.Value.Source = null;
                        }
                        else if (internalMPUsed)
                        {
                            mediaPlayerFactory.Value.Play();
                        }
                    }
                }

                return new RemoteLaunchDialogOutput(
                    result.status == CastingConnectionErrorStatus.Succeeded,
                    result.canceled,
                    GetStatusMessage(result.status));
            });


            void Picker_CastingDevicePickerDismissed(CastingDevicePicker sender, object args)
            {
                tcs?.TrySetResult((CastingConnectionErrorStatus.Unknown, true));
            }

            async Task<CastingSource?> GetCastingSourceFromUriAsync(Uri link)
            {
                try
                {
                    if (!link.Scheme.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                    {
                        return null;
                    }

                    var source = MediaSource.CreateFromUri(link);
                    await source.OpenAsync();

                    var mediaPlayer = mediaPlayerFactory.Value;
                    mediaPlayer.AutoPlay = false;
                    mediaPlayer.Source = source;

                    await Task.Yield();

                    return mediaPlayer.GetAsCastingSource();
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex);
                    return null;
                }
            }

            async void Picker_CastingDeviceSelected(CastingDevicePicker sender, CastingDeviceSelectedEventArgs args)
            {
                try
                {
                    await DispatcherHelper.GetForCurrentOrMainView().RunTaskAsync(async () =>
                    {
                        var connection = args.SelectedCastingDevice.CreateCastingConnection();

                        connection.ErrorOccurred += Connection_ErrorOccurred;

                        try
                        {
                            var castingSource = (CastingSource)remoteLauncherInput!.MediaSource!;
                            var status = await connection.RequestStartCastingAsync(castingSource);

                            tcs?.TrySetResult((status, false));
                        }
                        catch (Exception ex)
                        {
                            await notificationService.ShowAsync(Strings.CastTo_StartCastFailed,
                                NotificationType.Error);
                            tcs?.TrySetException(ex);
                        }
                    });
                }
                catch (Exception ex)
                {
                    tcs?.TrySetException(ex);
                }
            }

            async void Connection_ErrorOccurred(CastingConnection sender, CastingConnectionErrorOccurredEventArgs args)
            {
                if (args.ErrorStatus == CastingConnectionErrorStatus.Succeeded)
                {
                    return;
                }

                var errorMessage = GetStatusMessage(args.ErrorStatus);
                await notificationService.ShowAsync(errorMessage, NotificationType.Error);

                if (args.ErrorStatus == CastingConnectionErrorStatus.InvalidCastingSource)
                {
                    var exception = new Exception(args.Message);
                    logger.LogError(exception);
                }
            }

            static string GetStatusMessage(CastingConnectionErrorStatus message)
            {
                return message switch
                {
                    CastingConnectionErrorStatus.DeviceDidNotRespond => Strings.CastTo_DeviceDidNotRespond,
                    CastingConnectionErrorStatus.DeviceError => Strings.CastTo_DeviceError,
                    CastingConnectionErrorStatus.DeviceLocked => Strings.CastTo_DeviceLocked,
                    CastingConnectionErrorStatus.InvalidCastingSource => Strings.CastTo_InvalidCastingSource,
                    CastingConnectionErrorStatus.ProtectedPlaybackFailed => Strings.CastTo_ProtectedPlaybackFailed,
                    CastingConnectionErrorStatus.Succeeded => Strings.CastTo_Succeeded,
                    _ => Strings.CastTo_Unknown
                };
            }
        }
    }
}
