namespace FSClient.Shared.Services
{
    using System;
    using System.Collections.Concurrent;
    using System.Threading;
    using System.Threading.Tasks;

    using Microsoft.Extensions.Logging;

    public class CacheService : ICacheService
    {
        private const string CacheFolder = "Cache";
        private readonly IStorageService storageService;
        private readonly ILogger logger;
        private readonly ConcurrentDictionary<string, object?> inMemoryCache;

        public CacheService(
            IStorageService storageService,
            ILogger logger)
        {
            this.storageService = storageService;
            this.logger = logger;

            inMemoryCache = new ConcurrentDictionary<string, object?>();
        }

        public ValueTask<TType> GetOrAddAsync<TType>(string key, Func<string, CancellationToken, Task<TType>> addFunc, TimeSpan maxAge, CancellationToken cancellationToken)
        {
            if (inMemoryCache.TryGetValue(key, out var value))
            {
                return new ValueTask<TType>((TType)value!);
            }

            return GetOrAddFromFileAsync();

            async ValueTask<TType> GetOrAddFromFileAsync()
            {
                TType? value = default;
                try
                {
                    if (storageService.TempFolder is not IStorageFolder tempFolder
                        || await tempFolder.GetOrCreateFolderAsync(CacheFolder).ConfigureAwait(false) is not IStorageFolder folder)
                    {
                        value = await addFunc(key, cancellationToken).ConfigureAwait(false);
                        inMemoryCache.TryAdd(key, value);
                        return value;
                    }

                    var file = await folder.GetFileAsync(key + ".json").ConfigureAwait(false);
                    if (file != null
                        && file.DateCreated.HasValue
                        && file.DateCreated.Value.AddMinutes(maxAge.TotalMinutes) > DateTimeOffset.Now)
                    {
                        value = await file.ReadFromJsonFileAsync<TType>(cancellationToken).ConfigureAwait(false);
                        if (!(value is null))
                        {
                            inMemoryCache.TryAdd(key, value);
                            return value;
                        }
                    }

                    file = await folder.OpenOrCreateFileAsync(key + ".json").ConfigureAwait(false);
                    value = await addFunc(key, cancellationToken).ConfigureAwait(false)!;
                    if (file != null)
                    {
                        await file.WriteJsonAsync(value, cancellationToken).ConfigureAwait(false);
                    }
                    inMemoryCache.TryAdd(key, value);
                    return value;
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex);
                    value ??= await addFunc(key, cancellationToken).ConfigureAwait(false);
                    inMemoryCache.TryAdd(key, value);
                    return value;
                }
            }
        }
    }
}
