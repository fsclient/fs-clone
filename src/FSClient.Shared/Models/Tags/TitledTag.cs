namespace FSClient.Shared.Models
{
    using System;

    using FSClient.Localization.Resources;

    public struct TitledTag : IEquatable<TitledTag>
    {
        public static readonly TitledTag Any;

        private readonly string? title;
        private readonly bool hasValue;

        public TitledTag(string? title, Site site, string type, string? value)
        {
            hasValue = true;

            this.title = title;
            Site = site;
            Type = type;
            Value = value;
        }

        public TitledTag(Site site, string type, string? value)
            : this(null, site, type, value)
        {
        }

        public TitledTag(string? title)
            : this(title, Any.Site, Any.Type!, Any.Value)
        {
        }

        public Site Site { get; }
        public string? Type { get; }
        public string? Value { get; }
        public string? Title => title ?? (this == default ? Strings.TitledTag_Any : null);

        public override string? ToString()
        {
            return Title;
        }

        public bool Equals(TitledTag other)
        {
            return hasValue == other.hasValue
                && Site == other.Site
                && Type == other.Type
                && Value == other.Value;
        }

        public override bool Equals(object obj)
        {
            return obj is TitledTag tag && Equals(tag);
        }

        public override int GetHashCode()
        {
            return (hasValue, Site, Type, Value).GetHashCode();
        }

        public static bool operator ==(TitledTag left, TitledTag right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(TitledTag left, TitledTag right)
        {
            return !left.Equals(right);
        }
    }
}
