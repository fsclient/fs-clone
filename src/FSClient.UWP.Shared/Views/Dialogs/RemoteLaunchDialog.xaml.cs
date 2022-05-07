namespace FSClient.UWP.Shared.Views.Dialogs
{
    using System;
    using System.Collections.ObjectModel;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    using Windows.Devices.Enumeration;
    using Windows.Foundation.Metadata;
    using Windows.Media.Casting;
    using Windows.Media.Core;
    using Windows.Media.Playback;
    using Windows.System;
    using Windows.System.RemoteSystems;
    using Windows.UI.Core;

#if WINUI3
    using Microsoft.UI.Xaml;
    using Microsoft.UI.Xaml.Media;
    using Microsoft.UI.Xaml.Controls;
    using Microsoft.UI.Xaml.Media.Imaging;
#else
    using Windows.UI.Xaml;
    using Windows.UI.Xaml.Media;
    using Windows.UI.Xaml.Controls;
    using Windows.UI.Xaml.Media.Imaging;
#endif

    using FSClient.Localization.Resources;
    using FSClient.Shared.Helpers;
    using FSClient.Shared.Mvvm;
    using FSClient.Shared.Services;
    using FSClient.UWP.Shared.Helpers;

    using Microsoft.Extensions.Logging;

    public class RemoteSystemVM : BindableBase
    {
        private static readonly bool ManufacturerDisplayNameAvailabale
            = ApiInformation.IsPropertyPresent(typeof(RemoteSystem).FullName,
                nameof(RemoteSystem.ManufacturerDisplayName));

        private static readonly bool ModelDisplayNameAvailabale
            = ApiInformation.IsPropertyPresent(typeof(RemoteSystem).FullName, nameof(RemoteSystem.ModelDisplayName));

        public RemoteSystemVM(RemoteSystem remoteSystem)
        {
            RemoteSystemEntry = remoteSystem;

            ManufacturerDisplayName = ManufacturerDisplayNameAvailabale
                ? remoteSystem.ManufacturerDisplayName
                : remoteSystem.DisplayName;
            ModelDisplayName = ModelDisplayNameAvailabale
                ? remoteSystem.ModelDisplayName
                : remoteSystem.DisplayName;
            DisplayName = remoteSystem.DisplayName;
            Kind = remoteSystem.Kind;
        }

        public RemoteSystemVM(DeviceInformation deviceInformation)
        {
            DeviceInformationEntry = deviceInformation;

            DisplayName = deviceInformation.Name;
        }

        public RemoteSystem? RemoteSystemEntry { get; }

        public DeviceInformation? DeviceInformationEntry { get; }

        public string? ModelDisplayName { get; }

        public string? ManufacturerDisplayName { get; }

        public string DisplayName { get; }

        public string? Kind { get; }

        public bool IsSending
        {
            get => Get<bool>();
            set => Set(value);
        }

        public FrameworkElement? Icon { get; set; }
    }

    public sealed partial class RemoteLaunchDialog : ContentDialog,
        IContentDialog<RemoteLaunchDialogInput, RemoteLaunchDialogOutput>
    {
        private readonly ILogger logger;
        private readonly Lazy<MediaPlayer> mediaPlayerFactory;

        private RemoteLaunchDialogOutput? launchResult;
        private RemoteLaunchDialogInput remoteLauncherInput = new RemoteLaunchDialogInput(null, null, false, null);

        private (DispatcherTimer? timer, RemoteSystemWatcher? remote, DeviceWatcher? device, bool deviceLaunched, bool
            internalMPUsed) watchersPerTimer;

        public RemoteLaunchDialog()
        {
            logger = Logger.Instance;
            mediaPlayerFactory = new Lazy<MediaPlayer>(() => new MediaPlayer());

            InitializeComponent();
        }

        public ObservableCollection<RemoteSystemVM> RemoteSystems { get; } = new ObservableCollection<RemoteSystemVM>();

        private async Task<RemoteSystemWatcher?> GetRemoteSystemWatcher()
        {
            try
            {
                var accessStatus = await RemoteSystem.RequestAccessAsync();
                if (accessStatus == RemoteSystemAccessStatus.Allowed)
                {
                    var remoteSystemWatcher = RemoteSystem.CreateWatcher();
                    remoteSystemWatcher.RemoteSystemAdded += RemoteSystemWatcher_RemoteSystemAdded;
                    remoteSystemWatcher.RemoteSystemRemoved += RemoteSystemWatcher_RemoteSystemRemoved;
                    remoteSystemWatcher.RemoteSystemUpdated += RemoteSystemWatcher_RemoteSystemUpdated;
                    return remoteSystemWatcher;
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex);
            }

            return null;
        }

        private DeviceWatcher? GetDeviceWatcher()
        {
            try
            {
                var selector = CastingDevice.GetDeviceSelector(CastingPlaybackTypes.Video);
                var watcher = DeviceInformation.CreateWatcher(selector);
                watcher.Added += DeviceWatcher_Added;
                watcher.Removed += DeviceWatcher_Removed;
                watcher.Stopped += DeviceWatcher_Stopped;
                watcher.EnumerationCompleted += DeviceWatcher_Stopped;
                return watcher;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex);
            }

            return null;
        }

        private DispatcherTimer CreateTimer()
        {
            var notFoundTimer = new DispatcherTimer();
            notFoundTimer.Interval = TimeSpan.FromSeconds(10);
            notFoundTimer.Tick += (s, a) =>
            {
                notFoundTimer.Stop();
                if (!RemoteSystems.Any())
                {
                    LoadingProgressRing.Visibility = Visibility.Collapsed;
                    ErrorBlock.Text = Strings.RemoteLaunchDialog_NoDeviceWasFound;
                }
            };
            return notFoundTimer;
        }

        Task<RemoteLaunchDialogOutput> IContentDialog<RemoteLaunchDialogInput, RemoteLaunchDialogOutput>.ShowAsync(
            RemoteLaunchDialogInput arg, CancellationToken cancellationToken)
        {
            remoteLauncherInput = arg ?? throw new ArgumentNullException(nameof(arg));
            return Dispatcher.CheckBeginInvokeOnUI(async () =>
            {
                launchResult = null;
                watchersPerTimer = default;

                try
                {
                    await this.ShowAsync(cancellationToken).ConfigureAwait(true);
                }
                finally
                {
                    if (mediaPlayerFactory.IsValueCreated)
                    {
                        if (!watchersPerTimer.deviceLaunched)
                        {
                            mediaPlayerFactory.Value.Source = null;
                        }
                        else if (watchersPerTimer.internalMPUsed)
                        {
                            mediaPlayerFactory.Value.Play();
                        }
                    }
                }

                return launchResult ?? new RemoteLaunchDialogOutput(false, true, null);
            });
        }

        private async void ShareToDeviceDialog_Opened(ContentDialog sender, ContentDialogOpenedEventArgs args)
        {
            ErrorBlock.Text = "";

            LoadingProgressRing.Visibility = Visibility.Visible;

            ClearState();

            watchersPerTimer = (CreateTimer(), null, null, false, false);

            if (remoteLauncherInput.FSProtocolSource != null
                || remoteLauncherInput!.UriSource != null)
            {
                watchersPerTimer.remote = await GetRemoteSystemWatcher().ConfigureAwait(true);
            }

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
                    watchersPerTimer.internalMPUsed = true;
                }
            }

            if (remoteLauncherInput.MediaSource is CastingSource castingSource)
            {
                castingSource.PreferredSourceUri = remoteLauncherInput.UriSource;
                watchersPerTimer.device = GetDeviceWatcher();
            }

            if (watchersPerTimer.remote == null && watchersPerTimer.device == null)
            {
                LoadingProgressRing.Visibility = Visibility.Collapsed;
                ErrorBlock.Text = Strings.RemoteLaunchDialog_AccessDenied;
            }
            else
            {
                watchersPerTimer.remote?.Start();
                watchersPerTimer.device?.Start();
                watchersPerTimer.timer?.Start();
            }
        }

        private void ShareToDeviceDialog_Closed(ContentDialog sender, ContentDialogClosedEventArgs args)
        {
            ClearState();
        }

        private void ClearState()
        {
            try
            {
                watchersPerTimer.remote?.Stop();
                watchersPerTimer.device?.Stop();
                watchersPerTimer.timer?.Stop();
                watchersPerTimer = default;

                RemoteSystems.Clear();
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex);
            }
        }

        private async void RemoteSystemWatcher_RemoteSystemUpdated(RemoteSystemWatcher sender,
            RemoteSystemUpdatedEventArgs args)
        {
            await Dispatcher.RunTaskAsync(async () =>
            {
                var index = RemoteSystems.IndexOf(
                    RemoteSystems.FirstOrDefault(rs => rs.RemoteSystemEntry?.Id == args.RemoteSystem.Id));
                if (index > -1)
                {
                    var item = new RemoteSystemVM(args.RemoteSystem);
                    item.Icon = await GetIconFromRemoteSystemVM(item);
                    RemoteSystems[index] = item;
                }
            });
        }

        private async void RemoteSystemWatcher_RemoteSystemRemoved(RemoteSystemWatcher sender,
            RemoteSystemRemovedEventArgs args)
        {
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                var index = RemoteSystems.IndexOf(
                    RemoteSystems.FirstOrDefault(rs => rs.RemoteSystemEntry?.Id == args.RemoteSystemId));
                if (index >= 0)
                {
                    RemoteSystems.RemoveAt(index);
                }
            });
        }

        private async void RemoteSystemWatcher_RemoteSystemAdded(RemoteSystemWatcher sender,
            RemoteSystemAddedEventArgs args)
        {
            if (watchersPerTimer.remote != sender)
            {
                return;
            }

            var timer = watchersPerTimer.timer;

            await Dispatcher.RunTaskAsync(async () =>
            {
                var index = RemoteSystems.IndexOf(
                    RemoteSystems.FirstOrDefault(rs => rs.RemoteSystemEntry?.Id == args.RemoteSystem.Id));
                if (index < 0)
                {
                    LoadingProgressRing.Visibility = Visibility.Collapsed;
                    timer?.Stop();
                    var item = new RemoteSystemVM(args.RemoteSystem);
                    item.Icon = await GetIconFromRemoteSystemVM(item);
                    RemoteSystems.Add(item);
                }
            });
        }

        private async void DeviceWatcher_Stopped(DeviceWatcher sender, object args)
        {
            if (watchersPerTimer.device != sender)
            {
                return;
            }

            var timer = watchersPerTimer.timer;

            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                if (RemoteSystems.Any())
                {
                    timer?.Stop();
                }
            });
        }

        private async void DeviceWatcher_Removed(DeviceWatcher sender, DeviceInformationUpdate args)
        {
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                var index = RemoteSystems.IndexOf(
                    RemoteSystems.FirstOrDefault(rs => rs.DeviceInformationEntry?.Id == args.Id));
                if (index >= 0)
                {
                    RemoteSystems.RemoveAt(index);
                }
            });
        }

        private async void DeviceWatcher_Added(DeviceWatcher sender, DeviceInformation args)
        {
            if (watchersPerTimer.device != sender)
            {
                return;
            }

            var timer = watchersPerTimer.timer;

            await Dispatcher.RunTaskAsync(async () =>
            {
                var index = RemoteSystems.IndexOf(
                    RemoteSystems.FirstOrDefault(rs => rs.DeviceInformationEntry?.Id == args.Id));
                if (index < 0)
                {
                    LoadingProgressRing.Visibility = Visibility.Collapsed;
                    timer?.Stop();
                    var item = new RemoteSystemVM(args);
                    item.Icon = await GetIconFromRemoteSystemVM(item);
                    RemoteSystems.Add(item);
                }
            });
        }

        private async void ListView_ItemClick(object sender, ItemClickEventArgs e)
        {
            var remoteSystemVM = (RemoteSystemVM)e.ClickedItem;
            if (remoteSystemVM.IsSending)
            {
                return;
            }

            try
            {
                launchResult = default;
                ErrorBlock.Text = "";

                remoteSystemVM.IsSending = true;
                if (remoteSystemVM.RemoteSystemEntry is { } remoteSystem)
                {
                    launchResult = await LaunchRemoteAsync(remoteSystem);
                }
                else if (remoteSystemVM.DeviceInformationEntry is { } deviceInformation)
                {
                    launchResult = await LaunchDeviceAsync(deviceInformation);
                }

                remoteSystemVM.IsSending = false;

                if (launchResult?.IsSuccess == true)
                {
                    Hide();
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.LogError(ex);
            }
            finally
            {
                ErrorBlock.Text = launchResult?.Error ?? "";
            }
        }

        private async Task<RemoteLaunchDialogOutput> LaunchRemoteAsync(RemoteSystem remoteSystem)
        {
            var sourceLink = remoteLauncherInput!.FSProtocolSource ?? remoteLauncherInput.UriSource;
            var result =
                await RemoteLauncher.LaunchUriAsync(new RemoteSystemConnectionRequest(remoteSystem), sourceLink);

            switch (result)
            {
                case RemoteLaunchUriStatus.Success:
                    return new RemoteLaunchDialogOutput(true, false, null);
                case RemoteLaunchUriStatus.AppUnavailable:
                    return new RemoteLaunchDialogOutput(false, false, Strings.RemoteLaunchDialog_AppUnavailable);
                case RemoteLaunchUriStatus.DeniedByLocalSystem:
                    return new RemoteLaunchDialogOutput(false, false, Strings.RemoteLaunchDialog_DeniedByLocalSystem);
                case RemoteLaunchUriStatus.DeniedByRemoteSystem:
                    return new RemoteLaunchDialogOutput(false, false, Strings.RemoteLaunchDialog_DeniedByRemoteSystem);
                case RemoteLaunchUriStatus.ProtocolUnavailable:
                    var str = Strings.RemoteLaunchDialog_ProtocolUnavailable;
                    if (sourceLink?.Scheme.StartsWith(UriParserHelper.AppProtocol, StringComparison.Ordinal) == true)
                    {
                        str += "\r\n" + Strings.RemoteLaunchDialog_ProbablyNeedToUpdateRemote;
                    }

                    return new RemoteLaunchDialogOutput(false, false, str);
                case RemoteLaunchUriStatus.RemoteSystemUnavailable:
                    return new RemoteLaunchDialogOutput(false, false,
                        Strings.RemoteLaunchDialog_RemoteSystemUnavailable);
                case RemoteLaunchUriStatus.ValueSetTooLarge:
                    return new RemoteLaunchDialogOutput(false, false, Strings.RemoteLaunchDialog_ValueSetTooLarge);
                default:
                    return new RemoteLaunchDialogOutput(false, false, Strings.RemoteLaunchDialog_UnknownError);
            }
        }

        private async Task<CastingSource?> GetCastingSourceFromUriAsync(Uri link)
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

        private async Task<RemoteLaunchDialogOutput> LaunchDeviceAsync(DeviceInformation deviceInformation)
        {
            try
            {
                return await Dispatcher.CheckBeginInvokeOnUI(async () =>
                {
                    var device = await CastingDevice.FromIdAsync(deviceInformation.Id);
                    var connection = device.CreateCastingConnection();

                    var castingSource = (CastingSource)remoteLauncherInput!.MediaSource!;

                    var status = await connection.RequestStartCastingAsync(castingSource);
                    var errorMessage = status switch
                    {
                        CastingConnectionErrorStatus.DeviceDidNotRespond => Strings.CastTo_DeviceDidNotRespond,
                        CastingConnectionErrorStatus.DeviceError => Strings.CastTo_DeviceError,
                        CastingConnectionErrorStatus.DeviceLocked => Strings.CastTo_DeviceLocked,
                        CastingConnectionErrorStatus.InvalidCastingSource => Strings.CastTo_InvalidCastingSource,
                        CastingConnectionErrorStatus.ProtectedPlaybackFailed => Strings.CastTo_ProtectedPlaybackFailed,
                        CastingConnectionErrorStatus.Succeeded => Strings.CastTo_Succeeded,
                        _ => Strings.CastTo_Unknown
                    };

                    if (status == CastingConnectionErrorStatus.Succeeded)
                    {
                        watchersPerTimer.deviceLaunched = true;
                    }

                    return new RemoteLaunchDialogOutput(status == CastingConnectionErrorStatus.Succeeded, false,
                        errorMessage);
                });
            }
            catch (Exception ex)
            {
                logger.LogError(ex);
            }

            return new RemoteLaunchDialogOutput(false, true, null);
        }

        private void CloseAppBarButton_Click(object sender, RoutedEventArgs e)
        {
            launchResult = null;
            Hide();
        }

        public static async Task<FrameworkElement> GetIconFromRemoteSystemVM(RemoteSystemVM input)
        {
            if (input.RemoteSystemEntry?.Kind is { } remoteKind)
            {
                var stringArg = remoteKind.ToUpperInvariant();
                string glyph;

                if (stringArg.Contains("XBOX"))
                {
                    glyph = "\uE990";
                }
                else if (stringArg.Contains("PHONE"))
                {
                    glyph = "\uE8EA";
                }
                else if (stringArg.Contains("DESKTOP"))
                {
                    glyph = "\uE977";
                }
                else
                {
                    glyph = "\uE975";
                }

                return new FontIcon {Glyph = glyph, FontFamily = new FontFamily("Segoe MDL2 Assets")};
            }
            else if (input.DeviceInformationEntry is { } deviceInfo)
            {
                try
                {
                    var stream = await deviceInfo.GetGlyphThumbnailAsync();
                    if (stream != null)
                    {
                        var bitmap = new BitmapImage();
                        await bitmap.SetSourceAsync(stream);
                        return new Image {Source = bitmap, Width = 20, Height = 20, Stretch = Stretch.Uniform};
                    }
                }
                catch (Exception ex)
                {
                    Logger.Instance.LogWarning(ex);
                }
            }

            return new FontIcon {Glyph = "\uE975", FontFamily = new FontFamily("Segoe MDL2 Assets")};
        }
    }
}
