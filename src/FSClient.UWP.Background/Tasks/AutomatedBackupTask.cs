#nullable enable
namespace FSClient.UWP.Background.Tasks
{
    using System;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    using Windows.ApplicationModel.Background;

    using FSClient.Shared;
    using FSClient.Shared.Helpers;
    using FSClient.Shared.Managers;
    using FSClient.Shared.Models;
    using FSClient.Shared.Services;
    using FSClient.UWP.Shared.Services;

    using Microsoft.Extensions.Logging;

    public sealed class AutomatedBackupTask : IBackgroundTask
    {
        private const string BackupNameMask = "fs-backup-{0:yyyy-MM-dd-HH-mm}.json";
        private const int BackupsToKeepOnDisk = 10;

        public async void Run(IBackgroundTaskInstance taskInstance)
        {
            var deferral = taskInstance.GetDeferral();
            var cts = new CancellationTokenSource();
            taskInstance.Canceled += (sender, reason) => cts?.Cancel();
            ViewModelLocator? viewModelLocator = null;

            try
            {
                viewModelLocator = new ViewModelLocator(isReadOnly: true);
                UWPLoggerHelper.InitGlobalHandlers();
                if (!Settings.Instance.AutomatedBackupTaskEnabled)
                {
                    return;
                }

                await CreateBackupAsync(viewModelLocator, cts.Token);
            }
            catch (OperationCanceledException)
            {

            }
            catch (Exception ex)
            when (Logger.Initialized)
            {
                Logger.Instance.LogError(ex);
            }
            finally
            {
                cts.Dispose();
                viewModelLocator?.Dispose();
                deferral.Complete();
            }
        }

        private async Task CreateBackupAsync(ViewModelLocator viewModelLocator, CancellationToken cancellationToken)
        {
            var backupManager = viewModelLocator.Resolve<IBackupManager>();
            var storageService = viewModelLocator.Resolve<IStorageService>();

            var backupData = await backupManager.BackupAsync(BackupDataTypes.All, cancellationToken).ConfigureAwait(false);
            if (cancellationToken.IsCancellationRequested
                || storageService.LocalFolder == null)
            {
                return;
            }

            IStorageFolder? backupFolder = null;
            if (Settings.Instance.AutomatedBackupTaskCustomFolder is string customFolderGuid)
            {
                backupFolder = await storageService.GetSavedFolderAsync(customFolderGuid).ConfigureAwait(false);
            }
            backupFolder ??= await storageService.LocalFolder.GetOrCreateFolderAsync(StorageServiceExtensions.BackupFolderName)
                .ConfigureAwait(false);
            if (backupFolder == null)
            {
                return;
            }

            await CleanBackupFolderAsync(backupFolder).ConfigureAwait(false);

            var backupName = string.Format(BackupNameMask, DateTime.Now);
            var file = await backupFolder.OpenOrCreateFileAsync(backupName);
            if (file == null
                || cancellationToken.IsCancellationRequested)
            {
                return;
            }

            await file.WriteJsonAsync(backupData, cancellationToken).ConfigureAwait(false);
        }

        private Task CleanBackupFolderAsync(IStorageFolder backupFolder)
        {
            return backupFolder.GetItemsAsync()
                .ToFlatAsyncEnumerable()
                .OfType<IStorageFile>()
                .Where(f => f.Title.StartsWith("fs-backup-", StringComparison.Ordinal))
                .OrderByDescending(f => f.DateCreated)
                .Skip(BackupsToKeepOnDisk)
                .WhenAllAsync((t, _) => t.DeleteAsync());
        }
    }
}
