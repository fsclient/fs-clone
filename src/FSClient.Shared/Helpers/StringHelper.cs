namespace FSClient.Shared.Helpers
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Text;

    using FSClient.Shared.Comparers;
    using FSClient.Shared.Services;

    public static class StringHelper
    {
        public static string? NotEmptyOrNull(this string? str)
        {
            return string.IsNullOrEmpty(str) ? null : str;
        }

        public static int? ToIntOrNull(this string? str)
        {
            return int.TryParse(str, out var value) ? (int?)value : null;
        }

        public static double? ToDoubleOrNull(this string? str)
        {
            return double
                .TryParse(
                    str?.Replace(',', '.'),
                    NumberStyles.Any,
                    CultureInfo.InvariantCulture,
                    out var i)
                ? i : (double?)null;
        }

        public static float? ToFloatOrNull(this string? str)
        {
            return float
                .TryParse(
                    str?.Replace(',', '.'),
                    NumberStyles.Any,
                    CultureInfo.InvariantCulture,
                    out var i)
                ? i : (float?)null;
        }

        public static Uri? ToHttpUriOrNull(this string? str)
        {
            if (str == null)
            {
                return null;
            }

            if (str.StartsWith("http", StringComparison.Ordinal))
            {
                return ToUriOrNull(str);
            }

            if (str.StartsWith("//", StringComparison.Ordinal))
            {
                return ToUriOrNull("http:" + str);
            }

            return ToUriOrNull("http://" + str);
        }

        public static Uri? ToUriOrNull(this string? str, UriKind uriKind = UriKind.RelativeOrAbsolute)
        {
            if (string.IsNullOrEmpty(str))
            {
                return null;
            }

            try
            {
                _ = Uri.TryCreate(str, uriKind, out var parsedUri);
                return parsedUri;
            }
            catch (Exception ex)
            {
                Logger.Instance.LogWarning(ex);
                return null;
            }
        }

        public static Uri? ToUriOrNull(this string? str, Uri baseUri)
        {
            if (string.IsNullOrEmpty(str))
            {
                return null;
            }

            try
            {
                _ = Uri.TryCreate(baseUri, str, out var parsedUri);
                return parsedUri;
            }
            catch (Exception ex)
            {
                Logger.Instance.LogWarning(ex);
                return null;
            }
        }

        public static double Proximity(this string left, string right, bool caseSensitive = true)
        {
            const double mWeightThreshold = 0.7;
            const int mNumChars = 4;

            if (!caseSensitive)
            {
                left = left.ToUpperInvariant();
                right = right.ToUpperInvariant();
            }

            var lLen1 = left.Length;
            var lLen2 = right.Length;
            if (lLen1 == 0)
            {
                return lLen2 == 0 ? 1.0 : 0.0;
            }

            var lSearchRange = Math.Max(0, (Math.Max(lLen1, lLen2) / 2) - 1);

            var lMatched1 = new bool[lLen1];
            var lMatched2 = new bool[lLen2];

            var lNumCommon = 0;
            for (var i = 0; i < lLen1; ++i)
            {
                var lStart = Math.Max(0, i - lSearchRange);
                var lEnd = Math.Min(i + lSearchRange + 1, lLen2);
                for (var j = lStart; j < lEnd; ++j)
                {
                    if (lMatched2[j])
                    {
                        continue;
                    }

                    if (left[i] != right[j])
                    {
                        continue;
                    }

                    lMatched1[i] = true;
                    lMatched2[j] = true;
                    ++lNumCommon;
                    break;
                }
            }
            if (lNumCommon == 0)
            {
                return 0.0;
            }

            var lNumHalfTransposed = 0;
            var k = 0;
            for (var i = 0; i < lLen1; ++i)
            {
                if (!lMatched1[i])
                {
                    continue;
                }

                while (!lMatched2[k])
                {
                    ++k;
                }

                if (left[i] != right[k])
                {
                    ++lNumHalfTransposed;
                }

                ++k;
            }
            var lNumTransposed = lNumHalfTransposed / 2;

            double lNumCommonD = lNumCommon;
            var lWeight = ((lNumCommonD / lLen1)
                + (lNumCommonD / lLen2)
                + ((lNumCommon - lNumTransposed) / lNumCommonD)) / 3.0;

            if (lWeight <= mWeightThreshold)
            {
                return lWeight;
            }

            var lMax = Math.Min(mNumChars, Math.Min(left.Length, right.Length));
            var lPos = 0;
            while (lPos < lMax && left[lPos] == right[lPos])
            {
                ++lPos;
            }

            if (lPos == 0)
            {
                return lWeight;
            }

            return lWeight + (0.1 * lPos * (1.0 - lWeight));
        }

        public static string RemoveCharacters(this string str, params char[] characters)
        {
            return string.Concat(str.Split(characters));
        }

        public static string RemoveSpecialCharacters(this string str)
        {
            var sb = new StringBuilder();
            foreach (var c in str)
            {
                if (char.IsLetterOrDigit(c)
                    || c == ' ')
                {
                    _ = sb.Append(c);
                }
            }

            return sb.ToString();
        }

        public static string GetLettersAndDigits(this string str)
        {
            var sb = new StringBuilder();
            foreach (var c in str)
            {
                if (char.IsLetterOrDigit(c))
                {
                    _ = sb.Append(c);
                }
            }

            return sb.ToString();
        }

        public static string GetDigits(this string str)
        {
            var sb = new StringBuilder();
            foreach (var c in str)
            {
                if (char.IsDigit(c))
                {
                    _ = sb.Append(c);
                }
            }

            return sb.ToString();
        }

        public static string GetLetters(this string str)
        {
            var sb = new StringBuilder();
            foreach (var c in str)
            {
                if (char.IsLetter(c))
                {
                    _ = sb.Append(c);
                }
            }

            return sb.ToString();
        }

        public static void SortStrings<T>(this List<T> list, Func<T, string> stringFunc)
        {
            list.Sort((l, r) => default(NumericStringComparer)
                .Compare(stringFunc(l) ?? string.Empty, stringFunc(r) ?? string.Empty));
        }

        /// <summary>
        /// Splits a string into substrings that are based on the characters in an array. 
        /// Source: https://stackoverflow.com/questions/28178519/is-there-a-lazy-string-split-in-c-sharp
        /// </summary>
        /// <param name="value">The string to split.</param>
        /// <param name="options"><see cref="StringSplitOptions.RemoveEmptyEntries"/> to omit empty array elements from the array returned; or <see cref="StringSplitOptions.None"/> to include empty array elements in the array returned.</param>
        /// <param name="count">The maximum number of substrings to return.</param>
        /// <param name="separator">A character array that delimits the substrings in this string, an empty array that contains no delimiters, or null. </param>
        /// <returns></returns>
        /// <remarks>
        /// Delimiter characters are not included in the elements of the returned array. 
        /// If this instance does not contain any of the characters in separator the returned sequence consists of a single element that contains this instance.
        /// If the separator parameter is null or contains no characters, white-space characters are assumed to be the delimiters. White-space characters are defined by the Unicode standard and return true if they are passed to the <see cref="char.IsWhiteSpace"/> method.
        /// </remarks>
        public static IEnumerable<string> SplitLazy(this string value, int count = int.MaxValue, StringSplitOptions options = StringSplitOptions.None, params char[] separator)
        {
            if (count <= 0)
            {
                if (count < 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(count), "Count cannot be less than zero.");
                }

                yield break;
            }

            Func<char, bool> predicate = char.IsWhiteSpace;
            if (separator != null && separator.Length != 0)
            {
                predicate = (c) => separator.Contains(c);
            }

            if (string.IsNullOrEmpty(value) || count == 1 || !value.Any(predicate))
            {
                yield return value;
                yield break;
            }

            var removeEmptyEntries = (options & StringSplitOptions.RemoveEmptyEntries) != 0;
            var ct = 0;
            var sb = new StringBuilder();
            for (var i = 0; i < value.Length; ++i)
            {
                var c = value[i];
                if (!predicate(c))
                {
                    sb.Append(c);
                }
                else
                {
                    if (sb.Length != 0)
                    {
                        yield return sb.ToString();
                        sb.Clear();
                    }
                    else
                    {
                        if (removeEmptyEntries)
                        {
                            continue;
                        }

                        yield return string.Empty;
                    }

                    if (++ct >= count - 1)
                    {
                        if (removeEmptyEntries)
                        {
                            while (++i < value.Length && predicate(value[i]))
                            {
                                ;
                            }
                        }
                        else
                        {
                            ++i;
                        }

                        if (i < value.Length - 1)
                        {
                            sb.Append(value, i, value.Length - i);
                            yield return sb.ToString();
                        }
                        yield break;
                    }
                }
            }

            if (sb.Length > 0)
            {
                yield return sb.ToString();
            }
            else if (!removeEmptyEntries && predicate(value[^1]))
            {
                yield return string.Empty;
            }
        }

        public static IEnumerable<string> SplitLazy(this string value, int count, params char[] separator)
        {
            return value.SplitLazy(count, StringSplitOptions.None, separator);
        }

        public static uint GetDeterministicHashCode(this string str)
        {
            unchecked
            {
                var hash1 = (5381 << 16) + 5381u;
                var hash2 = hash1;

                for (var i = 0; i < str.Length; i += 2)
                {
                    hash1 = ((hash1 << 5) + hash1) ^ str[i];
                    if (i == str.Length - 1)
                    {
                        break;
                    }
                    hash2 = ((hash2 << 5) + hash2) ^ str[i + 1];
                }

                return hash1 + (hash2 * 1566083941);
            }
        }
    }
}
