namespace FSClient.Shared.Models
{
    using System;

    public readonly struct Quality : IComparable<Quality>, IEquatable<Quality>
    {
        public static readonly Quality Any = "Any";

        public Quality(string title, int value)
        {
            Title = title;
            Value = value;
        }

        public string? Title { get; }
        public int Value { get; }

        public bool IsHD => Value >= 720;
        public bool IsUnknown => Value <= 1;
        public bool IsAny => Value == 0;

        public static implicit operator Quality(string? s)
        {
            if (string.IsNullOrEmpty(s))
            {
                return new Quality();
            }

            var title = s!;
            var quality = 0;

            if (int.TryParse(s!.TrimEnd('p'), out var t))
            {
                quality = t;
                title = t + "p";
            }
            else if (s.Contains("4K"))
            {
                quality = 2160;
            }
            else if (s.Contains("2K"))
            {
                quality = 1081;
            }
            else if (s.Contains("1080"))
            {
                quality = 1080;
            }
            else if (s.Contains("720"))
            {
                quality = 720;
            }
            else if (s.Contains("480"))
            {
                quality = 480;
            }
            else if (s.Contains("360"))
            {
                quality = 360;
            }
            else if (s.Contains("HD"))
            {
                quality = 720;
            }
            else if (s.Contains("HQ"))
            {
                quality = 720;
            }
            else if (s.Contains("SD"))
            {
                quality = 480;
            }
            else if (s.Contains("LQ"))
            {
                quality = 360;
            }
            else
            {
                quality = 1;
            }

            return new Quality(title.Trim(), quality);
        }

        public static implicit operator string(Quality q)
        {
            return q.ToString();
        }

        public static implicit operator Quality(int qual)
        {
            return new Quality(qual + "p", qual);
        }

        public static implicit operator int(Quality q)
        {
            return q.Value;
        }

        public static bool operator ==(Quality l, Quality r)
        {
            return l.CompareTo(r) == 0;
        }

        public static bool operator !=(Quality l, Quality r)
        {
            return l.CompareTo(r) != 0;
        }

        public int CompareTo(Quality other)
        {
            return Value.CompareTo(other.Value);
        }

        public override string ToString()
        {
            return Title ?? "Unknown";
        }

        public override bool Equals(object obj)
        {
            return obj is Quality quality && Equals(quality);
        }

        public bool Equals(Quality other)
        {
            return Value == other.Value;
        }

        public override int GetHashCode()
        {
            return Value.GetHashCode();
        }

        public static bool operator <(Quality left, Quality right)
        {
            return left.CompareTo(right) < 0;
        }

        public static bool operator <=(Quality left, Quality right)
        {
            return left.CompareTo(right) <= 0;
        }

        public static bool operator >(Quality left, Quality right)
        {
            return left.CompareTo(right) > 0;
        }

        public static bool operator >=(Quality left, Quality right)
        {
            return left.CompareTo(right) >= 0;
        }
    }
}
