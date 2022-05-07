namespace FSClient.Shared.Models
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    public class VideoVariant
    {
        public VideoVariant(params Uri[] parts)
            : this(parts.AsEnumerable())
        {
        }

        public VideoVariant(IEnumerable<Uri> parts)
        {
            Parts = parts?.Where(l => l != null).ToList() ?? new List<Uri>();
        }

        public IReadOnlyList<Uri> Parts { get; }

        public static explicit operator VideoVariant(Uri link)
        {
            return new VideoVariant(link);
        }

        public override string ToString()
        {
            return string.Join("; ", Parts.Select(l => l.ToString()));
        }
    }
}
