namespace FSClient.Shared.Services
{
    using System;
    using System.IO;
    using System.Text;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;

    using FSClient.Shared.Models;

    public static class StorageServiceExtensions
    {
        public const string LogsFolderName = "Logs";
        public const string BackupFolderName = "BackupTask";

        public static async Task<string?> ReadAsTextAsync(this IStorageFile file)
        {
            if (file == null)
            {
                throw new ArgumentNullException(nameof(file));
            }

            using var stream = await file.ReadAsync().ConfigureAwait(false);

            if (stream == null)
            {
                return null;
            }

            using var reader = new StreamReader(stream);

            return await reader.ReadToEndAsync().ConfigureAwait(false);
        }

        public static async Task<bool> WriteTextAsync(this IStorageFile file, string text)
        {
            if (file == null)
            {
                throw new ArgumentNullException(nameof(file));
            }

            var bytes = Encoding.UTF8.GetBytes(text);

            using (var jsonStream = new MemoryStream(bytes))
            {
                if (await file.WriteAsync(jsonStream).ConfigureAwait(false))
                {
                    return true;
                }
            }
            return false;
        }

        public static async Task<T?> ReadFromJsonFileAsync<T>(this IStorageFile file, CancellationToken cancellationToken)
        {
            if (file == null)
            {
                throw new ArgumentNullException(nameof(file));
            }

            using var stream = await file.ReadAsync().ConfigureAwait(false);
            if (stream == null
                || stream.Length == 0
                || cancellationToken.IsCancellationRequested)
            {
                return default;
            }

            try
            {
                using var reader = new StreamReader(stream);
                return await JsonSerializer.DeserializeAsync<T>(stream, new JsonSerializerOptions()
                {
                    Converters =
                {
                    new RangeJsonConverter(),
                    new WebImageJsonConverter(),
                    new SectionJsonConverter(),
                    new SiteJsonConverter(),
                    new TitledTagJsonConverter()
                }
                }, cancellationToken);
            }
            // Empty input is not valid JSON.
            catch (JsonException ex) when (ex.Message.Contains("The input does not contain any JSON tokens"))
            {
                return default;
            }
        }

        public static async Task<bool> WriteJsonAsync<TValue>(this IStorageFile file, TValue value, CancellationToken cancellationToken)
        {
            if (file == null)
            {
                throw new ArgumentNullException(nameof(file));
            }

            using var fileStream = await file.OpenForWriteAsync();
            if (fileStream == null
                || cancellationToken.IsCancellationRequested)
            {
                return false;
            }

            await JsonSerializer.SerializeAsync(fileStream, value, new JsonSerializerOptions()
            {
                Converters =
                {
                    new RangeJsonConverter(),
                    new WebImageJsonConverter(),
                    new SectionJsonConverter(),
                    new SiteJsonConverter(),
                    new TitledTagJsonConverter()
                }
            }, cancellationToken);

            return true;
        }
    }
}
