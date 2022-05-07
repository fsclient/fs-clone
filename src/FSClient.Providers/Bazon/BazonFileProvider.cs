namespace FSClient.Providers
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    using FSClient.Shared.Helpers;
    using FSClient.Shared.Helpers.Web;
    using FSClient.Shared.Models;
    using FSClient.Shared.Providers;
    using FSClient.Shared.Services;

    using Microsoft.Extensions.Logging;

    using Newtonsoft.Json.Linq;

    using Nito.AsyncEx;

    public sealed class BazonFileProvider : IFileProvider, IDisposable
    {
        private readonly BazonSiteProvider siteProvider;
        private readonly PlayerJsParserService playerJsParserService;
        private readonly ILogger logger;
        private readonly SemaphoreSlim loaderSemaphore;
        private readonly ConcurrentDictionary<string, (SemaphoreSlim semaphore, string? fileMetadata)> cache;

        private string? loaderJsCodeCache;
        private string? mitmScriptCache;

        public BazonFileProvider(
            BazonSiteProvider siteProvider,
            PlayerJsParserService playerJsParserService,
            ILogger logger)
        {
            this.siteProvider = siteProvider;
            this.playerJsParserService = playerJsParserService;
            this.logger = logger;

            loaderSemaphore = new SemaphoreSlim(1);
            cache = new ConcurrentDictionary<string, (SemaphoreSlim semaphore, string? fileMetadata)>();
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
                cache.Clear();

                var items = (await LoadRootFromItemsAsync(currentItems, token).ConfigureAwait(false)).ToList();
                if (items.Count == 1
                    && items[0] is Folder singleRootFolder)
                {
                    return singleRootFolder;
                }
                else
                {
                    return items;
                }
            }

            return Enumerable.Empty<ITreeNode>();
        }

        private async Task<IEnumerable<ITreeNode>> LoadRootFromItemsAsync(IEnumerable<ItemInfo> items, CancellationToken cancellationToken)
        {
            var domain = await siteProvider.GetMirrorAsync(cancellationToken).ConfigureAwait(false);

            var bazonIitems = items.Where(i => !string.IsNullOrEmpty(i?.SiteId));

            var films = bazonIitems.Where(i => i is not BazonItemInfo bazonItemInfo || bazonItemInfo.EpisodesPerSeasons.Count == 0).Select(i =>
            {
                var bazonItemInfo = i as BazonItemInfo;
                var id = $"bzn{i.SiteId}" + (bazonItemInfo?.TranslationId is int translationId ? $"__{translationId}" : string.Empty);
                var file = new File(Site, id)
                {
                    Title = bazonItemInfo?.Translation ?? i.Title,
                    ItemTitle = i.Title,
                    FrameLink = new Uri(domain, i.Link)
                };
                file.SetVideosFactory((f, ct) => LoadVideosAsync(f, id, ct));

                return (ITreeNode)file;
            });

            var seasons = bazonIitems
                .OfType<BazonItemInfo>()
                .Where(i => i.EpisodesPerSeasons.Count > 0)
                .SelectMany(i => i.EpisodesPerSeasons
                .SelectMany(season =>
                {
                    var seasonId = $"{i.SiteId}_{season.Key}_{i.TranslationId}";

                    return season.Value.Select(episode =>
                    {
                        var episodeFile = new File(Site, $"bzn{seasonId}_{episode.episode}")
                        {
                            Episode = episode.episode.ToRange(),
                            Season = season.Key,
                            ItemTitle = i.Title,
                            FrameLink = new Uri(domain, i.Link)
                        };
                        episodeFile.SetVideosFactory((f, ct) => LoadVideosAsync(f, i.SiteId + i.Translation, ct));

                        return (itemId: i.SiteId, translateId: i.TranslationId, translate: i.Translation, season: season.Key, file: episodeFile);
                    });
                }))
                .Where(tuple => tuple.file?.Id != null)
                .GroupBy(tuple => (tuple.itemId, tuple.season))
                .OrderBy(group => group.Key.season)
                .SelectMany(group =>
                {
                    var seasonFolder = new Folder(Site, $"bzn{group.Key.itemId}_{group.Key.season}", FolderType.Season, PositionBehavior.Max)
                    {
                        Title = "Сезон " + group.Key.season,
                        Season = group.Key.season
                    };
                    seasonFolder.AddRange(group.GroupBy(t => (t.translateId, t.translate))
                        .Select(gt =>
                        {
                            var translateFolder = new Folder(Site,
                                $"bzn{group.Key.itemId}_{group.Key.season}_{gt.Key.translateId}", FolderType.Translate, PositionBehavior.Average)
                            {
                                Title = gt.Key.translate,
                                Season = group.Key.season
                            };
                            translateFolder.AddRange(gt.Select(t => t.file));
                            return translateFolder;
                        }));
                    return new[] { (ITreeNode)seasonFolder };
                })!;

            return films.Concat(seasons);
        }

        private async Task<IEnumerable<Video>> LoadVideosAsync(File file, string bazonId, CancellationToken cancellationToken)
        {
            var (semaphore, fileMetadata) = cache.GetOrAdd(bazonId, _ => (new SemaphoreSlim(1), null));

            if (fileMetadata == null)
            {
                using var _ = await semaphore.LockAsync(cancellationToken).ConfigureAwait(false);

                if (cache.TryGetValue(bazonId, out var tuple)
                    && tuple.fileMetadata != null)
                {
                    fileMetadata = tuple.fileMetadata;
                }
                else
                {
                    fileMetadata = await GetFileInfoAsync(file.FrameLink!, cancellationToken).ConfigureAwait(false);
                    if (fileMetadata == null)
                    {
                        return Enumerable.Empty<Video>();
                    }
                    cache.AddOrUpdate(bazonId, (semaphore, fileMetadata), (_, t) => (t.semaphore, fileMetadata));
                }
            }

            var serialArray = JsonHelper.ParseOrNull<JArray>(fileMetadata);
            var videosStr = fileMetadata;
            if (serialArray != null)
            {
                var sn = file.Season;
                if (!sn.HasValue || sn < 0)
                {
                    sn = 0;
                }
                else
                {
                    sn--;
                }
                var ep = file.Episode?.Start.Value;
                if (!ep.HasValue || ep <= 0)
                {
                    ep = 0;
                }
                else
                {
                    ep--;
                }
                var videosStrOrNull = (((serialArray[sn] as JObject)?
                    ["folder"] as JArray)?
                    [ep] as JObject)?
                    ["file"]?
                    .ToString();
                if (videosStrOrNull == null)
                {
                    return Array.Empty<Video>();
                }
                videosStr = videosStrOrNull;
            }

            return ProviderHelper.ParseVideosFromPlayerJsString(videosStr, file.FrameLink!)
                .Select(t => t.video)
                .Select(v =>
                {
                    v.CustomHeaders.Add("Referer", file.FrameLink!.ToString());
                    return v;
                });
        }

        public Task<Folder?> GetTorrentsRootAsync(ItemInfo itemInfo, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<Folder?> GetTrailersRootAsync(ItemInfo itemInfo, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        private async Task<string?> GetFileInfoAsync(Uri frameLink, CancellationToken cancellationToken)
        {
            var decodeKey = siteProvider.Properties[BazonSiteProvider.BazonDecodeKeyKey];
            var pathKey = siteProvider.Properties[BazonSiteProvider.BazonPathKeyKey];

            var mitmScript = await GetMitmScriptCodeAsync();

            var domain = await siteProvider.GetMirrorAsync(cancellationToken).ConfigureAwait(false);
            var referer = siteProvider.Properties[BazonSiteProvider.BazonRefererKey] ?? domain.ToString();
            if (referer == "<generated>")
            {
                referer = $"https://vid{DateTime.Now.Millisecond}.co/film/";
            }

            var iframeHtml = await siteProvider.HttpClient
                .GetBuilder(frameLink)
                .WithHeader("Referer", referer)
                .SendAsync(cancellationToken)
                .AsHtml(cancellationToken)
                .ConfigureAwait(false);
            if (iframeHtml == null
                || iframeHtml.Scripts.LastOrDefault(s => s.Source == null)?.Text is not string script)
            {
                return null;
            }

            var loaderText = await GetLoaderJsCodeAsync(frameLink, frameLink).ConfigureAwait(false);
            if (loaderText == null)
            {
                return null;
            }

            string? fileMetadata = null;
            try
            {
                var engine = new Jint.Engine()
                    .SetupBrowserFunctions()
                    .SetValue("decodeKey", decodeKey)
                    .SetValue("pathKey", pathKey)
                    .Execute(mitmScript)
                    .Execute(loaderText)
                    .Execute(script);

                try
                {
                    engine = engine.Execute("process()");
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex);
                }

                var optionsFile = engine.GetValue("options").Get("file");
                var decodeKesy = engine.GetValue("options").Get("decodeKey");
                var evalInput = engine.GetValue("evalInputs").Get(0);

                fileMetadata = engine
                    .Execute("getFile()")
                    .GetCompletionValue()
                    .ToString();
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex);
                if (fileMetadata == null)
                {
                    return null;
                }
            }

            if (fileMetadata == null)
            {
                return null;
            }

            fileMetadata = await playerJsParserService
                .DecodeAsync(
                    fileMetadata,
                    new PlayerJsConfig(
                        playerJsFileLink: siteProvider.PlayerJsConfig.PlayerJsFileLink ?? new Uri(frameLink, "/js/playerjs.js"),
                        keys: siteProvider.PlayerJsConfig.Keys,
                        trash: siteProvider.PlayerJsConfig.Trash,
                        separator: siteProvider.PlayerJsConfig.Separator,
                        oyKey: siteProvider.PlayerJsConfig.OyKey),
                    cancellationToken)
                .ConfigureAwait(false);

            return fileMetadata;
        }

        private async Task<string?> GetLoaderJsCodeAsync(Uri baseDomain, Uri referer)
        {
            if (loaderJsCodeCache != null)
            {
                return loaderJsCodeCache;
            }

            using var _ = await loaderSemaphore.LockAsync().ConfigureAwait(false);

            if (loaderJsCodeCache != null)
            {
                return loaderJsCodeCache;
            }

            var loaderText = await siteProvider.HttpClient
                .GetBuilder(new Uri(baseDomain, "/js/loader.js"))
                .WithHeader("Referer", referer.ToString())
                .SendAsync(default)
                .AsText()
                .ConfigureAwait(false);
            var lastLine = loaderText?.Split(Environment.NewLine.ToCharArray()).LastOrDefault();

            return (loaderJsCodeCache = lastLine?
                // See https://github.com/sebastienros/jint/issues/710
                .Replace("[0x0]", "[0]"));
        }

        private async Task<string> GetMitmScriptCodeAsync()
        {
            var fallback = GetType().ReadResourceFromTypeAssembly("BazonMitmScript.inline.js");

            if (!siteProvider.Properties.TryGetValue(BazonSiteProvider.BazonMitmScriptLinkKey, out var mitmScriptLink))
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

        public void Dispose()
        {
            loaderSemaphore.Dispose();

            foreach (var (semaphore, _) in cache.Values)
            {
                semaphore.Dispose();
            }
        }
    }
}
