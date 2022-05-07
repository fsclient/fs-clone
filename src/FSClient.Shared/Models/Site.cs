namespace FSClient.Shared.Models
{
    using System;
    using System.Collections.Concurrent;

    using FSClient.Localization.Resources;
    using FSClient.Shared.Helpers;

    public readonly struct Site : IEquatable<Site>
    {
        private static readonly ConcurrentDictionary<string, Site> sites
            = new ConcurrentDictionary<string, Site>();

        private static readonly string?[] SpecialValues = { null, "Local", "All" };

        private readonly string? value;
        private readonly string? title;

        private Site(string? val, string? display)
        {
            value = val;
            title = display;
        }

        public bool IsSpecial => Array.IndexOf(SpecialValues, value) >= 0;
        public string Value => value ?? "Any";
        public string Title => title ?? Strings.Site_Any;

        public static readonly Site All = GetOrCreate("All", Strings.Site_All);

        public static readonly Site Any;

        public static Site GetOrCreate(string value, string name)
        {
            if (value == null)
            {
                return default;
            }

            return sites.GetOrAdd(value.GetLettersAndDigits().ToLower(), _ => new Site(value, name));
        }

        public static Site Parse(string? value, in Site otherwise = default, bool allowUnknown = false)
        {
            return TryParse(value, out var site, allowUnknown)
                ? site
                : otherwise;
        }

        public static bool TryParse(string? value, out Site site, bool allowUnknown = false)
        {
            if (value?.GetLettersAndDigits().ToLower() is string key
                && sites.TryGetValue(key, out site))
            {
                return true;
            }
            else if (allowUnknown)
            {
                site = new Site(value, value);
                return true;
            }

            site = default;
            return false;
        }

        public bool Equals(Site other)
        {
            if (Value == null || Value == Any.Value)
            {
                return other.Value == null || other.Value == Any.Value;
            }

            return Value == other.Value;
        }

        public static bool operator ==(Site left, Site right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(Site left, Site right)
        {
            return !left.Equals(right);
        }

        public override bool Equals(object obj)
        {
            return obj is Site site && Equals(site);
        }

        public override int GetHashCode()
        {
            return Value?.GetHashCode() ?? -1;
        }

        public override string ToString()
        {
            return Title;
        }
    }
}
