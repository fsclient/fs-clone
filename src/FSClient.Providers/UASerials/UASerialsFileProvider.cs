namespace FSClient.Providers
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    using FSClient.Shared.Helpers;
    using FSClient.Shared.Helpers.Web;
    using FSClient.Shared.Models;
    using FSClient.Shared.Providers;
    using FSClient.Shared.Services;

    using Jint;

    using Microsoft.Extensions.Logging;

    using Newtonsoft.Json.Linq;

    using Nito.AsyncEx;

    public class UASerialsFileProvider : IFileProvider
    {
        private AsyncLazy<JArray?>? currentJsonData;
        private UASerialsItemInfo? currentItem;
        private readonly AsyncLazy<string> cryptoJsSource;
        private readonly UASerialsSiteProvider siteProvider;
        private readonly TortugaFileProvider tortugaFileProvider;
        private readonly ILogger logger;

        public UASerialsFileProvider(
            UASerialsSiteProvider siteProvider,
            TortugaFileProvider tortugaFileProvider,
            ILogger logger)
        {
            this.siteProvider = siteProvider;
            this.tortugaFileProvider = tortugaFileProvider;
            this.logger = logger;

            cryptoJsSource = new AsyncLazy<string>(async () =>
            {
                var cryptoJsLink = new Uri("https://cdnjs.cloudflare.com/ajax/libs/crypto-js/3.1.9-1/crypto-js.min.js");
                return await siteProvider.HttpClient.GetBuilder(cryptoJsLink).SendAsync(default).AsText().ConfigureAwait(false) ?? string.Empty;
            });
        }

        public Site Site => siteProvider.Site;

        public bool ProvideOnline => true;

        public bool ProvideTorrent => false;

        public bool ProvideTrailers => false;

        public ProviderRequirements ReadRequirements => ProviderRequirements.None;

        public void InitForItems(IEnumerable<ItemInfo> items)
        {
            currentItem = items
                .OfType<UASerialsItemInfo>()
                .FirstOrDefault();

            currentJsonData = currentItem == null ? null : new AsyncLazy<JArray?>(() => GetJsonData(currentItem));
        }

        public async Task<IEnumerable<ITreeNode>> GetFolderChildrenAsync(Folder folder, CancellationToken cancellationToken)
        {
            if (folder == null)
            {
                throw new ArgumentNullException(nameof(folder));
            }

            if (folder.Count == 0)
            {
                var rootItems = await GetRootNodesAsync(cancellationToken).ConfigureAwait(false);
                return rootItems;
            }

            return Enumerable.Empty<ITreeNode>();
        }

        public Task<Folder?> GetTorrentsRootAsync(ItemInfo itemInfo, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<Folder?> GetTrailersRootAsync(ItemInfo itemInfo, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        private async Task<IEnumerable<ITreeNode>> GetRootNodesAsync(CancellationToken cancellationToken)
        {
            if (currentJsonData == null
                || currentItem is not UASerialsItemInfo item)
            {
                return Enumerable.Empty<File>();
            }

            var token = await currentJsonData.Task.WaitAsync(cancellationToken);
            var tortugaSeasons = token?.OfType<JObject>().FirstOrDefault(n => n["tabName"]?.ToString() != "Трейлер")?["seasons"] as JArray ?? new JArray();

            return tortugaSeasons
                .SelectMany((seasonJson, index) =>
                {
                    var seasonNumber = seasonJson["title"]?.ToString()?.SplitLazy(2, StringSplitOptions.RemoveEmptyEntries, ' ').FirstOrDefault().ToIntOrNull()
                        ?? (index + 1);
                    return (seasonJson["episodes"] as JArray ?? new JArray())
                        .SelectMany((episodeJson, index) =>
                        {
                            var episodeNumber = episodeJson["title"]?.ToString()?.SplitLazy(2, StringSplitOptions.RemoveEmptyEntries, ' ').FirstOrDefault().ToIntOrNull()
                                ?? (index + 1);

                            return (episodeJson["sounds"] as JArray ?? new JArray())
                                .Select(soundJson =>
                                {
                                    var title = soundJson["title"]?.ToString() ?? item.Translation;
                                    var url = soundJson["url"]?.ToUriOrNull();
                                    return (title, url);
                                })
                                .Where(tuple => tuple.url != null)
                                .Select(tuple => (
                                    translation: tuple.title,
                                    tuple.url,
                                    episodeNumber,
                                    seasonNumber
                                ));
                        });
                })
                .GroupBy(tuple => tuple.seasonNumber)
                .Select(group =>
                {
                    var seasonFolder = new Folder(Site, $"uas{item.SiteId}_{group.Key}", FolderType.Season, PositionBehavior.Max)
                    {
                        Title = "Сезон " + group.Key
                    };

                    var translates = group
                        .GroupBy(tuple => tuple.translation)
                        .Select(group =>
                        {
                            var translateFolder = new Folder(Site, $"{seasonFolder.Id}_{group.Key?.GetDeterministicHashCode()}", FolderType.Translate, PositionBehavior.Average)
                            {
                                Title = group.Key
                            };
                            translateFolder.AddRange(group.Select(tuple =>
                            {
                                var file = new File(Site, $"{translateFolder.Id}_{tuple.episodeNumber}")
                                {
                                    ItemTitle = item.Title,
                                    FrameLink = tuple.url,
                                    Episode = tuple.episodeNumber.ToRange(),
                                    Season = tuple.seasonNumber
                                };
                                file.SetVideosFactory((f, ct) => GetVideosAsync(f, item.Link!, ct));
                                return file;
                            }));
                            return translateFolder;
                        })
                        .ToList();

                    if (translates.Count == 1)
                    {
                        seasonFolder.AddRange(translates[0].ItemsSource);
                    }
                    else
                    {
                        seasonFolder.AddRange(translates);
                    }
                    return seasonFolder;
                });
        }

        private async Task<IEnumerable<Video>> GetVideosAsync(File file, Uri referer, CancellationToken cancellationToken)
        {
            var files = await tortugaFileProvider.GetVideosFromTortugaAsync(file.FrameLink!, referer, null, null, null, cancellationToken).ConfigureAwait(false);
            var tortugaFile = files.OfType<File>().FirstOrDefault();
            if (tortugaFile == null)
            {
                return Enumerable.Empty<Video>();
            }

            await tortugaFile.PreloadAsync(cancellationToken).ConfigureAwait(false);
            return tortugaFile.Videos;
        }

        private async Task<JArray?> GetJsonData(UASerialsItemInfo itemInfo)
        {
            var cryptoJs = await cryptoJsSource;

            var uaTools = GetType().ReadResourceFromTypeAssembly("UASerialsMitmScript.inline.js");

            if (!siteProvider.Properties.TryGetValue(UASerialsSiteProvider.UASerialsPassphraseKey, out var passphrase))
            {
                passphrase = string.Empty;
            }

            try
            {
                return await Task.Run(() => JsonHelper.ParseOrNull<JArray>(new Engine()
                    .Execute(cryptoJs)
                    .Execute(uaTools)
                    .SetValue("dataTag", itemInfo.DataTag)
                    .Execute($@"CryptoJSAesDecrypt('{passphrase}', dataTag).replace(/\\/g, '')")
                    .GetCompletionValue()
                    .AsString()))
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex);
                return null;
            }
        }
    }
}
