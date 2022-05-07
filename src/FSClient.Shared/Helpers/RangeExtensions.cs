namespace FSClient.Shared.Helpers
{
    using System;

    public static class RangeExtensions
    {
        public static bool HasRange(this Range current)
        {
            return !current.Start.Value.Equals(current.End.Value - 1);
        }

        public static string ToFormattedString(this Range current)
        {
            return current.HasRange()
                ? $"{current.Start.Value} - {current.End.Value - 1}"
                : current.Start.Value.ToString();
        }

        public static bool IsIntersected(this Range current, Range other)
        {
            return (current.End.Value <= other.End.Value
                && current.Start.Value >= other.Start.Value)
                || (other.End.Value <= current.End.Value
                    && other.End.Value >= current.Start.Value);
        }

        public static bool IsNear(this Range current, Range other)
        {
            return current.End.Value == other.Start.Value
                || other.End.Value == current.Start.Value
                || current.IsIntersected(other);
        }

        public static Range? ToRange(this int? intValue)
        {
            return intValue.HasValue
                ? new Range(intValue.Value, intValue.Value + 1)
                : (Range?)null;
        }

        public static Range ToRange(this int intValue)
        {
            return new Range(intValue, intValue + 1);
        }

#pragma warning disable RCS1224 // Make method an extension method.
        public static bool TryParse(string? line, out Range range)
#pragma warning restore RCS1224 // Make method an extension method.
        {
            if (line == null)
            {
                range = default;
                return false;
            }

            if (int.TryParse(line, out var left))
            {
                range = new Range(left, left + 1);
                return true;
            }

            var lineParts = line
                .Split(new[] { " ", "-", ",", ".." }, System.StringSplitOptions.RemoveEmptyEntries);
            if (lineParts.Length > 1
                && int.TryParse(lineParts[0], out left)
                && int.TryParse(lineParts[1], out var right))
            {
                range = new Range(left, right + 1);
                return true;
            }

            range = default;
            return false;
        }
    }
}
