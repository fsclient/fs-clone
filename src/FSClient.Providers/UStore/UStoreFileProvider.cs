namespace FSClient.Providers
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    using FSClient.Shared;
    using FSClient.Shared.Helpers;
    using FSClient.Shared.Helpers.Web;
    using FSClient.Shared.Models;
    using FSClient.Shared.Providers;
    using FSClient.Shared.Services;

    using Jint;

    using Microsoft.Extensions.Logging;

    using Newtonsoft.Json.Linq;

    using Nito.AsyncEx;

#pragma warning disable CA1001 // Types that own disposable fields should be disposable
    public class UStoreFileProvider : IFileProvider
#pragma warning restore CA1001 // Types that own disposable fields should be disposable
    {
        private readonly UStoreSiteProvider siteProvider;
        private readonly SemaphoreSlim loaderSemaphore;
        private readonly ILogger logger;
        private string? mitmScriptCache;

        public UStoreFileProvider(
            UStoreSiteProvider siteProvider,
            ILogger logger)
        {
            this.siteProvider = siteProvider;
            this.logger = logger;

            loaderSemaphore = new SemaphoreSlim(1);
        }

        public Site Site => siteProvider.Site;

        public bool ProvideOnline => true;

        public bool ProvideTorrent => false;

        public bool ProvideTrailers => false;

        public ProviderRequirements ReadRequirements => ProviderRequirements.None;

        private IEnumerable<ItemInfo>? currentItems;

        public void InitForItems(IEnumerable<ItemInfo> items)
        {
            currentItems = items;
        }

        public async Task<IEnumerable<ITreeNode>> GetFolderChildrenAsync(Folder folder, CancellationToken token)
        {
            if (folder == null)
            {
                throw new ArgumentNullException(nameof(folder));
            }

            if (folder.Count == 0 && currentItems != null)
            {
                var rootFolders = await currentItems
                    .ToAsyncEnumerable()
                    .WhenAll(async (item, ct) => (item, translates: await LoadRootFromItemsAsync(item, ct).ConfigureAwait(false)))
                    .Select(tuple =>
                    {
                        var itemFolder = new Folder(Site, $"ust{tuple.item.SiteId}", FolderType.Item, PositionBehavior.Max)
                        {
                            Title = tuple.item.Title
                        };
                        itemFolder.AddRange(tuple.translates);
                        return itemFolder;
                    })
                    .ToArrayAsync(token)
                    .ConfigureAwait(false);

                // Unwind items
                if (rootFolders.Length == 1)
                {
                    return rootFolders[0];
                }
                return rootFolders;
            }

            return Enumerable.Empty<ITreeNode>();
        }

        private async Task<IEnumerable<ITreeNode>> LoadRootFromItemsAsync(ItemInfo currentItem, CancellationToken cancellationToken)
        {
            var domain = await siteProvider.GetMirrorAsync(cancellationToken).ConfigureAwait(false);
            currentItem.Link = new Uri(domain, currentItem.Link);

            var apiHash = currentItem.Link?.Segments.Skip(2).FirstOrDefault().Trim('/') ?? Secrets.UStoreApiKey;
            if (apiHash == null)
            {
                return Enumerable.Empty<ITreeNode>();
            }

            if (currentItem is UStoreItemInfo uStoreItemInfo)
            {
                return LoadRootFromItem(uStoreItemInfo, uStoreItemInfo.EpisodesPerSeasonsPerTranslation, apiHash);
            }
            else
            {
                var text = await siteProvider.HttpClient
                    .GetBuilder(new Uri(domain, currentItem.Link))
                    .WithHeader("Origin", domain.GetOrigin())
                    .WithHeader("Referer", domain.ToString())
                    .SendAsync(cancellationToken)
                    .AsText()
                    .ConfigureAwait(false);
                if (text == null)
                {
                    return Enumerable.Empty<ITreeNode>();
                }

                var playlistIndex = text.IndexOf("\"playlist\":", StringComparison.Ordinal);
                if (playlistIndex > 0)
                {
                    var playlistArrayStart = text.IndexOf('[', playlistIndex);
                    if (playlistArrayStart < 0)
                    {
                        return Enumerable.Empty<ITreeNode>();
                    }

                    var playlistArrayEnd = text.IndexOf(']', playlistArrayStart);
                    if (playlistArrayEnd < 0)
                    {
                        return Enumerable.Empty<ITreeNode>();
                    }

                    var playlist = JsonHelper.ParseOrNull<JArray>(text[playlistArrayStart..(playlistArrayEnd + 1)]);
                    var parserPlaylist = (playlist ?? new JArray())
                        .OfType<JObject>()
                        .Where(j => j["translate"] != null)
                        .ToDictionary(
                            key => key["translate"]!.ToString(),
                            value => (IReadOnlyDictionary<int, IReadOnlyCollection<string>>)
                                (value["data"] as JObject ?? new JObject())
                                .OfType<JProperty>()
                                .ToDictionary(
                                    key => key.Name.ToIntOrNull() ?? 1,
                                    value => (IReadOnlyCollection<string>)
                                        (value.Value as JObject ?? new JObject())
                                        .OfType<JProperty>()
                                        .Select(v => v.Value.ToString())
                                        .Where(v => v != null)
                                        .ToArray()));

                    return LoadRootFromItem(currentItem, parserPlaylist, apiHash);
                }
                else
                {
                    var fileHash = currentItem.Link?.Segments.Skip(3).FirstOrDefault().Trim('/');
                    if (fileHash == null)
                    {
                        return Enumerable.Empty<ITreeNode>();
                    }

                    var translates = new Dictionary<string, IReadOnlyDictionary<int, IReadOnlyCollection<string>>>
                    {
                        [currentItem.Title ?? string.Empty] = new Dictionary<int, IReadOnlyCollection<string>>
                        {
                            [0] = new[] { fileHash }
                        }
                    };

                    return LoadRootFromItem(currentItem, translates, apiHash);
                }
            }
        }

        private IEnumerable<ITreeNode> LoadRootFromItem(ItemInfo item, IReadOnlyDictionary<string, IReadOnlyDictionary<int, IReadOnlyCollection<string>>> dict, string apiHash)
        {
            return dict
               .SelectMany(translatePair => translatePair.Value
                   .SelectMany(seasonPair => seasonPair.Value.Select(episode => (
                       itemId: item.SiteId,
                       translateId: translatePair.Key.GetDeterministicHashCode(),
                       translate: translatePair.Key,
                       season: seasonPair.Key,
                       fileHash: episode
                   ))))
               .GroupBy(tuple => (tuple.itemId, tuple.season))
               .OrderBy(group => group.Key.season)
               .SelectMany(group =>
               {
                   var translatesOrFiles = group.GroupBy(t => (t.translateId, t.translate))
                       .Select(gt =>
                       {
                           var files = gt.Select((t, ep) =>
                           {
                               var file = new File(Site, $"ust{group.Key.itemId}_{group.Key.season}_{gt.Key.translateId}_{t.fileHash}");
                               file.FrameLink = item.Link;
                               file.ItemTitle = item.Title;
                               if (group.Key.season > 0)
                               {
                                   file.Season = group.Key.season;
                                   file.Episode = (ep + 1).ToRange();
                               }
                               else
                               {
                                   file.Title = gt.Key.translate;
                               }
                               file.SetVideosFactory((f, ct) => LoadVideosAsync(f, apiHash, t.fileHash, ct));
                               return file;
                           });

                           var translateFolder = new Folder(Site,
                               $"ust{group.Key.itemId}_{group.Key.season}_{gt.Key.translateId}", FolderType.Translate, PositionBehavior.Average)
                           {
                               Title = gt.Key.translate,
                               Season = group.Key.season
                           };
                           translateFolder.AddRange(files);
                           return (ITreeNode)translateFolder;
                       })
                       .ToArray();

                   if (translatesOrFiles.Length == 1)
                   {
                       translatesOrFiles = ((Folder)translatesOrFiles[0]).ItemsSource.ToArray();
                   }

                   if (group.Key.season == 0)
                   {
                       return translatesOrFiles;
                   }
                   else
                   {
                       var seasonFolder = new Folder(Site, $"ust{group.Key.itemId}_{group.Key.season}", FolderType.Season, PositionBehavior.Max)
                       {
                           Title = "Сезон " + group.Key.season,
                           Season = group.Key.season
                       };
                       seasonFolder.AddRange(translatesOrFiles);
                       return new[] { (ITreeNode)seasonFolder };
                   }
               });
        }

        private async Task<IEnumerable<Video>> LoadVideosAsync(File file, string apiHash, string epHash, CancellationToken cancellationToken)
        {
            if (file.FrameLink == null)
            {
                throw new InvalidOperationException("Invalid file. FrameLink is null.");
            }

            var referer = siteProvider.Properties[UStoreSiteProvider.UStoreRefererKey] ?? file.FrameLink.ToString();
            var playerLink = siteProvider.Properties[UStoreSiteProvider.UStorePlayerLinkKey];
            string? relativeLink = null;

            if (!siteProvider.Properties.TryGetValue(UStoreSiteProvider.UStoreSecurityKeyKey, out var securityKeyJson)
                || JsonHelper.ParseOrNull<JArray>(securityKeyJson) is not JArray securityKeyArray
                || securityKeyArray[0]?.ToString() is not string s1
                || securityKeyArray[1]?.ToString() is not string s2)
            {
                var tuple = await UPlayerKeysFether.FetchKeysAsync(siteProvider, new Uri(file.FrameLink, playerLink), cancellationToken).ConfigureAwait(false);
                if (tuple.HasValue)
                {
                    (relativeLink, s1, s2) = tuple.Value;
                }
                else
                {
                    s1 = string.Empty;
                    s2 = string.Empty;
                }
            }
            if (!siteProvider.Properties.TryGetValue(UStoreSiteProvider.UStoreSecurityKey2Key, out var securityKey2)
                || JsonHelper.ParseOrNull<JArray>(securityKey2) is not JArray securityKey2Array
                || securityKey2Array[0]?.ToString() is not string s12
                || securityKey2Array[1]?.ToString() is not string s22)
            {
                s12 = string.Empty;
                s22 = string.Empty;
            }

            if (siteProvider.Properties.ContainsKey(UStoreSiteProvider.UStoreVideoDetailsLinkKey))
            {
                relativeLink = siteProvider.Properties[UStoreSiteProvider.UStoreVideoDetailsLinkKey];
            }

            if (string.IsNullOrEmpty(s1) || string.IsNullOrEmpty(s2) || string.IsNullOrEmpty(relativeLink))
            {
                return Enumerable.Empty<Video>();
            }

            var content = await siteProvider.HttpClient
                // Relative link includes "?hash=" part, but we duplicate it just for case.
                .GetBuilder(new Uri(file.FrameLink, relativeLink + apiHash))
                .WithArgument("hash", apiHash)
                .WithArgument("id", epHash)
                .WithHeader("Origin", file.FrameLink.GetOrigin())
                .WithHeader("Referer", referer)
                .SendAsync(cancellationToken)
                .AsNewtonsoftJson<JObject>()
                .ConfigureAwait(false);
            if (content == null
                || content["url"] is not JArray urls)
            {
                return Enumerable.Empty<Video>();
            }

            if (content["subtitles"] is JArray subtitles)
            {
                file.SubtitleTracks.AddRange(subtitles
                    .Select(subtitlesJson => (subtitlesJson, link: subtitlesJson["src"]?.ToUriOrNull()))
                    .Where(t => t.link != null && t.subtitlesJson["kind"]?.ToString() == "captions")
                    .Select(t => new SubtitleTrack(t.subtitlesJson["lang"]?.ToString(), t.link!)
                    {
                        Title = t.subtitlesJson["label"]?.ToString()
                    })
                    .ToArray());
            }

            var qualities = new[] { 360, 480, 720, 1080, 1440, 2160 };

            var mitmScript = await GetMitmScriptCodeAsync().ConfigureAwait(false);

            return urls
                .Take(qualities.Length)
                .Select((url, index) => (
                    uri: Decode(url.ToString(), s1, s2, s12, s22, mitmScript)?.ToUriOrNull(),
                    qual: qualities[index]))
                .Where(t => t.uri != null && t.uri.IsAbsoluteUri)
                .GroupBy(t => t.uri!.OriginalString)
                .Select(g => g.OrderBy(t => t.qual).First())
                .Select(t => new Video(t.uri!)
                {
                    Quality = t.qual
                });
        }

        private string? Decode(string input, string? s1, string? s2, string? s12, string? s22, string? mitmScript)
        {
            try
            {
                if (string.IsNullOrEmpty(input)
                    || input[0] != '=')
                {
                    return input;
                }

                if (s1 != null && s2 != null
                    && s12 != null && s22 != null)
                {
                    try
                    {
                        // https://hms.lostcut.net/viewtopic.php?pid=17014#p17014
                        var data = input[1..];
                        for (var i = 0; i < s1.Length; i++)
                        {
                            data = data.Replace(s1[i].ToString(), "__");
                            data = data.Replace(s2[i], s1[i]);
                            data = data.Replace("__", s2[i].ToString());
                        }

                        if (data.Length % 4 != 0)
                        {
                            data += new string('=', 4 - (data.Length % 4));
                        }
                        var decodedBytes = Convert.FromBase64String(data);
                        data = Encoding.UTF8.GetString(decodedBytes);
                        data = WebUtility.UrlDecode(data);

                        for (var i = 0; i < s12.Length; i++)
                        {
                            data = data.Replace(s12[i].ToString(), "__");
                            data = data.Replace(s22[i], s12[i]);
                            data = data.Replace("__", s22[i].ToString());
                        }

                        if (data.Length % 4 != 0)
                        {
                            data += new string('=', 4 - (data.Length % 4));
                        }
                        decodedBytes = Convert.FromBase64String(data);
                        data = Encoding.UTF8.GetString(decodedBytes);
                        data = WebUtility.UrlDecode(data);

                        const string base36CharList = "0123456789abcdefghijklmnopqrstuvwxyz";
                        var decoded = new StringBuilder(data.Length);
                        for (var i = 0; i < data.Length; i += 2)
                        {
                            if (data[i] != '!')
                            {
                                break;
                            }
                            i++;
                            decoded.Append((char)((36 * base36CharList.IndexOf(data[i])) + base36CharList.IndexOf(data[i + 1])));
                        }
                        return decoded.ToString();
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(ex);
                    }
                }

                return new Engine()
                    .SetupBrowserFunctions()
                    .Execute("var window = this;")
                    .SetValue("securityKey", new[] { s1, s2 })
                    .SetValue("securityKey2", new[] { s12, s22 })
                    .Execute($"window.securityKey = securityKey")
                    .Execute($"window.securityKey2 = securityKey2")
                    .Execute(mitmScript)
                    .SetValue("g_input", input)
                    .Execute("decode(g_input)")
                    .GetCompletionValue()
                    .AsString();
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex);
                return null;
            }
        }

        public Task<Folder?> GetTorrentsRootAsync(ItemInfo itemInfo, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<Folder?> GetTrailersRootAsync(ItemInfo itemInfo, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        private async Task<string?> GetMitmScriptCodeAsync()
        {
            var fallback = GetType().ReadResourceFromTypeAssembly("UStoreMitmScript.inline.js");

            if (!siteProvider.Properties.TryGetValue(UStoreSiteProvider.UStoreMitmScriptLinkKey, out var mitmScriptLink))
            {
                return fallback;
            }

            if (mitmScriptCache != null)
            {
                return mitmScriptCache;
            }

            using var _ = await loaderSemaphore.LockAsync().ConfigureAwait(false);

            if (mitmScriptCache != null)
            {
                return mitmScriptCache;
            }

            mitmScriptCache = await siteProvider.HttpClient
                .GetBuilder(new Uri(mitmScriptLink))
                .SendAsync(default)
                .AsText()
                .ConfigureAwait(false);
            return mitmScriptCache ?? fallback;
        }
    }
}
