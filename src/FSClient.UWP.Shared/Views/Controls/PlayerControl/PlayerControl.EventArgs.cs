namespace FSClient.UWP.Shared.Views.Controls
{
    using System;
    using System.Collections.Generic;

    using Windows.Media.Playback;

    using FSClient.Shared.Models;

    public class VideoEventArgs : EventArgs
    {
        public VideoEventArgs(Video? v = null)
        {
            Video = v;
        }

        public Video? Video { get; }
    }

    public class VolumeEventArgs : EventArgs
    {
        public VolumeEventArgs(double newValue)
        {
            NewValue = newValue;
        }

        public VolumeEventArgs(double newValue, double oldValue)
            : this(newValue)
        {
            OldValue = oldValue;
        }

        public double NewValue { get; }
        public double? OldValue { get; }
        public double? Delta => OldValue.HasValue ? OldValue.Value - NewValue : (double?)null;

        public bool Save { get; set; }
    }

    public class PositionEventArgs : EventArgs
    {
        public PositionEventArgs(TimeSpan newValue, PositionChangeType changeType, TimeSpan? duration = null)
        {
            NewValue = newValue;
            ChangeType = changeType;
            Duration = duration;
        }

        public PositionEventArgs(TimeSpan newValue, TimeSpan oldValue, PositionChangeType changeType,
            TimeSpan? duration = null)
            : this(newValue, changeType, duration)
        {
            OldValue = oldValue;
        }

        public TimeSpan NewValue { get; }
        public TimeSpan? OldValue { get; }
        public TimeSpan? Delta => OldValue.HasValue ? NewValue - OldValue.Value : (TimeSpan?)null;

        public TimeSpan? Duration { get; }

        public PositionChangeType ChangeType { get; }
    }

    public class PlayerStateEventArgs : EventArgs
    {
        public PlayerStateEventArgs(MediaPlaybackState state)
        {
            State = state;
        }

        public MediaPlaybackState State { get; }
    }

    public class BufferedRangesChangedEventArgs : EventArgs
    {
        public BufferedRangesChangedEventArgs(IEnumerable<MediaRangedProgressBarRange> ranges, TimeSpan totalDuration)
        {
            Ranges = ranges;
            TotalDuration = totalDuration;
        }

        public IEnumerable<MediaRangedProgressBarRange> Ranges { get; }

        public TimeSpan TotalDuration { get; }
    }

    public class BufferingEventArgs : EventArgs
    {
        public BufferingEventArgs(double progress)
        {
            Progress = progress;
        }

        public double Progress { get; }

        public bool Active => Progress >= 0 && Progress < 1;
    }

    public class PlaybackRateEventArgs : EventArgs
    {
        public PlaybackRateEventArgs(double playbackRate)
        {
            PlaybackRate = playbackRate;
        }

        public double PlaybackRate { get; }
    }
}
