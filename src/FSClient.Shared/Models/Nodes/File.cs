namespace FSClient.Shared.Models
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;

    using FSClient.Shared.Helpers;
    using FSClient.Shared.Helpers.Web;
    using FSClient.Shared.Services;

    using Nito.AsyncEx;

    /// <summary>
    /// Online file tree node
    /// </summary>
    public class File : TreeNodeBase, IPreloadableNode, IEquatable<File>
    {
        private static readonly Func<File, CancellationToken, Task<IEnumerable<Video>>> defaultFactory = new Func<File, CancellationToken, Task<IEnumerable<Video>>>(
            (f, _) => Task.FromResult(f.Videos.AsEnumerable()));

        private static readonly HttpClient httpClient = new HttpClient(new HttpClientHandler
        {
            AllowAutoRedirect = true,
            MaxAutomaticRedirections = 10
        });

        private readonly SemaphoreSlim videosPreloadingSemaphore = new SemaphoreSlim(1);
        private Func<File, CancellationToken, Task<IEnumerable<Video>>> videosFactory;

        public File(Site site, string id)
            : base(site, id)
        {
            videosFactory = defaultFactory;

            SubtitleTracks = TrackCollection.Empty<SubtitleTrack>();
            EmbededAudioTracks = TrackCollection.Empty<AudioTrack>();
        }

        /// <summary>
        /// Link to webplayer with file
        /// </summary>
        public Uri? FrameLink { get; set; }

        /// <summary>
        /// Is trailer file
        /// </summary>
        public bool IsTrailer { get; set; }

        /// <summary>
        /// File quality. Usually max <see cref="Videos"/> quality.
        /// </summary>
        public string? Quality
        {
            get => Get<string>();
            private set => Set(value);
        }

        /// <inheritdoc/>
        public bool IsPreloaded => Videos.Count > 0;

        /// <inheridoc/>
        public bool IsLoading
        {
            get => Get(false);
            private set => Set(value);
        }

        /// <inheridoc/>
        public bool IsFailed
        {
            get => Get(false);
            private set => Set(value);
        }

        /// <summary>
        /// File playlist
        /// </summary>
        public IReadOnlyList<File> Playlist
        {
            get => Get(() =>
            {
                var playlist = Parent?.ItemsSource?.OfType<File>().ToList().AsReadOnly();
                return playlist?.Any(f => f.Episode != null && f.Episode.Equals(Episode)) == true
                    ? playlist
                    : (IReadOnlyList<File>)new File[] { this };
            });
            set => Set(value);
        }

        /// <summary>
        /// Related item title
        /// </summary>
        public string? ItemTitle
        {
            get => Get<string?>() ?? ItemInfo?.Title;
            set => Set(value);
        }

        /// <summary>
        /// File subtitle tracks
        /// </summary>
        public TrackCollection<SubtitleTrack> SubtitleTracks { get; }

        /// <summary>
        /// File embeded audio tracks
        /// </summary>
        public TrackCollection<AudioTrack> EmbededAudioTracks { get; }

        /// <summary>
        /// File video placeholder image
        /// </summary>
        public Uri? PlaceholderImage
        {
            get => Get<Uri?>() ?? ItemInfo?.Poster[ImageSize.Original];
            set => Set(value);
        }

        /// <summary>
        /// Videos array
        /// </summary>
        public IReadOnlyList<Video> Videos
        {
            get => Get((IReadOnlyList<Video>)Array.Empty<Video>());
            private set
            {
                if (Set(value))
                {
                    foreach (var v in value)
                    {
                        v.ParentFile = this;
                    }
                }
            }
        }

        /// <summary>
        /// Update <see cref="Quality"/> field depending on videos
        /// </summary>
        public void UpdateQuality()
        {
            Quality = GetByQuality(Settings.Instance.PreferredQuality, false)?.Quality is Quality qual
                && !qual.IsUnknown
                ? qual.Title
                : null;
        }

        /// <summary>
        /// Set videos
        /// </summary>
        /// <param name="videos">Array of videos</param>
        public void SetVideos(params Video[] videos)
        {
            Array.Sort(videos, (l, r) => r.CompareTo(l));
            Videos = videos;
        }

        /// <summary>
        /// Set videos factory to lazy load videos
        /// </summary>
        /// <param name="videosTask">Videos factory</param>
        public void SetVideosFactory(Func<File, CancellationToken, Task<IEnumerable<Video>>> videosTask)
        {
            videosFactory = videosTask ?? throw new ArgumentNullException(nameof(videosTask));
        }

        /// <summary>
        /// Preload videos from lazy factory
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Is at least one video successfully preloaded</returns>
        public async ValueTask<bool> PreloadAsync(CancellationToken cancellationToken)
        {
            if (Videos.Count > 0)
            {
                return true;
            }

            if (videosFactory == null)
            {
                return false;
            }

            try
            {
                using (await videosPreloadingSemaphore.LockAsync(cancellationToken).ConfigureAwait(false))
                {
                    if (Videos.Count > 0)
                    {
                        return true;
                    }

                    IsLoading = true;
                    var videos = await videosFactory(this, cancellationToken).ConfigureAwait(false) ?? Array.Empty<Video>();
                    Videos = videos.OrderByDescending(l => l).ToList();

                    return Videos.Count > 0;
                }
            }
            catch (OperationCanceledException)
            {
                return Videos.Count > 0;
            }
            catch (Exception ex)
            {
                Videos = Array.Empty<Video>();

                // TODO move logging to FileManager layer
                // throw;
                ex.Data["Site"] = Site;
                ex.Data["FileId"] = Id;
                ex.Data["Item"] = ItemInfo;
                Logger.Instance.LogError(ex);
                return false;
            }
            finally
            {
                IsLoading = false;
                IsFailed = Videos.Count == 0;
            }
        }

        /// <summary>
        /// Request every video to get size
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        public async Task PreloadVideosSizeAsync(CancellationToken cancellationToken)
        {
            try
            {
                if (Videos.All(v => v.Size.HasValue))
                {
                    return;
                }

                using (await videosPreloadingSemaphore.LockAsync(cancellationToken))
                {
                    IsLoading = true;
                    Videos = (await Task
                        .WhenAll(Videos
                        .Select(async v =>
                        {
                            if (v.Size.HasValue)
                            {
                                return v;
                            }

                            v.Size = (await Task.WhenAll(v
                                .Links
                                .Select(l => httpClient.GetContentSizeAsync(l, v.CustomHeaders, cancellationToken)))
                                .ConfigureAwait(false))
                                .Sum();

                            return v;
                        }))
                        .ConfigureAwait(false))
                        .Where(v => v != null)
                        .ToArray();
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                Logger.Instance.LogError(ex);
            }
            finally
            {
                IsLoading = false;
                IsFailed = Videos.Count == 0;
            }
        }

        /// <summary>
        /// Get similar video by quality
        /// </summary>
        /// <param name="another">Similar to video</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Similar video</returns>
        public ValueTask<Video?> GetSimilarAsync(Video another, CancellationToken cancellationToken)
        {
            return GetByQualityAsync(another.Quality, true, cancellationToken);
        }

        /// <summary>
        /// Preloads file and get video by quality
        /// </summary>
        /// <param name="qual">Quality to found</param>
        /// <param name="ignoreLowPriority">Should ignore low priority videos</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Founded video</returns>
        public ValueTask<Video?> GetByQualityAsync(Quality qual, bool ignoreLowPriority, CancellationToken cancellationToken)
        {
            if (Videos.Count > 0)
            {
                return new ValueTask<Video?>(GetByQuality(qual, ignoreLowPriority));
            }
            else
            {
                return GetByQualityInternalAsync();
                async ValueTask<Video?> GetByQualityInternalAsync()
                {
                    var preloaded = await PreloadAsync(cancellationToken).ConfigureAwait(false);
                    return preloaded ? GetByQuality(qual, ignoreLowPriority) : null;
                }
            }
        }

        /// <summary>
        /// Get default video depending on <see cref="Settings.PreferredQuality"/>
        /// Do not ignores low priority videos
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Default video</returns>
        public ValueTask<Video?> GetDefaultAsync(CancellationToken cancellationToken)
        {
            return GetByQualityAsync(Settings.Instance.PreferredQuality, false, cancellationToken);
        }

        /// <summary>
        /// Get lower quality video
        /// </summary>
        /// <param name="current">Lower quality from video</param>
        /// <returns>Video with lower quality</returns>
        public Video? GetLowerQualityVideo(Video current)
        {
            if (Videos.Count == 0)
            {
                return null;
            }

            var curVideoIndex = Videos.IndexOf(current);
            var lowerVideoIndex = curVideoIndex + 1;
            return curVideoIndex < 0 || lowerVideoIndex >= Videos.Count
                ? null
                : Videos[lowerVideoIndex];
        }

        /// <summary>
        /// Get video by quality without preloading. Return null, if wasn't preloaded and hasn't videos
        /// </summary>
        /// <param name="qual">Quality to found</param>
        /// <param name="ignoreLowPriority">Should ignore low priority videos</param>
        /// <returns>Founded video</returns>
        public Video? GetByQuality(Quality qual, bool ignoreLowPriority)
        {
            if (Videos.Count == 0)
            {
                return null;
            }

            var orderedVids = Videos
                .Select(v => new { Video = v, Diff = Math.Abs(v.Quality - qual) })
                .OrderBy(o => o.Diff)
                .Select(o => o.Video)
                .ToArray();

            if (ignoreLowPriority)
            {
                return orderedVids.First();
            }

            var lowPriority = orderedVids
                .Where(v => v.LowPriority)
                .ToArray();

            return orderedVids
                .Except(lowPriority)
                .Union(lowPriority)
                .First();
        }

        /// <inheridoc/>
        public bool Equals(File other)
        {
            return Site == other?.Site && Id == other?.Id;
        }

        /// <inheridoc/>
        public override bool Equals(object obj)
        {
            return obj is File another && Equals(another);
        }

        /// <inheridoc/>
        public override int GetHashCode()
        {
            return (Site, Id).GetHashCode();
        }

        /// <inheridoc/>
        public override string ToString()
        {
            return Site + ": " + Id + (Season.HasValue ? " s" + Season : "") + (Episode.HasValue ? " e" + Episode.Value.ToFormattedString() : "");
        }

        public override IDictionary<string, string> GetLogProperties(bool verbose)
        {
            var props = base.GetLogProperties(verbose);

            var quality = Quality ?? Videos.FirstOrDefault()?.Quality.ToString();
            props.Add(nameof(Quality), quality ?? "Unknown");
            props.Add(nameof(IsTrailer), IsTrailer.ToString());
            props.Add(nameof(SubtitleTrack), (SubtitleTracks.Count > 0).ToString());
            props.Add("LazyFile", (videosFactory != defaultFactory).ToString());

            return props;
        }
    }
}
