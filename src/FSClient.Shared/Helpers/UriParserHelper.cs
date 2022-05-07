namespace FSClient.Shared.Helpers
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    using FSClient.Shared.Models;
    using FSClient.Shared.Services;

    public static class UriParserHelper
    {
#if DEV_BUILD
        public const string AppProtocol = "fsclientdev";
#else
        public const string AppProtocol = "fsclient";
#endif
        private const string NodeIdsSeparator = "|";

        public static HistoryItem? GetHistoryItemFromArgs(IDictionary<string, string> args)
        {
            if (args.Count == 0)
            {
                return null;
            }

            if (args.TryGetValue("site", out var siteStr)
                && Site.TryParse(siteStr, out var site))
            {
                _ = args.TryGetValue("id", out var id);
                var link = args.TryGetValue("itemlink", out var linkStr) ? linkStr.ToUriOrNull()
                    : args.TryGetValue("link", out linkStr) ? linkStr.ToUriOrNull()
                    : null;

                var position = args.TryGetValue("position", out var posStr) ? posStr.ToFloatOrNull() ?? 0 : 0;
                var node = !args.TryGetValue("nodes", out var nodesStr) ? null : nodesStr
                    .Split(new[] { NodeIdsSeparator }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(n => new HistoryNode(n, 0))
                    .Aggregate((HistoryNode?)null, (parent, child) => { child.Parent = parent; return child; });
                if (node != null)
                {
                    node.Position = position;
                }

                var autoStart = args.TryGetValue("autostart", out var autoStartTemp) && autoStartTemp != "false";

                var episode = args.TryGetValue("e", out var episodeStr) ? episodeStr.ToIntOrNull() : null;
                var season = args.TryGetValue("s", out var seasonStr) ? seasonStr.ToIntOrNull() : null;

                var item = new ItemInfo(site, id)
                {
                    Link = link
                };
                return new HistoryItem(item, node)
                {
                    Season = season,
                    Episode = episode.ToRange(),
                    AutoStart = autoStart
                };
            }
            else if (args.TryGetValue("link", out var linkStr)
                && linkStr.ToHttpUriOrNull() is Uri link)
            {
                var item = new ItemInfo(Site.Any, null)
                {
                    Link = link
                };
                return new HistoryItem(item, null);
            }

            return null;
        }

        public static TitledTag GetTitledTagFromArgs(IDictionary<string, string> args)
        {
            if (args.Count == 0)
            {
                return TitledTag.Any;
            }

            _ = args.TryGetValue("value", out var value);
            _ = args.TryGetValue("title", out var title);

            return args.TryGetValue("type", out var type)
                && args.TryGetValue("site", out var site)
                && Site.TryParse(site, out var parsedSite)
                ? new TitledTag(title, parsedSite, type, value)
                : TitledTag.Any;
        }

        public static Uri GenerateUriFromNode(ITreeNode node, bool autoStart = true)
        {
            var args = new Dictionary<string, string?>();

            if (node != null)
            {
                if (node.Parent is IFolderTreeNode folder)
                {
                    if (folder.Site == Site.All)
                    {
                        args.Add("nodes", node.Site.Value + NodeIdsSeparator + node.Id);
                    }
                    else if (folder.GetIDsStack() is var idsStack)
                    {
                        args.Add("nodes", string.Join(NodeIdsSeparator, idsStack.Concat(new[] { node.Id })));
                    }
                }

                if (node is File file
                    && file.Position > 0)
                {
                    args.Add("position", $"{file.Position}");
                }

                if (node.ItemInfo is ItemInfo item)
                {
                    if (item.Site.IsSpecial
                        && item.Link is Uri link)
                    {
                        args.Add("link", link.ToString());
                    }
                    else if (item.SiteId is string id)
                    {
                        args.Add("site", item.Site.Value);
                        args.Add("id", id);
                    }
                }
            }

            if (autoStart)
            {
                args.Add("autostart", "");
            }

            return new Uri(AppProtocol + "://?" + QueryStringHelper.CreateQueryString(args));
        }

        public static Uri GenerateUriFromItemInfo(ItemInfo item)
        {
            var args = new Dictionary<string, string?>();
            if (item.Site.IsSpecial
                && item.Link?.ToString() is string linkStr)
            {
                args.Add("link", linkStr);
            }
            else
            {
                if (item.Site.Value is string siteValue)
                {
                    args.Add("site", siteValue);
                }

                if (item.SiteId is string siteId)
                {
                    args.Add("id", siteId);
                }
            }
            return new Uri(AppProtocol + "://?" + QueryStringHelper.CreateQueryString(args));
        }

        public static Uri? GenerateUriFromTitledTag(TitledTag titledTag)
        {
            if (titledTag == TitledTag.Any)
            {
                return null;
            }

            var args = new Dictionary<string, string?>
            {
                ["title"] = titledTag.Title,
                ["site"] = titledTag.Site.Value,
                ["type"] = titledTag.Type,
                ["value"] = titledTag.Value
            };

            return new Uri(AppProtocol + "://tag?" + QueryStringHelper.CreateQueryString(args));
        }

        public static string GetQueryStringFromViewModel(NavigationPageType pageType, IDictionary<string, string?>? parameters = null)
        {
            var pageStr = GetPageFromType(pageType, parameters);
            parameters ??= new Dictionary<string, string?>();

            parameters.Add("page", pageStr);

            return QueryStringHelper.CreateQueryString(parameters);
        }

        public static Uri GetProtocolUriFromViewModel(NavigationPageType pageType, IDictionary<string, string?>? parameters = null)
        {
            var pageStr = GetPageFromType(pageType, parameters);

            return parameters != null
                ? new Uri($"{AppProtocol}://{pageStr}?{QueryStringHelper.CreateQueryString(parameters)}")
                : new Uri($"{AppProtocol}://{pageStr}");
        }

        private static string? GetPageFromType(NavigationPageType pageType, IDictionary<string, string?>? parameters = null)
        {
            return pageType switch
            {
                NavigationPageType.Home => "home",
                NavigationPageType.Search => "search",
                NavigationPageType.Favorites => "fav",
                NavigationPageType.History => "history",
                NavigationPageType.Downloads => "downloads",
                NavigationPageType.Settings => "settings",
                NavigationPageType.Video when parameters?.ContainsKey("link") == true => "video",
                _ => null,
            };
        }
    }
}
