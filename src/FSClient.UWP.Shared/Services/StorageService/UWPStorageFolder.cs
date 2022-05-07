namespace FSClient.UWP.Shared.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    using Windows.Storage;

    using FSClient.Shared.Services;

    using Microsoft.Extensions.Logging;

    using IStorageFile = FSClient.Shared.Services.IStorageFile;
    using IStorageFolder = FSClient.Shared.Services.IStorageFolder;
    using IStorageItem = FSClient.Shared.Services.IStorageItem;

    public class UWPStorageFolder : IStorageFolder
    {
        private readonly ILogger logger;

        public UWPStorageFolder(StorageFolder folder, ILogger log)
        {
            logger = log;
            Folder = folder ?? throw new ArgumentNullException(nameof(folder),
                "UWPStorageFolder cannot be created from null StorageFolder");
        }

        public StorageFolder Folder { get; }

        public string Title => Folder.DisplayName;

        public string Path => Folder.Path;

        public bool CanDelete => true;

        public bool CanLaunch => true;

        public DateTimeOffset? DateCreated => Folder?.DateCreated;

        public async Task<IStorageFile?> GetFileAsync(string fileName)
        {
            if (fileName == null)
            {
                throw new ArgumentNullException(nameof(fileName));
            }

            if (string.IsNullOrWhiteSpace(fileName))
            {
                throw new ArgumentException("File name cannot be empty", nameof(fileName));
            }

            try
            {
                if (await Folder.TryGetItemAsync(fileName) is StorageFile file)
                {
                    return new UWPStorageFile(file, logger);
                }
            }
            catch (Exception ex)
            {
                logger?.LogWarning(ex);
            }

            return null;
        }

        public async Task<IStorageFolder?> GetOrCreateFolderAsync(string folderName)
        {
            if (folderName == null)
            {
                throw new ArgumentNullException(nameof(folderName));
            }

            if (string.IsNullOrWhiteSpace(folderName))
            {
                throw new ArgumentException("Folder name cannot be empty", nameof(folderName));
            }

            try
            {
                var folder = await Folder.CreateFolderAsync(folderName, CreationCollisionOption.OpenIfExists);
                return new UWPStorageFolder(folder, logger);
            }
            catch (Exception ex)
            {
                logger?.LogWarning(ex);
            }

            return null;
        }

        public async Task<IStorageFile?> CreateFileAsync(string fileName, bool replace = false)
        {
            if (fileName == null)
            {
                throw new ArgumentNullException(nameof(fileName));
            }

            if (string.IsNullOrWhiteSpace(fileName))
            {
                throw new ArgumentException("File name cannot be empty", nameof(fileName));
            }

            try
            {
                var uwpFile = await Folder.CreateFileAsync(fileName,
                    replace
                        ? CreationCollisionOption.ReplaceExisting
                        : CreationCollisionOption.GenerateUniqueName);
                if (uwpFile == null)
                {
                    return null;
                }

                return new UWPStorageFile(uwpFile, logger);
            }
            catch (Exception ex)
            {
                logger?.LogWarning(ex);
            }

            return null;
        }

        public async Task<IStorageFile?> OpenOrCreateFileAsync(string fileName)
        {
            if (fileName == null)
            {
                throw new ArgumentNullException(nameof(fileName));
            }

            if (string.IsNullOrWhiteSpace(fileName))
            {
                throw new ArgumentException("File name cannot be empty", nameof(fileName));
            }

            try
            {
                var file = await Folder.CreateFileAsync(fileName, CreationCollisionOption.OpenIfExists);
                if (file == null)
                {
                    return null;
                }

                return new UWPStorageFile(file, logger);
            }
            catch (Exception ex)
            {
                logger?.LogWarning(ex);
            }

            return null;
        }

        public async Task<ulong?> GetAvaliableSpaceAsync()
        {
            try
            {
                var props = await Folder.Properties.RetrievePropertiesAsync(new[] {"System.FreeSpace"});
                return props["System.FreeSpace"] as ulong?;
            }
            catch (Exception ex)
            {
                logger?.LogWarning(ex);
            }

            return null;
        }

        public async Task<IStorageFolder?> GetParentAsync()
        {
            try
            {
                var parent = await Folder.GetParentAsync();
                if (parent == null)
                {
                    return null;
                }

                return new UWPStorageFolder(parent, logger);
            }
            catch (Exception ex)
            {
                logger?.LogWarning(ex);
            }

            return null;
        }

        public async Task<bool> DeleteAsync()
        {
            try
            {
                await Folder.DeleteAsync(StorageDeleteOption.Default);
                return true;
            }
            catch (Exception ex)
            {
                logger?.LogWarning(ex);
            }

            return false;
        }

        public async Task<IEnumerable<IStorageItem>> GetItemsAsync()
        {
            try
            {
                var items = await Folder.GetItemsAsync();
                return items
                    .Select(item => item switch
                    {
                        StorageFile file when item.IsOfType(StorageItemTypes.File) =>
                        (IStorageItem)new UWPStorageFile(file, logger),
                        StorageFolder folder when item.IsOfType(StorageItemTypes.Folder) =>
                        new UWPStorageFolder(folder, logger),
                        _ => null,
                    })
                    .Where(item => item != null)
                    .ToList()!;
            }
            catch (Exception ex)
            {
                logger?.LogWarning(ex);
            }

            return new List<IStorageItem>();
        }
    }
}
