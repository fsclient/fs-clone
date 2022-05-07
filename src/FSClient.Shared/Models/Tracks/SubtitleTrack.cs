namespace FSClient.Shared.Models
{
    using System;
    using System.Collections.Generic;

    using FSClient.Shared.Helpers;

    /// <summary>
    /// Subtitle video file track
    /// </summary>
    public class SubtitleTrack : ITrack, IEquatable<SubtitleTrack?>
    {
        public SubtitleTrack(string? lang, Uri link)
        {
            if (link == null)
            {
                throw new ArgumentNullException(nameof(link));
            }
            if (!link.IsAbsoluteUri)
            {
                throw new ArgumentException($"{nameof(SubtitleTrack)}.{nameof(Link)} must be absolute uri");
            }

            Index = -1;
            Language = LocalizationHelper.NormalizeLanguageName(lang);
            Link = link;
            SpeedModifier = 1;
            Title = LocalizationHelper.GetFriendlyLanguageTitle(lang, lang);
            CustomHeaders = new Dictionary<string, string>();
        }

        /// <summary>
        /// Subtitle file link
        /// </summary>
        public Uri Link { get; }

        /// <inheridoc/>
        public int Index { get; set; }

        /// <inheridoc/>
        public string? Language { get; }

        /// <inheridoc/>
        public string? Title { get; set; }

        /// <summary>
        /// Subtitle`s cue change time speed modifier
        /// </summary>
        public double SpeedModifier { get; set; }

        /// <summary>
        /// Offset before first subtitle`s cue
        /// </summary>
        public TimeSpan Offset { get; set; }

        /// <summary>
        /// Subtitle time limit.
        /// Application ignores cues after <see cref="EndTime"/> moment.
        /// </summary>
        public TimeSpan? EndTime { get; set; }

        /// <summary>
        /// Custom headers to get subtitle by <see cref="Link"/>
        /// </summary>
        public Dictionary<string, string> CustomHeaders { get; set; }

        /// <inheridoc/>
        public override int GetHashCode()
        {
            return Link?.GetHashCode() ?? 0;
        }

        /// <inheridoc/>
        public override bool Equals(object? obj)
        {
            return obj is SubtitleTrack sub && Equals(sub);
        }

        /// <inheridoc/>
        public bool Equals(SubtitleTrack? other)
        {
            return !(other is null) && other.Link == Link;
        }

        /// <inheridoc/>
        public object Clone()
        {
            return (SubtitleTrack)MemberwiseClone();
        }

        /// <inheridoc/>
        public override string? ToString()
        {
            return Title;
        }

        /// <inheridoc/>
        public static bool operator ==(SubtitleTrack? left, SubtitleTrack? right)
        {
            return (left is null && right is null) || (left?.Equals(right) ?? false);
        }

        /// <inheridoc/>
        public static bool operator !=(SubtitleTrack? left, SubtitleTrack? right)
        {
            return !(left == right);
        }
    }
}
