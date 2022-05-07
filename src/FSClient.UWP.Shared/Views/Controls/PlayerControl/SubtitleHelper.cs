namespace FSClient.UWP.Shared.Views.Controls
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net.Http;
    using System.Text;
    using System.Threading.Tasks;

    using Windows.Media.Core;
    using Windows.Media.Playback;

#if WINUI3
    using Microsoft.UI;
#else
    using Windows.UI;
#endif

    using FSClient.Shared.Helpers.Web;
    using FSClient.Shared.Models;
    using FSClient.Shared.Services;
    using FSClient.UWP.Shared.Services;

    public static class SubtitleHelper
    {
        private const TimedMetadataTrackPresentationMode enabledMode =
            TimedMetadataTrackPresentationMode.PlatformPresented;

        private const TimedMetadataTrackPresentationMode disabledMode = TimedMetadataTrackPresentationMode.Hidden;

        private static readonly HttpClient subHttpClient = new HttpClient();

        public static IEnumerable<TimedMetadataTrack> Process(this IEnumerable<TimedMetadataTrack> tracks,
            SubtitleTrack sub)
        {
            var newTracks = new List<TimedMetadataTrack>();
            try
            {
                foreach (var track in tracks)
                {
                    var newTrack = new TimedMetadataTrack(track.Id, track.Label, track.TimedMetadataKind);
#if DEBUG
                    newTrack.TrackFailed += (_, e) => Logger.Instance.LogWarning(e.Error.ExtendedError);
#endif
                    if (sub.Title != null)
                    {
                        newTrack.Label = sub.Title;
                    }

                    var cues = track.Cues.OfType<TimedTextCue>().ToList();

                    var subEndTime = sub.EndTime.HasValue
                        ? sub.EndTime.Value - sub.Offset
                        : cues.Last().StartTime;

                    foreach (var cue in cues)
                    {
                        var startTime = TimeSpan.FromMilliseconds(
                            (cue.StartTime.TotalMilliseconds * sub.SpeedModifier) + sub.Offset.TotalMilliseconds);

                        var duration = TimeSpan.FromMilliseconds(
                            cue.Duration.TotalMilliseconds * sub.SpeedModifier);

                        var endTime = startTime + duration;

                        if (endTime < TimeSpan.Zero
                            || startTime > subEndTime
                            || cue.Lines.All(l => string.IsNullOrWhiteSpace(l.Text)))
                        {
                            continue;
                        }

                        if (startTime < TimeSpan.Zero)
                        {
                            duration += startTime;
                            startTime = TimeSpan.FromMilliseconds(1);
                            endTime = startTime + duration;
                        }

                        if (endTime >= subEndTime)
                        {
                            duration = subEndTime - startTime;
                        }

                        var isRedunant = false;
                        foreach (var lastCue in newTrack.Cues.OfType<TimedTextCue>().Reverse())
                        {
                            if (lastCue.StartTime + lastCue.Duration < startTime - TimeSpan.FromMilliseconds(100)
                                || startTime + duration < lastCue.StartTime - TimeSpan.FromMilliseconds(100))
                            {
                                break;
                            }

                            if (lastCue.Lines.Select(l => l.Text).SequenceEqual(cue.Lines.Select(l => l.Text)))
                            {
                                if (lastCue.StartTime + lastCue.Duration >= endTime)
                                {
                                    isRedunant = true;
                                }
                                else
                                {
                                    var newStartTime = lastCue.StartTime + lastCue.Duration;
                                    duration -= newStartTime - startTime;
                                    startTime = newStartTime;
                                }

                                break;
                            }
                        }

                        if (isRedunant)
                        {
                            continue;
                        }

                        // Fix bug on mobile
                        // See https://wpdev.uservoice.com/forums/110705/suggestions/34329040
                        if (UWPAppInformation.Instance.DeviceFamily == DeviceFamily.Mobile
                            && newTrack.Cues.LastOrDefault() is IMediaCue previousCue)
                        {
                            if (previousCue.StartTime >= startTime)
                            {
                                startTime = previousCue.StartTime + TimeSpan.FromMilliseconds(1);
                            }
                            else if ((previousCue.StartTime + previousCue.Duration) is var previousEnd
                                     && previousCue.StartTime < startTime
                                     && previousEnd > endTime)
                            {
                                var leftShiftedDurtaion = startTime - previousCue.StartTime;
                                if (leftShiftedDurtaion > duration)
                                {
                                    previousCue.Duration = leftShiftedDurtaion;
                                }
                                else
                                {
                                    previousCue.StartTime = startTime - TimeSpan.FromMilliseconds(1);
                                    previousCue.Duration = duration;
                                }
                            }
                        }

                        var currentCue = new TimedTextCue
                        {
                            Id = cue.Id,
                            Duration = duration,
                            StartTime = startTime,
                            CueRegion = new TimedTextRegion
                            {
                                Background = cue.CueRegion.Background,
                                DisplayAlignment = cue.CueRegion.DisplayAlignment,
                                Extent = cue.CueRegion.Extent,
                                IsOverflowClipped = cue.CueRegion.IsOverflowClipped,
                                LineHeight = cue.CueRegion.LineHeight,
                                Name = cue.CueRegion.Name,
                                Padding = cue.CueRegion.Padding,
                                Position = cue.CueRegion.Position,
                                TextWrapping = cue.CueRegion.TextWrapping,
                                WritingMode = cue.CueRegion.WritingMode,
                                ZIndex = cue.CueRegion.ZIndex,
                                // ScrollMode=Popon (which is default) throws ArgumentException
                                ScrollMode = TimedTextScrollMode.Rollup
                            },
                            CueStyle = new TimedTextStyle
                            {
                                FontFamily = cue.CueStyle.FontFamily,
                                FontSize = cue.CueStyle.FontSize,
                                Background = cue.CueStyle.Background,
                                FlowDirection = cue.CueStyle.FlowDirection,
                                FontWeight = cue.CueStyle.FontWeight,
                                IsBackgroundAlwaysShown = cue.CueStyle.IsBackgroundAlwaysShown,
                                LineAlignment = cue.CueStyle.LineAlignment,
                                OutlineRadius = cue.CueStyle.OutlineRadius
                            }
                        };

                        currentCue.CueStyle.SetupAppStyle();

                        foreach (var line in cue.Lines)
                        {
                            currentCue.Lines.Add(new TimedTextLine {Text = line.Text.Replace("\\h", "")});
                        }

                        newTrack.AddCue(currentCue);
                    }

                    newTrack.TrackFailed += (_, aa) => Logger.Instance.LogWarning(aa.Error.ExtendedError);

                    newTracks.Add(newTrack);
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.LogError(ex);
            }

            return newTracks;
        }

        public static ValueTask<TimedTextSource?> GetTimedTextSourceAsync(this SubtitleTrack sub)
        {
            if (sub.CustomHeaders.Count == 0
                && !sub.Link.AbsolutePath.EndsWith(".srt", StringComparison.OrdinalIgnoreCase))
            {
                var tts = TimedTextSource.CreateFromUri(sub.Link);
                return new ValueTask<TimedTextSource?>(tts);
            }

            return new ValueTask<TimedTextSource?>(InternalGetTimeTextSourceFromSubtitleAsync(sub));

            static async Task<TimedTextSource?> InternalGetTimeTextSourceFromSubtitleAsync(SubtitleTrack inSub)
            {
                var netStream = await subHttpClient
                    .GetBuilder(inSub.Link)
                    .WithHeaders(inSub.CustomHeaders)
                    .SendAsync(default)
                    .AsStream()
                    .ConfigureAwait(true);

                if (netStream == null)
                {
                    return null;
                }

                if (inSub.Link.AbsolutePath.EndsWith(".srt", StringComparison.OrdinalIgnoreCase))
                {
                    using (var reader = new MemoryStream())
                    {
                        await netStream.CopyToAsync(reader);
                        // Hack to mimic srt subtitles as vtt, UWP player will understend it and read with correct encoding
                        var utfString = "WEBVTT\n\n" + Encoding.UTF8.GetString(reader.ToArray());
                        netStream.Dispose();
#pragma warning disable IDE0067 // Dispose objects before losing scope
                        netStream = new MemoryStream(Encoding.UTF8.GetBytes(utfString));
#pragma warning restore IDE0067 // Dispose objects before losing scope
                    }
                }

                var stream = netStream.AsRandomAccessStream();
                return TimedTextSource.CreateFromStream(stream);
            }
        }

        public static void SetupAppStyle(this TimedTextStyle style)
        {
            if (style.OutlineThickness.Value == 0)
            {
                style.Foreground = Colors.White;
                style.OutlineColor = Colors.Black;
                style.OutlineThickness = new TimedTextDouble {Value = 5, Unit = TimedTextUnit.Percentage};
            }
        }

        public static void ApplySubtitles(this IMediaPlaybackSource source, IList<TimedMetadataTrack> allowedTracks)
        {
            MediaPlaybackItem[] playbackItems;
            if (source is MediaPlaybackItem item)
            {
                playbackItems = new[] {item};
            }
            else if (source is MediaPlaybackList list)
            {
                playbackItems = list.Items.ToArray();
            }
            else
            {
                return;
            }

            foreach (var playbackItem in playbackItems)
            {
                for (uint index = 0; index < playbackItem.TimedMetadataTracks.Count; index++)
                {
                    var track = playbackItem.TimedMetadataTracks[(int)index];
                    var currentMode = playbackItem.TimedMetadataTracks.GetPresentationMode(index);

                    if (allowedTracks.IndexOf(track) >= 0
                        && currentMode != enabledMode)
                    {
                        playbackItem
                            .TimedMetadataTracks
                            .SetPresentationMode(index, enabledMode);
                    }
                    else if (currentMode == enabledMode)
                    {
                        playbackItem.TimedMetadataTracks
                            .SetPresentationMode(index, disabledMode);
                    }
                }
            }
        }
    }
}
