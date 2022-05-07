namespace FSClient.Shared.Providers
{
    using System;

    using FSClient.Shared.Mvvm;

    public class SeasonInfo : IncrementalGrouping<int, EpisodeInfo>
    {
    }

    public class EpisodeInfo : IEquatable<EpisodeInfo>
    {
        public int Season { get; set; }
        public int? Episode { get; set; }
        public string? Title { get; set; }
        public bool IsSpecial { get; set; }
        public DateTimeOffset? DateTime { get; set; }

        public bool IsNotAired => DateTime > DateTimeOffset.Now;

        public bool Equals(EpisodeInfo other)
        {
            return other != null
                && Season == other.Season
                && Episode == other.Episode;
        }

        public override bool Equals(object obj)
        {
            return obj is EpisodeInfo ep && Equals(ep);
        }

        public override int GetHashCode()
        {
            return (Season, Episode, Title, IsSpecial, DateTime).GetHashCode();
        }
    }
}
