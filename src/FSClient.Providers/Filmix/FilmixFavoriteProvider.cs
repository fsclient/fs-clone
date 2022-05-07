namespace FSClient.Providers
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    using AngleSharp.Dom;
    using AngleSharp.Html.Dom;

    using FSClient.Shared.Helpers;
    using FSClient.Shared.Helpers.Web;
    using FSClient.Shared.Models;
    using FSClient.Shared.Providers;

    public class FilmixFavoriteProvider : IFavoriteProvider
    {
        private const int ItemsPerPage = 60;

        private readonly FilmixSiteProvider siteProvider;

        public FilmixFavoriteProvider(
            FilmixSiteProvider siteProvider)
        {
            this.siteProvider = siteProvider;
        }

        public IEnumerable<FavoriteListKind> AvailableListKinds { get; } = new[]
        {
            FavoriteListKind.Favorites,
            FavoriteListKind.ForLater,
            FavoriteListKind.InProcess
        };

        public Site Site => siteProvider.Site;

        public async Task<IReadOnlyList<ItemInfo>> GetItemsAsync(FavoriteListKind listKind, CancellationToken cancellationToken)
        {
            var domain = await siteProvider.GetMirrorAsync(cancellationToken).ConfigureAwait(false);

            siteProvider.Handler.SetCookie(domain, "per_page_news", ItemsPerPage.ToString());

            var htmlResult = await GetItemsAsync(listKind, 1, cancellationToken).ConfigureAwait(false);

            if (htmlResult == null)
            {
                return new List<ItemInfo>();
            }

            var maxPage = htmlResult
                .QuerySelectorAll(".navigation a[data-number]")
                .Select(a => int.TryParse(a.GetAttribute("data-number"), out var p) ? p : -1)
                .Concat(new[] { 1 })
                .Max();

            var items = (await Task
                .WhenAll(Enumerable
                .Range(2, maxPage - 1)
                .Select(p => GetItemsAsync(listKind, p, cancellationToken))).ConfigureAwait(false))
                .Select(html => html?.QuerySelectorAll(".shortstory") ?? Enumerable.Empty<IElement>())
                .SelectMany(stories => stories);

            return htmlResult
                .QuerySelectorAll(".shortstory")
                .Concat(items)
                .Select(i => FilmixSiteProvider.ParseElement(domain, i))
                .Where(i => i != null)
                .ToList()!;

            Task<IHtmlDocument?> GetItemsAsync(FavoriteListKind inType, int page, CancellationToken ct)
            {
                switch (inType)
                {
                    case FavoriteListKind.Favorites:
                    case FavoriteListKind.ForLater:
                        return GetFavAndPTWAsync(inType, page, ct);
                    case FavoriteListKind.InProcess:
                        return GetInProccessAsync(page, ct);
                    case FavoriteListKind.Finished:
                    default:
                        return Task.FromResult<IHtmlDocument?>(null);
                }
            }

            Task<IHtmlDocument?> GetInProccessAsync(int page, CancellationToken ct)
            {
                var args = new Dictionary<string, string>();
                if (page > 1)
                {
                    args.Add("cstart", page.ToString());
                }

                return siteProvider
                    .HttpClient
                    .PostBuilder(new Uri(domain, "engine/ajax/saved.php"))
                    .WithTimeSpanSemaphore(siteProvider.RequestSemaphore)
                    .WithBody(args)
                    .WithHeader("Referer", domain.ToString())
                    .WithAjax()
                    .SendAsync(ct)
                    .AsHtml(cancellationToken);
            }

            Task<IHtmlDocument?> GetFavAndPTWAsync(FavoriteListKind inType, int page, CancellationToken ct)
            {
                var typeStr = GetFavString(inType);
                if (typeStr == null)
                {
                    return Task.FromResult<IHtmlDocument?>(null);
                }

                var args = new Dictionary<string, string?>
                {
                    ["do"] = typeStr
                };
                if (page > 1)
                {
                    args.Add("cstart", page.ToString());
                }

                return siteProvider
                    .HttpClient
                    .GetBuilder(new Uri(domain, "loader.php"))
                    .WithTimeSpanSemaphore(siteProvider.RequestSemaphore)
                    .WithArguments(args)
                    .WithHeader("Referer", domain.ToString())
                    .WithAjax()
                    .SendAsync(ct)
                    .AsHtml(cancellationToken);
            }
        }

        public async Task<bool> AddAsync(ItemInfo item, FavoriteListKind listKind, CancellationToken cancellationToken)
        {
            if (item.SiteId == null)
            {
                throw new NullReferenceException($"Item {nameof(item.SiteId)} must be set");
            }
            var domain = await siteProvider.GetMirrorAsync(cancellationToken).ConfigureAwait(false);

            switch (listKind)
            {
                case FavoriteListKind.Favorites:
                    await siteProvider
                        .HttpClient
                        .GetBuilder(new Uri(domain, "engine/ajax/favorites.php"))
                        .WithTimeSpanSemaphore(siteProvider.RequestSemaphore)
                        .WithArguments(new Dictionary<string, string?>
                        {
                            ["fav_id"] = item.SiteId,
                            ["action"] = "plus",
                            ["skin"] = "Filmix",
                            ["alert"] = "0"
                        })
                        .WithHeader("Origin", domain.GetOrigin())
                        .WithHeader("Referer", domain.ToString())
                        .WithAjax()
                        .SendAsync(cancellationToken)
                        .ConfigureAwait(false);
                    break;
                case FavoriteListKind.InProcess:
                    await siteProvider
                        .HttpClient
                        .PostBuilder(new Uri(domain, "api/v2/movie/view_time"))
                        .WithTimeSpanSemaphore(siteProvider.RequestSemaphore)
                        .WithBody(new Dictionary<string, string>
                        {
                            ["add_item"] = item.SiteId
                        })
                        .WithHeader("Origin", domain.GetOrigin())
                        .WithHeader("Referer", new Uri(domain, $"/play/{item.SiteId}").ToString())
                        .WithAjax()
                        .SendAsync(cancellationToken)
                        .ConfigureAwait(false);
                    break;
                default:
                    var typeStr = GetFavString(listKind);
                    if (typeStr == null)
                    {
                        return false;
                    }

                    await siteProvider
                        .HttpClient
                        .PostBuilder(new Uri(domain, "engine/ajax/" + typeStr + ".php"))
                        .WithTimeSpanSemaphore(siteProvider.RequestSemaphore)
                        .WithBody(new Dictionary<string, string>
                        {
                            ["post_id"] = item.SiteId,
                            ["movie_id"] = item.SiteId,
                            ["action"] = "add"
                        })
                        .WithHeader("Origin", domain.GetOrigin())
                        .WithHeader("Referer", domain.ToString())
                        .WithAjax()
                        .SendAsync(cancellationToken)
                        .ConfigureAwait(false);
                    break;
            }

            return true;
        }

        public async Task<bool> RemoveAsync(ItemInfo item, FavoriteListKind listKind, CancellationToken cancellationToken)
        {
            if (item.SiteId == null)
            {
                throw new NullReferenceException($"Item {nameof(item.SiteId)} must be set");
            }
            var domain = await siteProvider.GetMirrorAsync(cancellationToken).ConfigureAwait(false);

            switch (listKind)
            {
                case FavoriteListKind.Favorites:
                    await siteProvider
                        .HttpClient
                        .GetBuilder(new Uri(domain, "engine/ajax/favorites.php"))
                        .WithTimeSpanSemaphore(siteProvider.RequestSemaphore)
                        .WithArguments(new Dictionary<string, string?>
                        {
                            ["fav_id"] = item.SiteId,
                            ["action"] = "minus",
                            ["skin"] = "Filmix",
                            ["alert"] = "0"
                        })
                        .WithHeader("Origin", domain.GetOrigin())
                        .WithHeader("Referer", domain.ToString())
                        .WithAjax()
                        .SendAsync(cancellationToken)
                        .ConfigureAwait(false);
                    break;
                case FavoriteListKind.InProcess:
                    await siteProvider
                        .HttpClient
                        .PostBuilder(new Uri(domain, "api/movies/rm_time"))
                        .WithTimeSpanSemaphore(siteProvider.RequestSemaphore)
                        .WithBody(new Dictionary<string, string>
                        {
                            ["movie_id"] = item.SiteId
                        })
                        .WithHeader("Origin", domain.GetOrigin())
                        .WithHeader("Referer", new Uri(domain, "/saved").ToString())
                        .WithAjax()
                        .SendAsync(cancellationToken)
                        .ConfigureAwait(false);
                    break;
                default:
                    var typeStr = GetFavString(listKind);
                    if (typeStr == null)
                    {
                        return false;
                    }

                    await siteProvider
                        .HttpClient
                        .PostBuilder(new Uri(domain, "engine/ajax/" + typeStr + ".php"))
                        .WithTimeSpanSemaphore(siteProvider.RequestSemaphore)
                        .WithBody(new Dictionary<string, string>
                        {
                            ["post_id"] = item.SiteId,
                            ["movie_id"] = item.SiteId,
                            ["action"] = "rm"
                        })
                        .WithHeader("Origin", domain.GetOrigin())
                        .WithHeader("Referer", domain.ToString())
                        .WithAjax()
                        .SendAsync(cancellationToken)
                        .ConfigureAwait(false);
                    break;
            }
            return true;
        }

        public bool IsItemSupported(ItemInfo item)
        {
            if (string.IsNullOrWhiteSpace(item.SiteId))
            {
                return false;
            }

            return item.Site == siteProvider.Site;
        }

        private static string? GetFavString(FavoriteListKind listKind)
        {
            return listKind switch
            {
                FavoriteListKind.Favorites => "favorites",
                FavoriteListKind.ForLater => "watch_later",
                FavoriteListKind.InProcess => "saved",
                _ => null,
            };
        }
    }
}
