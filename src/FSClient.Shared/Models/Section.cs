namespace FSClient.Shared.Models
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    using FSClient.Localization.Resources;
    using FSClient.Shared.Helpers;

    [Flags]
    public enum SectionModifiers
    {
        None = 0,
        Film = 1,
        Serial = Film << 1,
        Cartoon = Serial << 1,
        TVShow = Cartoon << 1,
        Anime = (TVShow << 1) | Cartoon
    }

    public struct Section : IEquatable<Section>
    {
        public static readonly Section Any;

        private readonly bool hasValue;
        private readonly string title;

        public Section(string value, string name = "")
        {
            Value = value;
            title = name;
            Modifier = SectionModifiers.None;

            hasValue = true;
        }

        public string Value { get; }
        public string Title => title ?? Strings.Section_All;

        public SectionModifiers Modifier { get; set; }

        public static Section CreateDefault(SectionModifiers sectionModifier)
        {
            if (sectionModifier == SectionModifiers.None)
            {
                return Any;
            }

            var (value, title) = GetSectionModifierTupleOrNull(sectionModifier)
                ?? throw new ArgumentException($"Section with {nameof(SectionModifiers)} = {sectionModifier} does not allowed to be created");

            return new Section(value, title)
            {
                Modifier = sectionModifier
            };
        }

        public static string GetTitleByModifier(SectionModifiers sectionModifier)
        {
            if (sectionModifier == SectionModifiers.None)
            {
                return Any.Title;
            }

            return GetSectionModifierTupleOrNull(sectionModifier)?.title
                ?? throw new ArgumentException($"Section with {nameof(SectionModifiers)} = {sectionModifier} does not allowed to be created");
        }

        public static Section? FromKindNameOrNull(string? kindName)
        {
            if (kindName == null)
            {
                return null;
            }

            var pair = GetSectionModifierTupleOrNull(kindName);
            if (pair == null)
            {
                return null;
            }

            return new Section(kindName, pair.Value.title)
            {
                Modifier = pair.Value.modifier
            };
        }

        public string? GetKindNameOrNull()
        {
            return GetSectionModifierTupleOrNull(Modifier)?.kindName;
        }

        public static bool IsModifierDefault(SectionModifiers sectionModifier)
        {
            return GetSectionModifierTupleOrNull(sectionModifier).HasValue;
        }

        public override string ToString()
        {
            return Title;
        }

        public override bool Equals(object obj)
        {
            return obj is Section section && Equals(section);
        }

        public bool Equals(Section other)
        {
            return hasValue == other.hasValue
                && Value == other.Value
                && Modifier == other.Modifier;
        }

        public override int GetHashCode()
        {
            return (hasValue, Value, Modifier).GetHashCode();
        }

        public static bool operator ==(Section left, Section right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(Section left, Section right)
        {
            return !left.Equals(right);
        }

        private static (string kindName, string title)? GetSectionModifierTupleOrNull(SectionModifiers sectionModifiers)
        {
            return sectionModifiers switch
            {
                SectionModifiers.Serial => ("serial", Strings.Section_Serials),
                SectionModifiers.Serial | SectionModifiers.Anime => ("animeserial", Strings.Section_AnimeSerial),
                SectionModifiers.Serial | SectionModifiers.Cartoon => ("cartoonserial", Strings.Section_CartoonSerial),
                SectionModifiers.Film => ("film", Strings.Section_Films),
                SectionModifiers.Film | SectionModifiers.Anime => ("anime", Strings.Section_Anime),
                SectionModifiers.Film | SectionModifiers.Cartoon => ("cartoon", Strings.Section_CartoonFilm),
                SectionModifiers.Cartoon => ("cartoon", Strings.Section_Cartoon),
                SectionModifiers.Anime => ("anime", Strings.Section_Anime),
                SectionModifiers.TVShow => ("tvshow", Strings.Section_TVShow),
                _ when sectionModifiers.HasFlag(SectionModifiers.TVShow) => GetSectionModifierTupleOrNull(SectionModifiers.TVShow),
                _ when sectionModifiers.GetFlags().ToArray() is { } flags && flags.Length > 1 => GetSectionModifierTupleOrNull(flags[0]),
                _ => null
            };
        }

        private static (SectionModifiers modifier, string title)? GetSectionModifierTupleOrNull(string? kindName)
        {
            return kindName switch
            {
                "serial" => (SectionModifiers.Serial, Strings.Section_Serials),
                "animeserial" => (SectionModifiers.Serial | SectionModifiers.Anime, Strings.Section_AnimeSerial),
                "cartoonserial" => (SectionModifiers.Serial | SectionModifiers.Cartoon, Strings.Section_CartoonSerial),
                "film" => (SectionModifiers.Film, Strings.Section_Films),
                "cartoon" => (SectionModifiers.Cartoon, Strings.Section_Cartoon),
                "anime" => (SectionModifiers.Anime, Strings.Section_Anime),
                "tvshow" => (SectionModifiers.TVShow, Strings.Section_TVShow),
                _ => null
            };
        }
    }
}
