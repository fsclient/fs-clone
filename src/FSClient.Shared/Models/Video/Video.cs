namespace FSClient.Shared.Models
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    public class Video : IComparable<Video>, IEquatable<Video>
    {
        private const int BytesPerMegabyte = 1024 * 1024;
        private readonly string linkFileName;
        private string? fileName;

        public Video(params Uri[] links)
            : this(links.AsEnumerable())
        {
        }

        public Video(IEnumerable<Uri> parts)
            : this(new[] { new VideoVariant(parts) })
        {
        }

        public Video(IEnumerable<VideoVariant> variants)
        {
            Variants = variants?.OrderBy(v => v.Parts.Count).ToList() ?? new List<VideoVariant>();
            Links = Variants.FirstOrDefault()?.Parts ?? new List<Uri>();

            if (Links.Count == 1)
            {
                SingleLink = Links.FirstOrDefault();
                DownloadLink = SingleLink;
            }

            linkFileName = Uri.UnescapeDataString(Links.FirstOrDefault()?.Segments.LastOrDefault()?.Split('?').First() ?? string.Empty);
            Quality = linkFileName.Split('.').LastOrDefault();
            CustomHeaders = new Dictionary<string, string>();
        }

        public string FileName
        {
            get => fileName ?? linkFileName;
            set => fileName = value;
        }

        public bool HasValidFileName => fileName != null;

        public Uri? SingleLink { get; }
        public IReadOnlyList<Uri> Links { get; }
        public IReadOnlyList<VideoVariant> Variants { get; }
        public Uri? DownloadLink { get; set; }

        public File? ParentFile { get; set; }
        public Quality Quality { get; set; }
        public long? Size { get; set; }
        public long? SizeInMb => Size > BytesPerMegabyte ? Size / BytesPerMegabyte : null;
        public bool LowPriority { get; set; }

        public IDictionary<string, string> CustomHeaders { get; }

        public Video Clone()
        {
            return new Video(Links)
            {
                FileName = FileName,
                Quality = Quality,
                Size = Size,
                ParentFile = ParentFile
            };
        }

        public int CompareTo(Video? other)
        {
            return Quality.CompareTo(other?.Quality ?? 0);
        }

        public override bool Equals(object obj)
        {
            return obj is Video other && Equals(other);
        }

        public override int GetHashCode()
        {
            return (Links.FirstOrDefault(), Links.Count, Variants.Count).GetHashCode();
        }

        public bool Equals(Video? other)
        {
            return !(other is null)
                && Variants.SequenceEqual(other.Variants)
                && Links.SequenceEqual(other.Links)
                && Quality.CompareTo(other.Quality) == 0;
        }

        public override string ToString()
        {
            return ParentFile + " " + string.Join("; ", Links.Select(l => l.ToString()));
        }

        public static bool operator ==(Video? left, Video? right)
        {
            if (left is null)
            {
                return right is null;
            }

            return left.Equals(right);
        }

        public static bool operator !=(Video? left, Video? right)
        {
            return !(left == right);
        }

        public static bool operator <(Video? left, Video? right)
        {
            return left is null ? right is object : left.CompareTo(right) < 0;
        }

        public static bool operator <=(Video? left, Video? right)
        {
            return left is null || left.CompareTo(right) <= 0;
        }

        public static bool operator >(Video? left, Video? right)
        {
            return left is object && left.CompareTo(right) > 0;
        }

        public static bool operator >=(Video? left, Video? right)
        {
            return left is null ? right is null : left.CompareTo(right) >= 0;
        }
    }
}
