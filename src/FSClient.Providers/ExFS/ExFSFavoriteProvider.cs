namespace FSClient.Providers
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    using FSClient.Shared.Helpers.Web;
    using FSClient.Shared.Models;
    using FSClient.Shared.Providers;

    public class ExFSFavoriteProvider : IFavoriteProvider
    {
        private Task<ConcurrentDictionary<FavoriteListKind, List<ItemInfo>>>? cache;
        private string? userNickname;

        private readonly ExFSSiteProvider siteProvider;

        public ExFSFavoriteProvider(
            ExFSSiteProvider siteProvider)
        {
            this.siteProvider = siteProvider;
        }

        public Site Site => siteProvider.Site;

        public IEnumerable<FavoriteListKind> AvailableListKinds { get; } = new[]
        {
            FavoriteListKind.Favorites,
            FavoriteListKind.ForLater,
            FavoriteListKind.InProcess,
            FavoriteListKind.Finished
        };

        public async Task<IReadOnlyList<ItemInfo>> GetItemsAsync(FavoriteListKind listKind, CancellationToken cancellationToken)
        {
            var currentUserName = siteProvider.CurrentUser?.Nickname;

            if (!AvailableListKinds.Contains(listKind)
                || currentUserName == null)
            {
                return new List<ItemInfo>();
            }

            if (currentUserName != userNickname)
            {
                cache = null;
                userNickname = null;
            }

            if (cache == null
                || (cache.IsCompleted && !cache.Result.ContainsKey(listKind)))
            {
                cache = GetCacheAsync(currentUserName, cancellationToken);
                userNickname = currentUserName;
            }

            await cache.ConfigureAwait(false);

            if (!cache.Result.ContainsKey(listKind))
            {
                return new List<ItemInfo>();
            }

            if (cache.Result.TryRemove(listKind, out var items))
            {
                return items;
            }

            return new List<ItemInfo>();
        }

        private async Task<ConcurrentDictionary<FavoriteListKind, List<ItemInfo>>> GetCacheAsync(string userNickname, CancellationToken token)
        {
            var domain = await siteProvider.GetMirrorAsync(token).ConfigureAwait(false);
            var html = await siteProvider
                .HttpClient
                .GetBuilder(new Uri(domain, "/user/" + userNickname))
                .WithTimeSpanSemaphore(siteProvider.RequestSemaphore)
                .SendAsync(token)
                .AsHtml(token)
                .ConfigureAwait(false);
            if (html == null)
            {
                return new ConcurrentDictionary<FavoriteListKind, List<ItemInfo>>();
            }

            var dictionary = html.QuerySelectorAll(".tab-content .tab-pane")
                .Select(tab =>
                {
                    var tabId = tab.Id;
                    FavoriteListKind listKind;
                    if (tabId == "tab1")
                    {
                        listKind = FavoriteListKind.Favorites;
                    }
                    else if (tabId == "tab2")
                    {
                        listKind = FavoriteListKind.ForLater;
                    }
                    else if (tabId == "tab3")
                    {
                        listKind = FavoriteListKind.InProcess;
                    }
                    else if (tabId == "tab4")
                    {
                        listKind = FavoriteListKind.Finished;
                    }
                    else if (tabId == "tab5")
                    {
                        // listKind = FavoriteListKind.Recommended;
                        return default;
                    }
                    else
                    {
                        return default;
                    }

                    return (listKind, items: html
                        .GetElementById(tabId)?
                        .QuerySelectorAll(".MiniPostAllFormFav")
                        .Select(domItem =>
                        {
                            Uri.TryCreate(domain, domItem.QuerySelector("a")?.GetAttribute("href"), out var itemLink);

                            var itemId = ExFSSiteProvider.GetIdFromUrl(itemLink);

                            var itemTitle = domItem.QuerySelector(".MiniPostNameFav")?.TextContent?.Trim();

                            if (string.IsNullOrEmpty(itemId))
                            {
                                return null;
                            }

                            var itemSection = ExFSSiteProvider.GetSectionFromUrl(itemLink);

                            return new ItemInfo(Site, itemId)
                            {
                                Title = itemTitle,
                                Poster = ExFSSiteProvider.GetPoster(domain, domItem.QuerySelector("img")?.GetAttribute("src")) ?? default,
                                Link = itemLink,
                                Section = itemSection
                            };
                        })
                        .Where(i => i != null));
                })
                .Where(t => t.listKind != FavoriteListKind.None && t.items != null)
                .ToDictionary(t => t.listKind, t => t.items!.ToList());
            return new ConcurrentDictionary<FavoriteListKind, List<ItemInfo>>(dictionary!);
        }

        public async Task<bool> AddAsync(ItemInfo item, FavoriteListKind listKind, CancellationToken cancellationToken)
        {
            if (item.SiteId == null)
            {
                throw new NullReferenceException($"Item {nameof(item.SiteId)} must be set");
            }
            if (cache?.IsCompleted == true
                && cache.Result.ContainsKey(listKind)
                && !cache.Result[listKind].Contains(item))
            {
                cache.Result[listKind].Add(item);
            }

            var domain = await siteProvider.GetMirrorAsync(cancellationToken).ConfigureAwait(false);
            await siteProvider
                .HttpClient
                .GetBuilder(new Uri(domain, "/engine/ajax/favoritesall.php"))
                .WithArguments(new Dictionary<string, string?>
                {
                    ["film_id"] = item.SiteId,
                    ["statys"] = "plus",
                    ["fav"] = GetFavString(listKind) ?? string.Empty
                })
                .WithAjax()
                .WithTimeSpanSemaphore(siteProvider.RequestSemaphore)
                .SendAsync(cancellationToken)
                .ConfigureAwait(false);

            return true;
        }

        public async Task<bool> RemoveAsync(ItemInfo item, FavoriteListKind listKind, CancellationToken cancellationToken)
        {
            if (item.SiteId == null)
            {
                throw new NullReferenceException($"Item {nameof(item.SiteId)} must be set");
            }
            if (cache?.IsCompleted == true
                && cache.Result.ContainsKey(listKind)
                && cache.Result[listKind].Contains(item))
            {
                cache.Result[listKind].Remove(item);
            }

            var domain = await siteProvider.GetMirrorAsync(cancellationToken).ConfigureAwait(false);
            await siteProvider
                .HttpClient
                .GetBuilder(new Uri(domain, "/engine/ajax/favoritesall.php"))
                .WithArguments(new Dictionary<string, string?>
                {
                    ["film_id"] = item.SiteId,
                    ["statys"] = "minus",
                    ["fav"] = GetFavString(listKind) ?? string.Empty
                })
                .WithAjax()
                .WithTimeSpanSemaphore(siteProvider.RequestSemaphore)
                .SendAsync(cancellationToken)
                .ConfigureAwait(false);

            return true;
        }

        public bool IsItemSupported(ItemInfo item)
        {
            if (string.IsNullOrWhiteSpace(item.SiteId))
            {
                return false;
            }

            return item.Site == Site;
        }

        private static string? GetFavString(FavoriteListKind listKind)
        {
            return listKind switch
            {
                FavoriteListKind.Favorites => "favFilms",
                FavoriteListKind.Finished => "favWatched",
                FavoriteListKind.ForLater => "favWillLook",
                FavoriteListKind.InProcess => "favFindWeb",
                _ => null,
            };
        }
    }
}
