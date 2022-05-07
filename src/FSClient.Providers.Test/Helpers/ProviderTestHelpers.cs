namespace FSClient.Providers.Test.Helpers
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;

    using FSClient.Shared.Helpers;
    using FSClient.Shared.Helpers.Web;
    using FSClient.Shared.Models;
    using FSClient.Shared.Providers;

    using Microsoft.Extensions.Logging;

    using NUnit.Framework;

    [Flags]
    public enum CheckFileFlags
    {
        None = 0,
        Parallel = 1,
        AtLeastOne = Parallel << 1,
        UniqueIDs = AtLeastOne << 1,
        Default = UniqueIDs | AtLeastOne,
        WithSubs = UniqueIDs << 1,
        Trailers = WithSubs << 1,
        Playlist = Trailers << 1,
        Episodes = Playlist << 1,
        Serial = Episodes | Playlist,
        IgnoreVideos = Episodes << 1,
        RequireHD = IgnoreVideos << 1
    }

    [Flags]
    public enum CheckTorrentFlags
    {
        None = 0,
        UniqueLinks = 1,
        CheckSeeds = 2,
        CheckPeers = 4,
        CheckLeaches = 8
    }

    public static class ProviderTestHelpers
    {
        private static readonly HttpClient testHttpClient = new HttpClient();

        public static async Task CheckFilesAsync(
            IEnumerable<File> fileEnumerable, CheckFileFlags behaviour = CheckFileFlags.Default, int limit = 5)
        {
            if (limit > -1)
            {
                fileEnumerable = fileEnumerable.Take(limit);
            }
            var files = fileEnumerable.ToArray();

            Assert.That(files.Length > 0, Is.True, "Zero files count");

            var parallel = behaviour.HasFlag(CheckFileFlags.Parallel);
            var checkSubs = behaviour.HasFlag(CheckFileFlags.WithSubs);
            var atLeastOne = behaviour.HasFlag(CheckFileFlags.AtLeastOne);
            var uniqueIds = behaviour.HasFlag(CheckFileFlags.UniqueIDs);
            var mustBeTrailer = behaviour.HasFlag(CheckFileFlags.Trailers);
            var playlist = behaviour.HasFlag(CheckFileFlags.Playlist);
            var episodes = behaviour.HasFlag(CheckFileFlags.Episodes);
            var ignoreVideos = behaviour.HasFlag(CheckFileFlags.IgnoreVideos);
            var requireHD = behaviour.HasFlag(CheckFileFlags.RequireHD) && !ignoreVideos;

            CollectionAssert.DoesNotContain(
                files.Select(f => string.IsNullOrWhiteSpace(f?.Id)).ToArray(),
                true,
                "Items ids is undefined");

            if (uniqueIds)
            {
                CollectionAssert.AllItemsAreUnique(
                    files.Select(f => f.Id).ToArray(),
                    "Items ids isn't unique");
            }

            if (playlist)
            {
                foreach (var file in files)
                {
                    Assert.That(file.Playlist, Is.Not.Null, $"Playlist is null for file={file}");
                    Assert.That(file.Playlist.Count, Is.Not.EqualTo(0), $"Playlist is empty file={file}");
                    CollectionAssert.AllItemsAreUnique(
                        file.Playlist.Select(p => p.Id).ToArray(),
                        $"Playlist items id is not unique file={file}");
                    CollectionAssert.AllItemsAreUnique(
                        file.Playlist.Select(p => p.Episode).ToArray(),
                        $"Playlist items episode is not unique file={file}");
                }
            }

            if (episodes)
            {
                foreach (var folderGroup in files.Where(f => !f.IsTrailer).GroupBy(f => f.Parent))
                {
                    CollectionAssert.AllItemsAreNotNull(
                        folderGroup.Select(f => f.Episode).ToArray(),
                        "Some items episode is null");
                    CollectionAssert.AllItemsAreUnique(
                        folderGroup.Select(f => f.Episode).ToArray(),
                        "Items episode isn't unique");
                }
            }

            CollectionAssert.DoesNotContain(
                files.Select(f => f.Episode.HasValue || !string.IsNullOrWhiteSpace(f.Title)).ToArray(),
                false,
                "Some items episode and title is undefined");

            if (parallel)
            {
                var operations = files
                    .Select(file => new Func<CancellationToken, Task<(bool success, string? info)>>(ct =>
                        CheckFileAndGetResultAsync(file, checkSubs, mustBeTrailer, ignoreVideos)))
                    .ToArray();
                if (atLeastOne)
                {
                    var (success, info) = await operations.WhenAny(r => r.success).ConfigureAwait(false);
                    if (!success)
                    {
                        Assert.Fail(info);
                    }
                    if (success && !string.IsNullOrWhiteSpace(info))
                    {
                        Assert.Inconclusive(info);
                    }
                }
                else
                {
                    var (success, info) = await operations.WhenAny(r => !r.success, (success: true, info: string.Empty)).ConfigureAwait(false);
                    if (!success)
                    {
                        Assert.Fail(info);
                    }

                    if (success && !string.IsNullOrWhiteSpace(info))
                    {
                        Assert.Inconclusive(info);
                    }
                }
            }
            else
            {
                foreach (var file in files)
                {
                    var (success, info) = await CheckFileAndGetResultAsync(file, checkSubs, mustBeTrailer, ignoreVideos).ConfigureAwait(false);
                    if (atLeastOne)
                    {
                        if (success)
                        {
                            if (!string.IsNullOrWhiteSpace(info))
                            {
                                Assert.Inconclusive(info);
                            }

                            return;
                        }

                        if (file.Equals(files.Last()))
                        {
                            Assert.Fail(info);
                        }
                    }
                    else if (!success)
                    {
                        Assert.Fail(info);
                    }
                }
            }
        }

        public static async Task CheckFileAsync(File? file, bool checkSubs = false, bool mustBeTrailer = false)
        {
            var (success, info) = await CheckFileAndGetResultAsync(file, checkSubs, mustBeTrailer).ConfigureAwait(false);
            if (!success)
            {
                Assert.Fail(info);
            }

            if (!string.IsNullOrWhiteSpace(info))
            {
                Assert.Inconclusive(info);
            }
        }

        public static async Task CheckVideosAsync(IReadOnlyList<Video> videos, File? file, HttpClient? httpClient = null)
        {
            var (success, info) = await CheckVideosAndGetResultAsync(videos, file, httpClient).ConfigureAwait(false);
            if (!success)
            {
                Assert.Fail(info);
            }

            if (!string.IsNullOrWhiteSpace(info))
            {
                Assert.Inconclusive(info);
            }
        }

        public static async Task CheckTorrentsAsync(
            ICollection<TorrentFolder> torrents, CheckTorrentFlags flags = CheckTorrentFlags.UniqueLinks, int limit = 5)
        {
            Assert.That(torrents.Count > 0, Is.True, "No torrents");

            if (limit > -1)
            {
                torrents = torrents.Take(limit).ToList();
            }

            await torrents.ToAsyncEnumerable()
                .ForEachAwaitAsync(t => t.PreloadAsync(CancellationToken.None).AsTask())
                .ConfigureAwait(false);

            CollectionAssert.AllItemsAreNotNull(
                torrents.Select(f => f.Link).ToArray(),
                "Some item is null");

            CollectionAssert.AllItemsAreNotNull(
                torrents.Select(f => f.Title.NotEmptyOrNull()).ToArray(),
                "Some item is null");

            if (flags.HasFlag(CheckTorrentFlags.UniqueLinks))
            {
                CollectionAssert.AllItemsAreUnique(
                    torrents.Select(f => f.Link).ToArray(),
                    "Items links aren't unique");
            }

            if (flags.HasFlag(CheckTorrentFlags.CheckSeeds))
            {
                Assert.That(torrents.Any(f => f.Seeds.HasValue), Is.True, "No seeds for all torrents");
            }
            if (flags.HasFlag(CheckTorrentFlags.CheckPeers))
            {
                Assert.That(torrents.Any(f => f.Peers.HasValue), Is.True, "No peers for all torrents");
            }
            if (flags.HasFlag(CheckTorrentFlags.CheckLeaches))
            {
                Assert.That(torrents.Any(f => f.Leeches.HasValue), Is.True, "No leaches for all torrents");
            }

            if (!torrents.All(t => t.IsMagnet))
            {
                var link = torrents.FirstOrDefault(v => v.Link != null && !v.IsMagnet)?.Link;
                Assert.That(link, Is.Not.Null, "Torrent link must be not null");

                using var resp = await testHttpClient.GetBuilder(link!)
                    .SendAsync(HttpCompletionOption.ResponseHeadersRead, CancellationToken.None)
                    .ConfigureAwait(false);

                Assert.That(
                    resp?.IsSuccessStatusCode ?? false, Is.True,
                    "Cannot load video file from link");
            }
        }

        public static IEnumerable<TNode> GetDeepNodes<TNode>(IEnumerable<ITreeNode> nodes)
            where TNode : ITreeNode
        {
            if (nodes == null)
            {
                throw new ArgumentNullException(nameof(nodes));
            }

#if DEBUG
            TraceNodes(nodes);
#endif
            return nodes.GetDeepNodes<TNode>();
        }

        public static async Task<List<TNode>> PreloadAndGetDeepNodes<TNode>(IFileProvider provider, Folder folder, int limit = 3)
            where TNode : ITreeNode
        {
            var deepNodes = await PreloadAndGetDeepNodesInternal(folder)
                .ConfigureAwait(false);
#if DEBUG
            TraceNodes(new[] { folder });
#endif
            return deepNodes;

            async Task<List<TNode>> PreloadAndGetDeepNodesInternal(Folder folder)
            {
                if (folder.Count == 0)
                {
                    var children = await provider.GetFolderChildrenAsync(folder, CancellationToken.None).ConfigureAwait(false);
                    folder.AddRange(children);
                }

                return await folder
                    .ItemsSource
                    .Take(limit)
                    .ToAsyncEnumerable()
                    .WhenAll((node, ct) => node switch
                    {
                        Folder f => PreloadAndGetDeepNodesInternal(f),
                        TNode t => Task.FromResult(new List<TNode> { t }),
                        _ => Task.FromResult(new List<TNode>()),
                    })
                    .SelectMany(n => n.ToAsyncEnumerable())
                    .ToListAsync(CancellationToken.None)
                    .ConfigureAwait(false);
            }
        }

        public static async Task<List<TNode>> PreloadAndGetFirstOfType<TNode>(IFileProvider provider, Folder folder)
            where TNode : ITreeNode
        {
            if (folder == null)
            {
                throw new ArgumentNullException(nameof(folder));
            }

            var children = await provider.GetFolderChildrenAsync(folder, CancellationToken.None).ConfigureAwait(false);
            folder.AddRange(children);

            var founded = folder.OfType<TNode>().ToList();
            if (founded.Count > 0)
            {
                return founded;
            }

            foreach (var subFolder in folder.OfType<Folder>())
            {
                founded = await PreloadAndGetFirstOfType<TNode>(provider, subFolder).ConfigureAwait(false);
                if (founded.Count > 0)
                {
                    return founded;
                }
            }
            return new List<TNode>();
        }

        private static void TraceNodes(IEnumerable<ITreeNode> nodes, int level = 1)
        {
            if (nodes == null)
            {
                throw new ArgumentNullException(nameof(nodes));
            }

            var rows = new List<string[]>
            {
                new [] { "Padding", "Type", "Title", "Additional" }
            };

            FillRows(nodes, level);

            Test.Logger.Log(LogLevel.Information, default, rows, null, (_, __) => string.Empty);

            void FillRows(IEnumerable<ITreeNode> nodes, int level)
            {
                foreach (var node in nodes)
                {
                    var padding = new string('-', level);
                    var title = (node as File)?.Episode is Range ep
                        ? $"Episode {ep.ToFormattedString()} {node.Title}".Trim()
                        : (node.Title?.NotEmptyOrNull() ?? " - ");
                    rows!.Add(new[] { padding, node.GetType().Name, title, node.ToString()! });

                    if (node is IFolderTreeNode folderTreeNode)
                    {
                        FillRows(folderTreeNode.ItemsSource, level + 1);
                    }
                }
            }
        }

        private static async Task<(bool success, string? info)> CheckFileAndGetResultAsync(
            File? file, bool checkSubs = false, bool mustBeTrailer = false, bool ignoreVideos = false, bool requireHD = false)
        {
            if (file == null)
            {
                return (false, "Some file is null");
            }

            if (mustBeTrailer
                && !file.IsTrailer)
            {
                return (false, "File is not a trailer " + file);
            }

            if (string.IsNullOrEmpty(file.Id))
            {
                return (false, "No ID in file " + file);
            }

            // Legacy history system is broken for that case, so we should avoid it
            if (file.Id.StartsWith(file.Site.Value, StringComparison.OrdinalIgnoreCase))
            {
                return (false, "Id is invalid for " + file);
            }

            if (!ignoreVideos)
            {
                await file.PreloadAsync(CancellationToken.None).ConfigureAwait(false);

                if (checkSubs
                    && (file.SubtitleTracks == null || file.SubtitleTracks.Count == 0))
                {
                    return (false, "No subs in file " + file);
                }

                return await CheckVideosAndGetResultAsync(file.Videos, file, requireHD: requireHD).ConfigureAwait(false);
            }
            return (true, null);
        }

        private static async Task<(bool success, string? info)> CheckVideosAndGetResultAsync(
            IReadOnlyList<Video> videos, File? file, HttpClient? httpClient = null, bool requireHD = false)
        {
            const long perMB = 1024 * 1024;

            if (videos.Count == 0)
            {
                return (false, "No videos in " + file);
            }

            if (videos.Any(c => c == null))
            {
                return (false, "Some video is null in " + file);
            }

            if (videos.Select(v => v.Links).ToArray().Any(l => l.Count == 0))
            {
                return (false, "Some video without links " + file);
            }

            if (videos.SelectMany(v => v.Variants).SelectMany(v => v.Parts).GroupBy(l => l).Any(g => g.Count() > 1))
            {
                return (false, "Some video link is duplicated in " + file);
            }

            if (requireHD && !videos.Max(v => v.Quality).IsHD)
            {
                return (false, "Max video quality is lower than HD for file " + file);
            }

            httpClient ??= testHttpClient;

            var responses = Array.Empty<(HttpResponseMessage? response, int varianIndex, CancellationTokenSource cts)>();
            try
            {
                responses = await videos
                    .Select(v => v.Variants.SelectMany((variant, varianIndex) => variant.Parts.Select(p => (part: p, varianIndex)))
                        .Select(tuple => (tuple.varianIndex, tuple.part, headers: v.CustomHeaders, cts: new CancellationTokenSource(TimeSpan.FromSeconds(5)))))
                    .SelectMany(v => v)
                    .ToAsyncEnumerable()
                    .SelectAwait(t => new ValueTask<(HttpResponseMessage? response, int varianIndex, CancellationTokenSource cts)>(httpClient
                        .GetBuilder(t.part)
                        .WithHeaders(t.headers.Concat(new Dictionary<string, string>
                        {
                            ["Range"] = "bytes=0-1"
                        }))
                        .SendAsync(HttpCompletionOption.ResponseHeadersRead, t.cts.Token)
                        .ContinueWith(task => (task.IsCompletedSuccessfully ? task.Result : null, t.varianIndex, t.cts), default, TaskContinuationOptions.None, TaskScheduler.Current)))
                    .ToArrayAsync()
                    .ConfigureAwait(false);

                if (responses.All(r => r.response?.IsSuccessStatusCode != true))
                {
                    return (false, "Cannot load video files from link in " + file);
                }

                var anyNotUniqueBySize = responses
                    .Select(r => (size: r.response?.GetContentSize(), r.varianIndex))
                    .Where(t => t.size > perMB)
                    .GroupBy(t => (t.size, t.varianIndex))
                    .Any(g => g.Count() > 1);
                if (anyNotUniqueBySize)
                {
                    return (false, "Some video contents is not unique by content size");
                }
            }
            finally
            {
                foreach (var (_, __, cts) in responses)
                {
                    if (!cts.IsCancellationRequested)
                    {
                        cts.Cancel();
                    }
                    cts.Dispose();
                }
            }

            return (true, null);
        }
    }
}
