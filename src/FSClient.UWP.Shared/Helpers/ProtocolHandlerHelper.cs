namespace FSClient.UWP.Shared.Helpers
{
    using System;
    using System.Linq;

    using FSClient.Localization.Resources;
    using FSClient.Shared.Helpers;
    using FSClient.Shared.Models;
    using FSClient.UWP.Shared.Services;
    using FSClient.UWP.Shared.Views.Pages;
    using FSClient.UWP.Shared.Views.Pages.Parameters;

    public static class ProtocolHandlerHelper
    {
        public static NavigationItem? GetNavigationItemFromUri(Uri argsUri)
        {
            if (argsUri == null)
            {
                return null;
            }

            var args = QueryStringHelper
                .ParseQuery(argsUri.Query)
                .ToDictionary(k => k.Key.ToLower(), v => v.Value);

            if (args.TryGetValue("page", out var argsPage)
                || !string.IsNullOrEmpty(argsUri.Host))
            {
                var page = string.IsNullOrEmpty(argsPage) ? argsUri.Host : argsPage;
                switch (page)
                {
                    case "home":
                        return new NavigationItem<HomePage>(Strings.NavigationPageType_Home);
                    case "search"
                        when args.TryGetValue("request", out var request):
                        var site = Site.Any;
                        if (args.TryGetValue("site", out var siteValue))
                        {
                            site = Site.Parse(siteValue);
                        }

                        return new NavigationItem<SearchPage>(Strings.NavigationPageType_Search)
                        {
                            Parameter = new SearchPageParameter(request, site)
                        };
                    case "video"
                        when args.TryGetValue("link", out var videoLink)
                             && videoLink.ToHttpUriOrNull() is Uri videoUri:
                        return new NavigationItem<VideoPage>(Strings.NavigationPageType_Video) {Parameter = videoUri};
                    case "tag"
                        when UriParserHelper.GetTitledTagFromArgs(args) is TitledTag tag
                             && tag != TitledTag.Any:
                        return new NavigationItem<ItemsByTagPage>(Strings.NavigationPageType_ItemsByTag) {Parameter = tag};
                    case "search":
                        return new NavigationItem<SearchPage>(Strings.NavigationPageType_Search);
                    case "fav":
                        return new NavigationItem<FavoritesPage>(Strings.NavigationPageType_Favorites);
                    case "history":
                        return new NavigationItem<HistoryPage>(Strings.NavigationPageType_History);
                    case "last":
                        return new NavigationItem<ItemPage>(Strings.NavigationPageType_LastWatched);
                    case "downloads":
                        return new NavigationItem<DownloadsPage>(Strings.NavigationPageType_Downloads);
                    case "settings":
                        return new NavigationItem<SettingsPage>(Strings.NavigationPageType_Settings);
                }
            }

            if (!string.IsNullOrEmpty(argsUri.Host)
                && argsUri.Host.Contains('.')
                && Uri.TryCreate(argsUri.ToString().Replace(UriParserHelper.AppProtocol, "http"), UriKind.Absolute,
                    out var link))
            {
                var item = new ItemInfo(Site.Any, null) {Link = link};
                return new NavigationItem<ItemPage>(Strings.NavigationPageType_ItemInfo)
                {
                    Parameter = new HistoryItem(item, null)
                };
            }

            if (UriParserHelper.GetHistoryItemFromArgs(args) is HistoryItem historyItem)
            {
                return new NavigationItem<ItemPage>(Strings.NavigationPageType_ItemInfo) {Parameter = historyItem};
            }

            return null;
        }
    }
}
