namespace FSClient.UWP.Shared.Views.Controls
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    using Windows.Foundation.Metadata;
    using Windows.Media;
    using Windows.Media.Core;
    using Windows.Media.Playback;
    using Windows.Media.Streaming.Adaptive;
    using Windows.Storage;
    using Windows.Storage.Streams;
    using Windows.Web.Http;

    using FSClient.Localization.Resources;
    using FSClient.Shared.Helpers;
    using FSClient.Shared.Helpers.Web;
    using FSClient.Shared.Managers;
    using FSClient.Shared.Models;
    using FSClient.Shared.Services;
    using FSClient.UWP.Shared.Services;

    using Humanizer;

    using Microsoft.Extensions.Logging;

    using Nito.AsyncEx;

    public sealed class MediaSourceState : IDisposable
    {
        private readonly ConcurrentDictionary<SubtitleTrack, TimedMetadataTrack[]> tracksPerSource
            = new ConcurrentDictionary<SubtitleTrack, TimedMetadataTrack[]>();

        // TODO Temp: refactor it
        private readonly AsyncManualResetEvent manualResetEvent = new AsyncManualResetEvent(false);

        private readonly HttpClient defaultHttpClient;
        private readonly IDownloadManager downloadManager;
        private readonly ILogger logger;

        public MediaSourceState(
            IDownloadManager downloadManager,
            ILogger logger)
        {
            defaultHttpClient = new HttpClient();

            this.downloadManager = downloadManager;
            this.logger = logger;
        }

        public void ClearSubsState()
        {
            manualResetEvent.Reset();
            tracksPerSource.Clear();
        }

        public async Task ApplySubtitlesAsync(IMediaPlaybackSource mediaPlaybackSource, SubtitleTrack? subs)
        {
            try
            {
                await manualResetEvent.WaitAsync().ConfigureAwait(true);

                if (tracksPerSource.IsEmpty)
                {
                    return;
                }

                if (subs != null && tracksPerSource.TryGetValue(subs, out var tracks))
                {
                    mediaPlaybackSource?.ApplySubtitles(tracks);
                }
                else
                {
                    mediaPlaybackSource?.ApplySubtitles(new List<TimedMetadataTrack>());
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex);
            }
        }

        public async Task<(IMediaPlaybackSource source, StorageFile? file)?> CreateFromVariantAsync(
            VideoVariant currentVariant, Video video, bool ignoreSubs = false)
        {
            var fromDownloadedSource = await TryCreateFromDownloadedAsync(video, ignoreSubs);
            if (fromDownloadedSource != null)
            {
                return fromDownloadedSource;
            }

            if (currentVariant.Parts.Count > 1)
            {
                var listSource = await TryCreateMediaPlaybackListSource(currentVariant, video, ignoreSubs)
                    .ConfigureAwait(true);
                if (listSource != null)
                {
                    return (listSource, null);
                }
            }

            // UWP Media Player doesn't allow to override UserAget, so we need to handle HTTP requests with custom stream
            if (video.CustomHeaders.ContainsKey("User-Agent"))
            {
                var customStreamSource = await TryCreateCustomStreamSource(currentVariant, video).ConfigureAwait(true);
                if (customStreamSource != null)
                {
                    return (customStreamSource, null);
                }
            }

            var source = await TryCreateSimpleMediaSource(currentVariant, video, ignoreSubs);
            if (source != null)
            {
                return (source, null);
            }

            try
            {
                var playbackItem =
                    new MediaPlaybackItem(MediaSource.CreateFromUri(currentVariant.Parts.FirstOrDefault()));
                ApplyDisplayProperties(playbackItem, video);
                return (playbackItem, null);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex);
                return null;
            }
        }

        public ValueTask<VideoVariant> GetAvailableVideoVariantAsync(Video video, CancellationToken cancellationToken)
        {
            if (video.Variants.Count == 0)
            {
                throw new InvalidOperationException("Can't get video variant from empty array");
            }

            if (video.Variants.Count == 1)
            {
                return new ValueTask<VideoVariant>(video.Variants[0]);
            }

            return new ValueTask<VideoVariant>(GetAvailableVideoVariantInternal(video, cancellationToken));

            async Task<VideoVariant> GetAvailableVideoVariantInternal(Video inVideo,
                CancellationToken inCancellationToken)
            {
                return (await inVideo.Variants
                            .Where(v => v.Parts.Count > 0)
                            .Select(v => new Func<CancellationToken, Task<(VideoVariant variant, bool success)>>(
                                async ct =>
                                {
                                    var dict = inVideo.CustomHeaders as IReadOnlyDictionary<string, string>
                                        ?? inVideo.CustomHeaders.ToDictionary(p => p.Key, p => p.Value);
                                    var success = await v.Parts[0].IsAvailableAsync(dict, ct)
                                        .ConfigureAwait(false);
                                    return (v, success);
                                }))!
                        .WhenAny(res => res.success, (null, false), inCancellationToken)
                        .ConfigureAwait(false))
                    .variant ?? inVideo.Variants[0];
            }
        }

        private async Task<MediaPlaybackList?> TryCreateMediaPlaybackListSource(VideoVariant variant, Video video,
            bool ignoreSubs)
        {
            try
            {
                if (variant.Parts.Count < 1)
                {
                    return null;
                }

                var list = new MediaPlaybackList();
                var subs = video.ParentFile?.SubtitleTracks ?? TrackCollection.Empty<SubtitleTrack>();

                var itemsWithSubs = await Task
                    .WhenAll(variant.Parts
                        .Select(async (link, index) =>
                        {
                            MediaSource? source = null;
                            if (video.CustomHeaders.Count > 0)
                            {
                                source = await TryCreateSourceWithCustomHeadersAsync(link, video.CustomHeaders)
                                    .ConfigureAwait(true);
                            }

                            if (source == null)
                            {
                                source = MediaSource.CreateFromUri(link);
                            }

                            var item = new MediaPlaybackItem(source);
                            ApplyDisplayProperties(item, video);
                            if (!source.IsOpen)
                            {
                                await source.OpenAsync();
                            }

                            if (index == 0 && !ignoreSubs)
                            {
                                var tracksPerSub = await Task.WhenAll(subs
                                        .Select(async s => (
                                            sub: s,
                                            tracks: await ResolveAndRemoveTracksAsync(source, s)
                                                .ConfigureAwait(false))))
                                    .ConfigureAwait(false);

                                return (item, tracksPerSub);
                            }

                            return (item,
                                tracksPerSub: Array.Empty<(SubtitleTrack, IEnumerable<TimedMetadataTrack>)>());
                        }))
                    .ConfigureAwait(true);

                var firstTracksPerSub = itemsWithSubs[0].tracksPerSub;

                var negativeOffset = TimeSpan.Zero;
                foreach (var (item, _) in itemsWithSubs)
                {
                    if (item.Source == null)
                    {
                        continue;
                    }

                    var currentItemDuration = item.Source.Duration ?? TimeSpan.Zero;
                    foreach (var (sub, tracks) in firstTracksPerSub)
                    {
                        var clonedSub = (SubtitleTrack)sub.Clone();
                        clonedSub.Offset -= negativeOffset;
                        clonedSub.EndTime = currentItemDuration;

                        var processed = tracks.Process(clonedSub).ToArray();
                        tracksPerSource.AddOrUpdate(sub,
                            processed,
                            (_, old) => old.Union(processed).ToArray());

                        foreach (var track in processed)
                        {
                            item.Source.ExternalTimedMetadataTracks.Add(track);
                        }
                    }

                    negativeOffset += currentItemDuration;
                    list.Items.Add(item);
                }

                return list;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex);
                return null;
            }
        }

        private async Task<IMediaPlaybackSource?> TryCreateSimpleMediaSource(VideoVariant variant, Video video,
            bool ignoreSubs)
        {
            try
            {
                var link = variant.Parts.FirstOrDefault();

                MediaSource? source = null;

                if (video.CustomHeaders.Count > 0)
                {
                    source = await TryCreateSourceWithCustomHeadersAsync(link, video.CustomHeaders);
                }

                if (source == null)
                {
#pragma warning disable IDE0067 // Dispose objects before losing scope
                    source = MediaSource.CreateFromUri(link);
#pragma warning restore IDE0067 // Dispose objects before losing scope
                }

                if (!ignoreSubs
                    && video.ParentFile?.SubtitleTracks is var subs
                    && subs?.Any() == true)
                {
                    _ = ConfigureSingleSourceSubsAsync(source, subs!);
                }

                var playbackItem = new MediaPlaybackItem(source);
                ApplyDisplayProperties(playbackItem, video);
                return playbackItem;
            }
            catch (Exception mediaSourceEx)
            {
                logger?.LogWarning(mediaSourceEx);
                return null;
            }
        }

        private async Task<(IMediaPlaybackSource source, StorageFile file)?> TryCreateFromDownloadedAsync(Video video,
            bool ignoreSubs)
        {
            try
            {
                var downloadFile = await downloadManager.GetDownloadFileByVideo(video).ConfigureAwait(false);
                if (downloadFile?.File is not UWPStorageFile uwpStorageFile)
                {
                    return null;
                }

                var source = MediaSource.CreateFromStorageFile(uwpStorageFile.File);

                if (!ignoreSubs
                    && video.ParentFile?.SubtitleTracks is var subs
                    && subs?.Any() == true)
                {
                    _ = ConfigureSingleSourceSubsAsync(source, subs!);
                }

                var playbackItem = new MediaPlaybackItem(source);
                ApplyDisplayProperties(playbackItem, video);
                return (playbackItem, uwpStorageFile.File);
            }
            catch (Exception mediaSourceEx)
            {
                logger?.LogWarning(mediaSourceEx);
                return null;
            }
        }

        private async Task<MediaSource?> TryCreateSourceWithCustomHeadersAsync(Uri link,
            IDictionary<string, string> headers)
        {
            try
            {
                defaultHttpClient.DefaultRequestHeaders.Clear();
                foreach (var header in headers)
                {
                    defaultHttpClient
                        .DefaultRequestHeaders
                        .TryAppendWithoutValidation(header.Key, header.Value);
                }

                var result = await AdaptiveMediaSource.CreateFromUriAsync(link, defaultHttpClient);

                if (result?.Status == AdaptiveMediaSourceCreationStatus.Success)
                {
                    return MediaSource.CreateFromAdaptiveMediaSource(result.MediaSource);
                }
            }
            catch (System.Net.Http.HttpRequestException)
            {
            }
            catch (Exception ex)
            {
                logger?.LogWarning(ex);
            }

            return null;
        }

        private async Task<IMediaPlaybackSource?> TryCreateCustomStreamSource(VideoVariant variant, Video video)
        {
            try
            {
                defaultHttpClient.DefaultRequestHeaders.Clear();
                foreach (var header in video.CustomHeaders)
                {
                    defaultHttpClient
                        .DefaultRequestHeaders
                        .TryAppendWithoutValidation(header.Key, header.Value);
                }

                var stream =
                    await HttpRandomAccessStream.CreateAsync(defaultHttpClient, variant.Parts.FirstOrDefault());
                var playbackItem = new MediaPlaybackItem(MediaSource.CreateFromStream(stream, stream.ContentType));
                ApplyDisplayProperties(playbackItem, video);
                return playbackItem;
            }
            catch (System.Net.Http.HttpRequestException)
            {
            }
            catch (Exception ex)
            {
                logger?.LogError(ex);
            }

            return null;
        }

        private async Task ConfigureSingleSourceSubsAsync(MediaSource source, IReadOnlyCollection<SubtitleTrack> subs)
        {
            IEnumerable<(SubtitleTrack sub, TimedMetadataTrack tracks)> subTracks;

            manualResetEvent.Reset();

            if (subs.Any(s => s.Offset != TimeSpan.Zero || s.SpeedModifier != 1))
            {
                if (!source.IsOpen)
                {
                    await source.OpenAsync();
                }

                subTracks = (await Task.WhenAll(subs
                            .Select(async sub =>
                            {
                                var tracks = await ResolveAndRemoveTracksAsync(source, sub).ConfigureAwait(true);
                                return (sub, tracks: tracks.Process(sub));
                            }))
                        .ConfigureAwait(false))
                    .SelectMany(t => t.tracks.Select(tr => (t.sub, tr)));

                foreach (var (_, track) in subTracks)
                {
                    try
                    {
                        source.ExternalTimedMetadataTracks.Add(track);
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(ex);
                    }
                }
            }
            else
            {
                subTracks = (await Task.WhenAll(subs
                            .Select(async sub => (
                                sub,
                                tracks: await ResolveAndNoRemoveTracksAsync(source, sub).ConfigureAwait(false)
                            )))
                        .ConfigureAwait(false))
                    .SelectMany(t => t.tracks.Select(tr => (t.sub, tr)));
            }

            foreach (var group in subTracks.GroupBy(t => t.sub))
            {
                tracksPerSource.AddOrUpdate(
                    group.Key,
                    _ => group.Select(t => t.tracks).ToArray(),
                    (_, old) => old.Union(group.Select(t => t.tracks)).ToArray());
            }

            manualResetEvent.Set();
        }

        private async Task<IEnumerable<TimedMetadataTrack>> ResolveAndNoRemoveTracksAsync(MediaSource source,
            SubtitleTrack sub)
        {
            try
            {
                var tts = await sub.GetTimedTextSourceAsync().ConfigureAwait(true);
                if (tts == null)
                {
                    return Enumerable.Empty<TimedMetadataTrack>();
                }

                var tcs = new TaskCompletionSource<TimedTextSourceResolveResultEventArgs>();
                tts.Resolved += (_, a) => tcs.TrySetResult(a);

                source.ExternalTimedTextSources.Add(tts);

                var result = await tcs.Task;

                if (result.Error?.ExtendedError is Exception resolveEx)
                {
                    logger.LogWarning(resolveEx);
                    return Enumerable.Empty<TimedMetadataTrack>();
                }

                var tracks = result.Tracks;

                foreach (var cue in result.Tracks.SelectMany(t => t.Cues).OfType<TimedTextCue>())
                {
                    cue.CueStyle.SetupAppStyle();
                }

                return tracks;
            }
            catch (Exception ex)
            {
                logger.LogError(ex);
            }

            return Enumerable.Empty<TimedMetadataTrack>();
        }

        private async Task<IEnumerable<TimedMetadataTrack>> ResolveAndRemoveTracksAsync(MediaSource source,
            SubtitleTrack sub)
        {
            try
            {
                var tts = await sub.GetTimedTextSourceAsync().ConfigureAwait(true);
                if (tts == null)
                {
                    return Enumerable.Empty<TimedMetadataTrack>();
                }

                var tcs = new TaskCompletionSource<TimedTextSourceResolveResultEventArgs>();
                tts.Resolved += (_, a) => tcs.TrySetResult(a);

                source.ExternalTimedTextSources.Add(tts);

                var result = await tcs.Task;

                if (result.Error?.ExtendedError is Exception resolveEx)
                {
                    logger.LogWarning(resolveEx);
                    return Enumerable.Empty<TimedMetadataTrack>();
                }

                if (!source.IsOpen)
                {
                    await source.OpenAsync();
                }

                if (source.ExternalTimedTextSources.Contains(tts))
                {
                    source.ExternalTimedTextSources.Remove(tts);
                }

                return result.Tracks;
            }
            catch (Exception ex)
            {
                logger.LogError(ex);
            }

            return Enumerable.Empty<TimedMetadataTrack>();
        }

        private static void ApplyDisplayProperties(MediaPlaybackItem mediaPlaybackItem, Video currentVideo)
        {
            if (ApiInformation.IsPropertyPresent(
                typeof(MediaPlaybackItem).FullName,
                nameof(MediaPlaybackItem.AutoLoadedDisplayProperties)))
            {
                mediaPlaybackItem.AutoLoadedDisplayProperties = AutoLoadedDisplayPropertyKind.None;
            }

            var props = mediaPlaybackItem.GetDisplayProperties();
            props.ClearAll();
            props.Type = MediaPlaybackType.Video;
            if (currentVideo?.ParentFile is File file)
            {
                if (file.ItemTitle != null)
                {
                    props.VideoProperties.Title = file.ItemTitle;
                }

                if (file.PlaceholderImage is Uri thumbUri
                    && thumbUri.IsAbsoluteUri)
                {
                    props.Thumbnail = RandomAccessStreamReference.CreateFromUri(thumbUri);
                }

                if (file.Episode?.ToFormattedString() is string episode)
                {
                    props.VideoProperties.Subtitle = file.Season is int season
                        ? Strings.File_SeasonAndEpisodeWithNumber.FormatWith(season, episode)
                        : Strings.File_EpisodeWithNumber.FormatWith(episode);
                }
                else if (file.Season is int season)
                {
                    props.VideoProperties.Subtitle = Strings.File_SeasonWithNumber.FormatWith(season);
                }
            }

            mediaPlaybackItem.ApplyDisplayProperties(props);
        }

        public void Dispose()
        {
            defaultHttpClient.Dispose();
        }
    }
}
