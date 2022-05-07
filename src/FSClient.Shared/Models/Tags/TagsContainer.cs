namespace FSClient.Shared.Models
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    using FSClient.Localization.Resources;

    public class TagsContainer
    {
        public TagsContainer(string title, params TitledTag[] items)
        {
            Items = items;
            Title = title;
        }

        public TagsContainer(TagType tagType, params TitledTag[] items)
        {
            TagType = tagType;
            Items = items;

            var (singular, plural) = GetTagTypeTitle(tagType);
            Title = items.Length > 1 ? plural : singular;
        }

        public TagType TagType { get; set; }
        public string Title { get; }
        public IEnumerable<TitledTag> Items { get; }

        public override string ToString()
        {
            return Title + ": " + string.Join(", ", Items.Select(i => i.Title));
        }

        private static (string singular, string plural) GetTagTypeTitle(TagType tagType)
        {
            return tagType switch
            {
                TagType.Actor => (Strings.TagType_Actor_One, Strings.TagType_Actor_Other),
                TagType.Genre => (Strings.TagType_Genre_One, Strings.TagType_Genre_Other),
                TagType.Director => (Strings.TagType_Director_One, Strings.TagType_Director_Other),
                TagType.County => (Strings.TagType_County_One, Strings.TagType_County_Other),
                TagType.Writter => (Strings.TagType_Writter_One, Strings.TagType_Writter_Other),
                TagType.Studio => (Strings.TagType_Studio_One, Strings.TagType_Studio_Other),
                TagType.Composer => (Strings.TagType_Composer_One, Strings.TagType_Composer_Other),
                TagType.Duration => (Strings.TagType_Duration, Strings.TagType_Duration),
                _ => throw new NotSupportedException($"TagType={nameof(tagType)} is not supported in {nameof(GetTagTypeTitle)}.")
            };
        }
    }
}
