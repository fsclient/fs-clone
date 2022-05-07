namespace FSClient.UWP.Shared.Services
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    using Windows.Storage;
    using Windows.System;

    using FSClient.Shared;
    using FSClient.Shared.Services;
    using FSClient.UWP.Shared.Helpers;

    using Microsoft.Extensions.Logging;

    using IStorageFile = FSClient.Shared.Services.IStorageFile;
    using IStorageFolder = FSClient.Shared.Services.IStorageFolder;

    public class LauncherService : ILauncherService
    {
        private readonly ILogger logger;
        private readonly IContentDialog<RemoteLaunchDialogInput, RemoteLaunchDialogOutput> remoteLaunchDialog;

        public LauncherService(ILogger log, IContentDialog<RemoteLaunchDialogInput, RemoteLaunchDialogOutput> dialog)
        {
            logger = log;
            remoteLaunchDialog = dialog;
        }

        public async Task<LaunchResult> LaunchFolderAsync(IStorageFolder folder)
        {
            try
            {
                if ((folder as UWPStorageFolder)?.Folder is not StorageFolder uwpFolder)
                {
                    return LaunchResult.ErrorInvalidStorageItem;
                }

                var success = await DispatcherHelper
                    .GetForCurrentOrMainView()
                    .CheckBeginInvokeOnUI(() => Launcher.LaunchFolderAsync(uwpFolder).AsTask())
                    .ConfigureAwait(false);
                if (success)
                {
                    return LaunchResult.Success;
                }
            }
            catch (Exception ex)
            {
                logger?.LogWarning(ex);
            }

            return LaunchResult.UnknownError;
        }

        public async Task<LaunchResult> LaunchUriAsync(Uri uri)
        {
            try
            {
                var query = await Launcher.QueryUriSupportAsync(uri, LaunchQuerySupportType.Uri);
                if (query != LaunchQuerySupportStatus.Available)
                {
                    return LaunchResult.ErrorHandlerIsNotAvailable;
                }

                var success = await DispatcherHelper
                    .GetForCurrentOrMainView()
                    .CheckBeginInvokeOnUI(() => Launcher
                        .LaunchUriAsync(
                            uri,
                            new LauncherOptions
                            {
                                DisplayApplicationPicker =
                                    UWPAppInformation.Instance.DeviceFamily == DeviceFamily.Desktop
                                    && Settings.Instance.IgnoreLauncherDefaults
                            }).AsTask())
                    .ConfigureAwait(false);
                if (success)
                {
                    return LaunchResult.Success;
                }
            }
            catch (Exception ex)
            {
                logger?.LogWarning(ex);
            }

            return LaunchResult.UnknownError;
        }

        public async Task<LaunchResult> LaunchFileAsync(IStorageFile file)
        {
            try
            {
                if ((file as UWPStorageFile)?.File is not StorageFile uwpFile)
                {
                    return LaunchResult.ErrorInvalidStorageItem;
                }

                var query = await Launcher.QueryFileSupportAsync(uwpFile);
                if (query != LaunchQuerySupportStatus.Available)
                {
                    return LaunchResult.ErrorHandlerIsNotAvailable;
                }

                var success = await DispatcherHelper
                    .GetForCurrentOrMainView()
                    .CheckBeginInvokeOnUI(() => Launcher
                        .LaunchFileAsync(
                            uwpFile,
                            new LauncherOptions
                            {
                                DisplayApplicationPicker =
                                    UWPAppInformation.Instance.DeviceFamily == DeviceFamily.Desktop
                                    && Settings.Instance.IgnoreLauncherDefaults
                            }).AsTask())
                    .ConfigureAwait(false);
                if (success)
                {
                    return LaunchResult.Success;
                }
            }
            catch (Exception ex)
            {
                logger?.LogWarning(ex);
            }

            return LaunchResult.UnknownError;
        }

        public Task<RemoteLaunchDialogOutput> RemoteLaunchUriAsync(RemoteLaunchDialogInput input)
        {
            return remoteLaunchDialog.ShowAsync(input, CancellationToken.None);
        }
    }
}
