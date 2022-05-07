namespace FSClient.UWP.Shared.Services
{
    using System;
    using System.IO;
    using System.Threading.Tasks;

    using Windows.Storage;

    using FSClient.Shared.Services;

    using Microsoft.Extensions.Logging;

    using IStorageFile = FSClient.Shared.Services.IStorageFile;
    using IStorageFolder = FSClient.Shared.Services.IStorageFolder;

    public class UWPStorageFile : IStorageFile
    {
        private readonly ILogger logger;

        public UWPStorageFile(StorageFile file, ILogger log)
        {
            logger = log;
            File = file ?? throw new ArgumentNullException(nameof(file),
                "UWPStorageFile cannot be created from null StorageFile");
        }

        public StorageFile File { get; }

        public string Title => File.Name;

        public string Path => File.Path;

        public bool CanRead => File.IsAvailable;

        public bool CanWrite => File.IsAvailable;

        public bool CanDelete => File.IsAvailable;

        public bool CanLaunch => File.IsAvailable;

        public DateTimeOffset? DateCreated => File.DateCreated;

        public ulong SizeInBytes => File.GetBasicPropertiesAsync()
            .AsTask().GetAwaiter().GetResult()
            .Size;

        public async Task<Stream?> ReadAsync()
        {
            try
            {
                var randomStream = await File.OpenAsync(FileAccessMode.Read);
                return randomStream.AsStream();
            }
            catch (Exception ex)
            {
                logger?.LogWarning(ex);
            }

            return null;
        }

        public async Task<bool> WriteAsync(Stream stream)
        {
            if (stream == null)
            {
                throw new ArgumentNullException(nameof(stream));
            }

            try
            {
                using (var writeStream = await File.OpenStreamForWriteAsync().ConfigureAwait(false))
                {
                    await stream.CopyToAsync(writeStream).ConfigureAwait(false);
                    await writeStream.FlushAsync().ConfigureAwait(false);
                }

                return true;
            }
            catch (Exception ex)
            {
                logger?.LogWarning(ex);
            }

            return false;
        }

        public async Task<IStorageFolder?> GetParentAsync()
        {
            try
            {
                var parent = await File.GetParentAsync();
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
                await File.DeleteAsync(StorageDeleteOption.Default);
                return true;
            }
            catch (Exception ex)
            {
                logger?.LogWarning(ex);
            }

            return false;
        }

        public Task<Stream?> OpenForWriteAsync()
        {
            return File.OpenStreamForWriteAsync();
        }
    }
}
