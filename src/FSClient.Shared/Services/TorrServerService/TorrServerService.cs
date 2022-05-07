namespace FSClient.Shared.Services
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net.Http;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Threading.Tasks;

    using FSClient.Shared.Helpers;
    using FSClient.Shared.Helpers.Web;
    using FSClient.Shared.Models;
    using FSClient.Shared.Repositories;

    using Microsoft.Extensions.Logging;

    using File = FSClient.Shared.Models.File;
    using Settings = FSClient.Shared.Settings;

    // TorrServer library doesn't work anymore and was removed.
    // Should be replaced by our own library or simple http requests.

    /// <inheridoc />
    public sealed class TorrServerService : ITorrServerService, IDisposable
    {
        private static readonly string[] subtitleFormats =
        {
            ".srt", ".vtt", ".ass", ".ssa"
        };

        private static readonly Regex episodeRegex = new Regex(
            @"(?:(?:s(?<sn>\d{1,4}))?\s*ep?(?<ep>\d{1,4}))"
            + @"|(?:(?<sn>\d{1,4})s\s*(?<ep>\d{1,4})ep?)"
            + @"|(?:(?<ep>\d{1,4})\s*ser[a-z]*)"
            + @"|(?:ser[a-z]*\s*(?<ep>\d{1,4}))"
            + @"|(?:Серия\s*(?<ep>\d{1,4}).*?Сезон\s*(?<sn>\d{1,4}))"
            + @"|(?:Сезон\s*(?<sn>\d{1,4}).*?Серия\s*(?<ep>\d{1,4}))"
            + @"|(?:(?<sn>\d{1,4})\s*Сезон.*?(?<ep>\d{1,4})\s*Серия)"
            + @"|(?:(?<ep>\d{1,4})\s*Серия.*?(?<sn>\d{1,4})\s*Сезон)"
            + @"|(?:(?<ep>\d{1,4})\s*Серия)"
            + @"|(?:Серия\s*(?<ep>\d{1,4}))"
            + @"|(?:^(?<ep>\d{1,4}))",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private readonly ITorrServerRepository torrServerRepository;
        private readonly ILogger logger;
        private readonly HttpClient httpClient;
        private readonly Settings settings;

        public TorrServerService(
            ITorrServerRepository torrServerRepository,
            ILogger logger,
            Settings settings)
        {
            this.torrServerRepository = torrServerRepository;
            this.logger = logger;
            this.settings = settings;

            httpClient = new HttpClient(new HttpClientHandler
            {
                AllowAutoRedirect = false
            });
            httpClient.Timeout = TimeSpan.FromMinutes(5);
        }

        /// <inheridoc />
        public async Task<bool> IsTorrServerAvailableAsync(CancellationToken cancellationToken)
        {
            return false;
            //if (string.IsNullOrWhiteSpace(settings.TorrServerAddress)
            //    || !Uri.TryCreate(settings.TorrServerAddress, UriKind.Absolute, out var torrServerLink)
            //    || !Uri.TryCreate(torrServerLink, "/btstat", out var btstatLink))
            //{
            //    return false;
            //}

            //return await btstatLink.IsAvailableAsync(cancellationToken).ConfigureAwait(false);
        }

        // TorrServer will return old hash id, if same link already exists
        /// <inheridoc />
        public async Task<string> AddOrUpdateTorrentAsync(TorrentFolder torrentFile, CancellationToken cancellationToken)
        {
            return string.Empty;
            //if (torrentFile == null)
            //{
            //    throw new ArgumentNullException(nameof(torrentFile));
            //}

            //if (torrentFile.Link == null)
            //{
            //    throw new ArgumentException("Torrent node Link must not be null", nameof(torrentFile));
            //}

            //EnsureTorrServerAddress();

            //torrentFile.TorrentHash = await torrentClient.AddTorrentAsync(torrentFile.Link, cancellationToken).ConfigureAwait(false);

            //if (Settings.Instance.InternalDatabaseForTorrServer)
            //{
            //    try
            //    {
            //        var entity = new TorrServerEntity
            //        {
            //            TorrentId = torrentFile.Id,
            //            TorrServerHash = torrentFile.TorrentHash
            //        };
            //        await torrServerRepository.UpsertManyAsync(new[] { entity }).ConfigureAwait(false);
            //    }
            //    catch (Exception ex)
            //    {
            //        logger.LogError(ex);
            //    }
            //}

            //return torrentFile.TorrentHash;
        }

        /// <inheridoc />
        public async Task StopActiveTorrentsAsync(CancellationToken cancellationToken)
        {
            return;
            //if (!Settings.Instance.InternalDatabaseForTorrServer)
            //{
            //    return;
            //}
            //EnsureTorrServerAddress(false);

            //var items = await torrServerRepository
            //    .GetAll()
            //    .ToListAsync(cancellationToken)
            //    .ConfigureAwait(false);
            //await items
            //    .ToAsyncEnumerable()
            //    .WhenAllAsync(async (torrent, ct) =>
            //    {
            //        try
            //        {
            //            await torrentClient.DropTorrentAsync(torrent.TorrServerHash!, ct).ConfigureAwait(false);
            //        }
            //        catch (UnauthorizedAccessException ex)
            //        {
            //            logger.LogWarning(ex);
            //        }
            //        catch (HttpRequestException ex)
            //        {
            //            logger.LogWarning(ex);
            //        }
            //    }, cancellationToken)
            //    .ConfigureAwait(false);
        }

        /// <inheridoc />
        public async Task StopAndRemoveActiveTorrentsAsync(CancellationToken cancellationToken)
        {
            return;
            //if (!Settings.Instance.InternalDatabaseForTorrServer)
            //{
            //    return;
            //}
            //EnsureTorrServerAddress(false);

            //var items = await torrServerRepository
            //    .GetAll()
            //    .ToListAsync(cancellationToken)
            //    .ConfigureAwait(false);
            //if (items.Count == 0)
            //{
            //    return;
            //}

            //await items
            //    .ToAsyncEnumerable()
            //    .WhenAllAsync(async (torrent, ct) =>
            //    {
            //        try
            //        {
            //            await torrentClient.DropTorrentAsync(torrent.TorrServerHash!, ct).ConfigureAwait(false);
            //            await torrentClient.RemoveTorrentAsync(torrent.TorrServerHash!, ct).ConfigureAwait(false);
            //        }
            //        catch (UnauthorizedAccessException ex)
            //        {
            //            logger.LogWarning(ex);
            //        }
            //        catch (HttpRequestException ex)
            //        {
            //            logger.LogWarning(ex);
            //        }
            //    }, cancellationToken)
            //    .ConfigureAwait(false);

            //await torrServerRepository.DeleteManyAsync(items).ConfigureAwait(false);
        }

        /// <inheridoc />
        public async Task StopTorrentAsync(string hashId, CancellationToken cancellationToken)
        {
            return;
            //if (hashId == null)
            //{
            //    throw new ArgumentNullException(nameof(hashId));
            //}

            //EnsureTorrServerAddress();

            //try
            //{
            //    await torrentClient.DropTorrentAsync(hashId, cancellationToken).ConfigureAwait(false);
            //}
            //catch (OperationCanceledException) { }
            //catch (UnauthorizedAccessException ex)
            //{
            //    logger.LogWarning(ex);
            //}
            //catch (HttpRequestException ex)
            //{
            //    logger.LogWarning(ex);
            //}
        }

        /// <inheridoc />
        public async Task<IReadOnlyCollection<ITreeNode>> GetTorrentNodesAsync(TorrentFolder torrentFile, string hashId, CancellationToken cancellationToken)
        {
            return Array.Empty<ITreeNode>();
            //if (torrentFile == null)
            //{
            //    throw new ArgumentNullException(nameof(torrentFile));
            //}

            //EnsureTorrServerAddress();

            //// loop for 10 retries
            //for (var i = 0; i < 10; i++)
            //{
            //    var torrent = await torrentClient.GetTorrentAsync(hashId, cancellationToken).ConfigureAwait(false);

            //    if (torrent.Files == null
            //        || torrent.Files.Count == 0)
            //    {
            //        if (torrent.Status == TorrentStatus.TorrentGettingInfo
            //            || torrent.Files == null)
            //        {
            //            await Task.Delay(1000, cancellationToken).ConfigureAwait(false);
            //        }
            //        else
            //        {
            //            return Array.Empty<ITreeNode>();
            //        }
            //    }
            //    else
            //    {
            //        return ParseJsonFiles(torrent.Files, torrentClient.TorrServerAddress, torrentFile.Id, torrentFile.Site)
            //            .ToList();
            //    }
            //}

            //return Array.Empty<ITreeNode>();

            //IEnumerable<ITreeNode> ParseJsonFiles(IEnumerable<TorrentFile> torrentFiles, Uri domain, string parentId, Site site)
            //{
            //    return torrentFiles
            //        .Select(torrentFile =>
            //        {
            //            var title = torrentFile.Name.Split('/').Last();
            //            var link = new Uri(domain, torrentFile.Link);
            //            var preload = new Uri(domain, torrentFile.Preload);
            //            var id = $"{parentId}_{title?.GetHashCode()}";

            //            var (season, episode) = ParseEpisodeAndSeason(title ?? string.Empty);

            //            if (title == null)
            //            {
            //                return default;
            //            }

            //            if (torrentFile.Files?.Count > 0)
            //            {
            //                var folder = new Folder(site, id, FolderType.Unknown, PositionBehavior.Average)
            //                {
            //                    Title = title,
            //                    Season = season,
            //                    IsTorrent = true
            //                };
            //                folder.AddRange(ParseJsonFiles(torrentFile.Files, domain, id, site));
            //                return (node: (ITreeNode?)folder, sub: (SubtitleTrack?)null);
            //            }
            //            else if (subtitleFormats.Any(f => link.AbsolutePath.Contains(f)))
            //            {
            //                var lang = LocalizationHelper.DetectLanguageNames(title).FirstOrDefault();
            //                return (node: (ITreeNode?)null, sub: new TorrServerSubtitleTrack(lang, link, preload)
            //                {
            //                    Title = title
            //                });
            //            }
            //            else
            //            {
            //                var file = new File(site, id)
            //                {
            //                    Season = season,
            //                    Episode = episode.ToRange(),
            //                    Title = title,
            //                    IsTorrent = true
            //                };

            //                file.SetVideosFactory(async (file, token) =>
            //                {
            //                    var tasks = file.SubtitleTracks
            //                        .OfType<TorrServerSubtitleTrack>()
            //                        .Where(s => s.PreloadLink != null)
            //                        .Select(s => httpClient
            //                            .GetBuilder(s.PreloadLink!)
            //                            .SendAsync(token))
            //                        .ToList();
            //                    if (preload != null)
            //                    {
            //                        tasks.Add(httpClient
            //                            .GetBuilder(preload)
            //                            .SendAsync(token));
            //                    }

            //                    await Task.WhenAll(tasks).ConfigureAwait(false);

            //                    return new[]
            //                    {
            //                        new Video(link)
            //                        {
            //                            Size = torrentFile.Size,
            //                            FileName = title!
            //                        }
            //                    };
            //                });

            //                return (node: file, sub: (SubtitleTrack?)null);
            //            }
            //        })
            //        .Where(tuple => tuple.node != null || tuple.sub != null)
            //        .GroupBy(_ => true)
            //        .SelectMany(all =>
            //        {
            //            var allList = all.ToList();
            //            var nodes = allList.Where(t => t.node != null).Select(t => t.node!).ToList();
            //            var subs = allList.Where(t => t.sub != null).Select(t => t.sub!).ToList();

            //            return nodes
            //                .Select(node =>
            //                {
            //                    if (node is File file)
            //                    {
            //                        if (nodes.Count == 1)
            //                        {
            //                            file.SubtitleTracks.AddRange(all
            //                                .Where(t => t.sub != null)
            //                                .Select(t => t.sub!)
            //                                .ToArray());
            //                        }
            //                        else if (file.Title is string fileTitle)
            //                        {
            //                            var fileName = Path.GetFileNameWithoutExtension(fileTitle);
            //                            file.SubtitleTracks.AddRange(all
            //                                .Where(t => t.sub?.Title != null && t.sub.Title.StartsWith(fileName, StringComparison.OrdinalIgnoreCase))
            //                                .Select(t => t.sub!)
            //                                .Concat(subs
            //                                    .Where(sub => sub.Title != null)
            //                                    .OrderByDescending(sub => Path.GetFileNameWithoutExtension(sub.Title).Proximity(fileTitle))
            //                                    .Take(1))
            //                                .Distinct()
            //                                .ToArray());
            //                        }
            //                    }
            //                    return node;
            //                });
            //        });
            //}
        }

        public void Dispose()
        {
            httpClient.Dispose();
        }

        //private void EnsureTorrServerAddress(bool throwOnError = true)
        //{
        //    if (string.IsNullOrWhiteSpace(settings.TorrServerAddress)
        //        || !Uri.TryCreate(settings.TorrServerAddress, UriKind.Absolute, out var torrServerAddress))
        //    {
        //        if (throwOnError)
        //        {
        //            throw new UriFormatException("TorrServer address not setted or is invalid");
        //        }
        //        return;
        //    }
        //    torrentClient.TorrServerAddress = torrServerAddress;
        //}

        //private (int? season, int? episode) ParseEpisodeAndSeason(string input)
        //{
        //    var match = episodeRegex.Match(input.Replace('.', ' '));
        //    return (
        //        match.Groups["sn"].Value?.ToIntOrNull(),
        //        match.Groups["ep"].Value?.ToIntOrNull()
        //    );
        //}

        private class TorrServerSubtitleTrack : SubtitleTrack
        {
            public TorrServerSubtitleTrack(string lang, Uri link, Uri? preloadLink) : base(lang, link)
            {
                PreloadLink = preloadLink;
            }

            public Uri? PreloadLink { get; }
        }
    }
}
