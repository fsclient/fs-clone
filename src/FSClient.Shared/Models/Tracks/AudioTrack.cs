namespace FSClient.Shared.Models
{
    using System;

    using FSClient.Shared.Helpers;

    /// <summary>
    /// Audio video file track
    /// </summary>
    public class AudioTrack : ITrack, IEquatable<AudioTrack?>
    {
        public AudioTrack(string? lang = null)
        {
            Index = -1;
            Language = LocalizationHelper.NormalizeLanguageName(lang);
            Title = LocalizationHelper.GetFriendlyLanguageTitle(lang, lang);
        }

        /// <inheridoc/>
        public int Index { get; set; }

        /// <inheridoc/>
        public string? Language { get; }

        /// <inheridoc/>
        public string? Title { get; set; }

        /// <inheridoc/>
        public override int GetHashCode()
        {
            return (Index, Language).GetHashCode();
        }

        /// <inheridoc/>
        public override bool Equals(object? obj)
        {
            return obj is AudioTrack track && Equals(track);
        }

        /// <inheridoc/>
        public bool Equals(AudioTrack? other)
        {
            return !(other is null) && other.Index == Index && other.Language == Language;
        }

        /// <inheridoc/>
        public override string? ToString()
        {
            return Title;
        }

        /// <inheridoc/>
        public static bool operator ==(AudioTrack? left, AudioTrack? right)
        {
            return ((left is null) && (right is null)) || (left?.Equals(right) ?? false);
        }

        /// <inheridoc/>
        public static bool operator !=(AudioTrack? left, AudioTrack? right)
        {
            return !(left == right);
        }
    }
}
