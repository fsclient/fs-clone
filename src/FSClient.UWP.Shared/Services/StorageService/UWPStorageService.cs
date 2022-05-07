namespace FSClient.UWP.Shared.Services
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading.Tasks;

    using Windows.Storage;
    using Windows.Storage.AccessCache;
    using Windows.Storage.Pickers;

    using FSClient.Shared.Helpers;
    using FSClient.Shared.Services;
    using FSClient.UWP.Shared.Helpers;

    using Microsoft.Extensions.Logging;

    using IStorageFile = FSClient.Shared.Services.IStorageFile;
    using IStorageFolder = FSClient.Shared.Services.IStorageFolder;

    public class UWPStorageService : IStorageService
    {
        private readonly ILogger logger;
        private readonly Lazy<IStorageFolder?> localFolder;
        private readonly Lazy<IStorageFolder?> tempFolder;

        public UWPStorageService(ILogger logger)
        {
            this.logger = logger;
            localFolder = new Lazy<IStorageFolder?>(() =>
            {
                try
                {
                    if (ApplicationData.Current.LocalFolder != null)
                    {
                        return new UWPStorageFolder(ApplicationData.Current.LocalFolder, this.logger);
                    }
                }
                catch (Exception ex)
                {
                    ex.Data["Reason"] = "Can't create LocalFolder";
                    this.logger?.LogWarning(ex);
                }

                return null;
            });
            tempFolder = new Lazy<IStorageFolder?>(() =>
            {
                try
                {
                    if (ApplicationData.Current.TemporaryFolder != null)
                    {
                        return new UWPStorageFolder(ApplicationData.Current.TemporaryFolder, this.logger);
                    }
                }
                catch (Exception ex)
                {
                    ex.Data["Reason"] = "Can't create TempFolder";
                    this.logger?.LogWarning(ex);
                }

                return null;
            });
        }

        public IStorageFolder? LocalFolder => localFolder.Value;

        public IStorageFolder? TempFolder => tempFolder.Value;

        public async Task<IStorageFile?> OpenFileFromPathAsync(string path)
        {
            try
            {
                var file = await StorageFile.GetFileFromPathAsync(path);
                return new UWPStorageFile(file, logger);
            }
            catch (Exception ex)
            {
                logger?.LogWarning(ex);
            }

            return null;
        }

        public async Task<IStorageFile?> PickFileAsync(string suggestedName, bool createNew = false)
        {
            try
            {
                return await DispatcherHelper
                    .GetForCurrentOrMainView()
                    .CheckBeginInvokeOnUI(async () =>
                    {
                        StorageFile file;
                        var fileExtension = Path.GetExtension(suggestedName);
                        var extension = suggestedName == null ? "*" : fileExtension;
                        if (createNew)
                        {
                            fileExtension ??= ".json";
                            var fileTypeName = fileExtension.TrimStart('.').ToUpperInvariant();
                            var fileName = Path.GetFileNameWithoutExtension(suggestedName);
                            var fileSavePicker = new FileSavePicker
                            {
                                SuggestedStartLocation = PickerLocationId.Desktop,
                                SuggestedFileName = fileName.NotEmptyOrNull(),
                                FileTypeChoices = {[fileTypeName + " file"] = new List<string> {fileExtension}}
                            };
                            file = await fileSavePicker.PickSaveFileAsync();
                        }
                        else
                        {
                            var fileOpenPicker = new FileOpenPicker
                            {
                                ViewMode = PickerViewMode.List,
                                FileTypeFilter = {extension},
                                SuggestedStartLocation = PickerLocationId.Desktop
                            };
                            file = await fileOpenPicker.PickSingleFileAsync();
                        }

                        if (file == null)
                        {
                            return null;
                        }

                        return new UWPStorageFile(file, logger);
                    })
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logger?.LogWarning(ex);
            }

            return null;
        }

        public async Task<IStorageFolder?> PickFolderAsync()
        {
            try
            {
                return await DispatcherHelper
                    .GetForCurrentOrMainView()
                    .CheckBeginInvokeOnUI(async () =>
                    {
                        var folderPicker = new FolderPicker
                        {
                            ViewMode = PickerViewMode.List,
                            SuggestedStartLocation = PickerLocationId.Downloads,
                            FileTypeFilter = {"*"}
                        };
                        var folder = await folderPicker.PickSingleFolderAsync();
                        if (folder == null)
                        {
                            return null;
                        }

                        return new UWPStorageFolder(folder, logger);
                    })
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logger?.LogWarning(ex);
            }

            return null;
        }

        public async Task<IStorageFolder?> GetSavedFolderAsync(string token)
        {
            if (token == null)
            {
                throw new ArgumentNullException(nameof(token));
            }

            if (string.IsNullOrWhiteSpace(token))
            {
                throw new ArgumentException("Folder token cannot be empty", nameof(token));
            }

            try
            {
                if (!StorageApplicationPermissions.FutureAccessList.ContainsItem(token))
                {
                    return null;
                }

                var folder = await StorageApplicationPermissions
                    .FutureAccessList
                    .GetFolderAsync(token);
                if (folder == null)
                {
                    return null;
                }

                return new UWPStorageFolder(folder, logger);
            }
            catch (Exception ex)
            {
                logger?.LogWarning(ex);
            }

            return null;
        }

        public Task<string?> SaveFolderAsync(IStorageFolder folder, string? token = null)
        {
            if (folder == null)
            {
                throw new ArgumentNullException(nameof(folder));
            }

            try
            {
                var uwpFolder = (folder as UWPStorageFolder)?.Folder;
                if (uwpFolder == null)
                {
                    return Task.FromResult<string?>(null);
                }

                if (string.IsNullOrEmpty(token))
                {
                    token = StorageApplicationPermissions.FutureAccessList.Add(uwpFolder);
                }
                else
                {
                    StorageApplicationPermissions.FutureAccessList.AddOrReplace(token, uwpFolder);
                }

                return Task.FromResult(token);
            }
            catch (Exception ex)
            {
                logger?.LogWarning(ex);
            }

            return Task.FromResult<string?>(null);
        }

        public void ForgetSavedFolder(string token)
        {
            if (token == null)
            {
                throw new ArgumentNullException(nameof(token));
            }

            if (string.IsNullOrWhiteSpace(token))
            {
                throw new ArgumentException("Folder token cannot be empty", nameof(token));
            }

            try
            {
                if (StorageApplicationPermissions.FutureAccessList.ContainsItem(token))
                {
                    StorageApplicationPermissions.FutureAccessList.Remove(token);
                }
            }
            catch (Exception ex)
            {
                logger?.LogWarning(ex);
            }
        }

        public async Task<bool> ClearApplicationData()
        {
            try
            {
                await ApplicationData.Current.ClearAsync();
                StorageApplicationPermissions.FutureAccessList.Clear();

                return true;
            }
            catch (Exception ex)
            {
                logger?.LogWarning(ex);
            }

            return false;
        }
    }
}
