namespace FSClient.Shared.Managers
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    using FSClient.Shared.Helpers;
    using FSClient.Shared.Models;
    using FSClient.Shared.Providers;
    using FSClient.Shared.Repositories;
    using FSClient.Shared.Services;

    using Microsoft.Extensions.Logging;

    using Nito.AsyncEx;

    /// <inheritdoc cref="IFavoriteManager" />
    public sealed class FavoriteManager : IFavoriteManager, IDisposable
    {
        private static readonly FavoriteListKind[] localFavoriteListKinds =
            new[] { FavoriteListKind.Favorites, FavoriteListKind.ForLater, FavoriteListKind.InProcess, FavoriteListKind.Finished };

        private IFavoriteProvider? activeProvider;
        private FavoriteProviderType providerType;

        private readonly IFavoriteProvider[] favoritesProviders;
        private readonly ISettingService settingService;
        private readonly IFavoriteRepository favoriteRepository;
        private readonly IProviderManager providerManager;
        private readonly IUserManager userManager;
        private readonly ILogger logger;
        private readonly ConcurrentDictionary<FavoriteListKind, (SemaphoreSlim semaphore, CancellationTokenSource token, List<FavoriteItem>? list)> activeSourceCache;

        public FavoriteManager(
            IEnumerable<IFavoriteProvider> favoritesProviders,
            IProviderManager providerManager,
            IUserManager userManager,
            ISettingService settingService,
            IFavoriteRepository favoriteRepository,
            ILogger logger)
        {
            this.settingService = settingService;
            this.favoriteRepository = favoriteRepository;
            this.providerManager = providerManager;
            this.userManager = userManager;
            this.logger = logger;

            activeSourceCache = new ConcurrentDictionary<FavoriteListKind, (SemaphoreSlim, CancellationTokenSource, List<FavoriteItem>?)>();

            this.favoritesProviders = favoritesProviders.ToArray();

            var providerTypeStr = settingService.GetSetting(Settings.UserSettingsContainer, "FavoriteProviderType", null);
            if (!Enum.TryParse(providerTypeStr, out providerType))
            {
                providerType = FavoriteProviderType.Local;
            }

            UpdateProvider();

            Settings.Instance.PropertyChanged += (value, args) =>
            {
                switch (args.PropertyName)
                {
                    case nameof(Settings.Instance.MainSite) when IsOnlineProviderUsed:
                        UpdateProvider();
                        break;
                }
            };
            userManager.UserLoggedIn += (_, __) => UpdateProvider();
            userManager.UserLoggedOut += _ => UpdateProvider();
        }

        /// <inheritdoc/>
        public event EventHandler<FavoriteChangedEventArgs>? FavoritesChanged;

        /// <inheritdoc/>
        public FavoriteProviderType ProviderType
        {
            get => providerType;
            set
            {
                if (providerType != value)
                {
                    providerType = value;
                    settingService.SetSetting(Settings.UserSettingsContainer, "FavoriteProviderType", value.ToString());
                    UpdateProvider();
                }
            }
        }

        public bool IsOnlineProviderUsed => ProviderType != FavoriteProviderType.Local && activeProvider != null;

        /// <inheritdoc/>
        public IEnumerable<FavoriteListKind> AvailableListKinds => IsOnlineProviderUsed
            ? activeProvider?.AvailableListKinds ?? Enumerable.Empty<FavoriteListKind>()
            : localFavoriteListKinds;

        /// <inheritdoc/>
        public async ValueTask<bool> AddToListAsync(ItemInfo item, FavoriteListKind listKind, CancellationToken cancellationToken)
        {
            if (item == null)
            {
                throw new ArgumentNullException(nameof(item));
            }
            if (IsOnlineProviderUsed && activeProvider == null)
            {
                throw new InvalidOperationException("Source is not setted up");
            }
            if (!AvailableListKinds.Contains(listKind))
            {
                throw new InvalidOperationException("Source doesn't contains specific list kind");
            }
            if (!IsSupportedByProvider(item))
            {
                throw new InvalidOperationException("Item is not supported by provider");
            }

            try
            {
                if (IsOnlineProviderUsed)
                {
                    activeSourceCache.TryGetValue(listKind, out var source);
                    if (source.list != null
                        && source.list.Any(f => f.ItemInfo == item))
                    {
                        return true;
                    }

                    source.list?.Insert(0, new FavoriteItem(item, listKind));
                }

                var tasks = new List<Task>();
                var eventArgs = new List<FavoriteChangedEventArgs>();

                if (listKind != FavoriteListKind.Favorites)
                {
                    // Any item could be added to FavoriteListKind.Favorites and to ONLY ONE another list - InProcess/Finished/ForLater
                    // If item is moved to any of InProcess/Finished/ForLater, it should be removed from other lists apart from Favorites.

                    tasks.AddRange(new[] { FavoriteListKind.ForLater, FavoriteListKind.InProcess, FavoriteListKind.Finished }
                        .Where(kind => kind != listKind)
                        .Select(async kind =>
                        {
                            var removed = false;
                            if (IsOnlineProviderUsed
                                && activeSourceCache.TryGetValue(kind, out var source)
                                && source.list != null)
                            {
                                var removedCount = source.list.RemoveAll(favoriteItem => favoriteItem.ItemInfo == item && favoriteItem.ListKind == kind);
                                if (removedCount > 0)
                                {
                                    removed = await RemoveItemInternal(item, kind, cancellationToken).ConfigureAwait(false);
                                }
                                return;
                            }
                            else
                            {
                                removed = await RemoveItemInternal(item, kind, cancellationToken).ConfigureAwait(false);
                            }
                            if (removed)
                            {
                                eventArgs.Add(new FavoriteChangedEventArgs(FavoriteItemChangedReason.Removed, kind));
                            }
                        }));
                }

                Task<bool> addTask;
                if (IsOnlineProviderUsed)
                {
                    tasks.Add(addTask = activeProvider!.AddAsync(item, listKind, cancellationToken));
                }
                else
                {
                    addTask = favoriteRepository
                        .UpsertManyAsync(new[] { new FavoriteItem(item, listKind) }).AsTask()
                        .ContinueWith(t => t.Status == TaskStatus.RanToCompletion && t.Result > 0, TaskScheduler.Default);
                    tasks.Add(addTask);
                }

                await Task.WhenAll(tasks).ConfigureAwait(false);
                if (await addTask.ConfigureAwait(false))
                {
                    eventArgs.Add(new FavoriteChangedEventArgs(FavoriteItemChangedReason.Added, listKind));
                }

                if (eventArgs.Count > 0
                    && FavoritesChanged is { } favoritesChanged)
                {
                    foreach (var args in eventArgs)
                    {
                        favoritesChanged.Invoke(this, args);
                    }
                    return true;
                }
                else
                {
                    return false;
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex);
                return false;
            }
        }

        /// <inheritdoc/>
        public async ValueTask<bool> RemoveFromListAsync(ItemInfo item, FavoriteListKind listKind, CancellationToken cancellationToken)
        {
            if (item == null)
            {
                throw new ArgumentNullException(nameof(item));
            }
            if (IsOnlineProviderUsed && activeProvider == null)
            {
                throw new InvalidOperationException("Source is not setted up");
            }
            if (!AvailableListKinds.Contains(listKind))
            {
                throw new InvalidOperationException("Source doesn't contains specific list kind");
            }

            try
            {
                if (IsOnlineProviderUsed)
                {
                    activeSourceCache.TryGetValue(listKind, out var source);
                    if (source.list != null
                        && source.list.All(f => f.ItemInfo != item))
                    {
                        return true;
                    }

                    source.list?.RemoveAll(favoriteItem => favoriteItem.ItemInfo == item && favoriteItem.ListKind == listKind);
                }

                var removed = await RemoveItemInternal(item, listKind, cancellationToken).ConfigureAwait(false);

                if (removed)
                {
                    FavoritesChanged?.Invoke(this, new FavoriteChangedEventArgs(FavoriteItemChangedReason.Removed, listKind));
                }

                return removed;
            }
            catch (Exception ex)
            {
                logger.LogError(ex);
                return false;
            }
        }

        /// <inheritdoc/>
        public bool IsSupportedByProvider(ItemInfo item)
        {
            return item != null
                && (!IsOnlineProviderUsed
                || activeProvider?.IsItemSupported(item) == true);
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            ClearProviderCache();
        }

        /// <inheritdoc/>
        public IAsyncEnumerable<FavoriteItem> GetFavorites(FavoriteListKind listKind)
        {
            if (!IsOnlineProviderUsed)
            {
                return favoriteRepository.GetAllByFavoriteListKind(listKind)
                    .SelectBatchAwait(10, async (item, ct) =>
                    {
                        try
                        {
                            var itemInfo = await providerManager.EnsureItemAsync(item.ItemInfo, ct).ConfigureAwait(false);
                            if (itemInfo == null)
                            {
                                return null;
                            }
                            return new FavoriteItem(itemInfo, item.ListKind);
                        }
                        catch (OperationCanceledException)
                        {
                            return item;
                        }
                    })
                    .Where(item => item != null)!;
            }
            else
            {
                return EnumerableHelper
                    .ToAsyncEnumerable(ct => LoadActiveAsync(listKind, ct))
                    .SelectMany(l => l.ToArray().ToAsyncEnumerable());
            }
        }

        /// <inheritdoc/>
        public IAsyncEnumerable<FavoriteItem> GetFavoritesByItems(IEnumerable<ItemInfo> items)
        {
            if (!IsOnlineProviderUsed)
            {
                return favoriteRepository.GetFavoritesByItems(items.Select(i => i.Key));
            }
            else
            {
                return AvailableListKinds
                    .ToAsyncEnumerable()
                    .SelectAwaitWithCancellation((ft, ct) => new ValueTask<List<FavoriteItem>>(LoadActiveAsync(ft, ct)))
                    .SelectMany(l => l.ToArray().ToAsyncEnumerable())
                    .Where(f => items.Any(i => i == f.ItemInfo))
                    .Distinct();
            }
        }

        private Task<bool> RemoveItemInternal(ItemInfo item, FavoriteListKind listKind, CancellationToken cancellationToken)
        {
            if (IsOnlineProviderUsed)
            {
                return activeProvider!.RemoveAsync(item, listKind, cancellationToken);
            }
            else
            {
                return favoriteRepository.DeleteAsync(new FavoriteItem(item, listKind).Key).AsTask();
            }
        }

        private void UpdateProvider()
        {
            if (ProviderType == FavoriteProviderType.Local)
            {
                activeProvider = null;
            }
            else
            {
                var site = Settings.Instance.MainSite;

                var provider = favoritesProviders.FirstOrDefault(p => p.Site == site);
                if (activeProvider == provider)
                {
                    return;
                }
                activeProvider = provider;
            }

            ClearProviderCache();

            FavoritesChanged?.Invoke(this, new FavoriteChangedEventArgs(FavoriteItemChangedReason.Reset, FavoriteListKind.None));
        }

        private void ClearProviderCache()
        {
            var values = activeSourceCache.Values.ToArray();
            activeSourceCache.Clear();

            foreach (var (semaphore, cts, _) in values)
            {
                if (cts != null)
                {
                    cts.Cancel();
                    cts.Dispose();
                }
                if (semaphore != null)
                {
                    semaphore.Release();
                }
            }
        }

        private async Task<List<FavoriteItem>> LoadActiveAsync(FavoriteListKind listKind, CancellationToken cancellationToken)
        {
            if (activeProvider == null)
            {
                return new List<FavoriteItem>();
            }

            var provider = activeProvider;

            try
            {
                var (semaphore, cts, list) = activeSourceCache.GetOrAdd(listKind, _ =>
                {
                    var cts = new CancellationTokenSource();
                    var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cts.Token, cancellationToken);
                    return (new SemaphoreSlim(1), linkedCts, null);
                });
                if (list != null)
                {
                    return list;
                }

                using var _ = await semaphore.LockAsync(cts.Token);
                if (cts.Token.IsCancellationRequested)
                {
                    return new List<FavoriteItem>();
                }
                if (activeSourceCache.TryGetValue(listKind, out var tuple)
                    && tuple.list != null)
                {
                    return tuple.list;
                }

                var isAllowed = await userManager
                    .CheckRequirementsAsync(provider.Site, ProviderRequirements.AccountForAny, cancellationToken)
                    .ConfigureAwait(false);
                if (!isAllowed)
                {
                    return new List<FavoriteItem>();
                }

                var source = await provider
                    .GetItemsAsync(listKind, cts.Token)
                    .ConfigureAwait(false);

                if (cts.Token.IsCancellationRequested)
                {
                    return new List<FavoriteItem>();
                }

                var convertedSource = source
                    .Select(item => new FavoriteItem(item, listKind))
                    .ToList();

                activeSourceCache.TryUpdate(listKind, (semaphore, cts, convertedSource), (semaphore, cts, list));
                FavoritesChanged?.Invoke(this, new FavoriteChangedEventArgs(FavoriteItemChangedReason.Reset, listKind));

                return convertedSource;
            }
            catch (OperationCanceledException)
            {
                return new List<FavoriteItem>();
            }
            catch (Exception ex)
            {
                ex.Data["FavoriteProvider"] = activeProvider?.GetType().FullName;
                logger?.LogError(ex);
                return new List<FavoriteItem>();
            }
        }

    }
}
