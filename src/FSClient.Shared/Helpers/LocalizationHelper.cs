namespace FSClient.Shared.Helpers
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Linq;
    using System.Text.RegularExpressions;

    using FSClient.Localization.Resources;

    using Humanizer;

    public static class LocalizationHelper
    {
        private static readonly Regex LangDetectionRegex =
            new Regex("(?:(?<RU>(?:rus?)|(?:рус?))|(?<EN>(?:eng?)|(?:англ?))|(?<UA>(?:ukr?)|(?:ua)|(?:укр))|(?<JP>(?:ja?p)|(?:яп)))", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public const string RuLang = "RU";
        public const string EnLang = "EN";
        public const string UaLang = "UK";
        public const string JpLang = "JP";

        [return: NotNullIfNotNull("name")]
        public static string? NormalizeLanguageName(string? name)
        {
            switch (name?.ToLower())
            {
                case "en":
                case "eng":
                case "анг":
                case "англ":
                    return EnLang;
                case "ru":
                case "rus":
                case "ру":
                case "рус":
                    return RuLang;
                case "ua":
                case "uk":
                case "ukr":
                case "укр":
                    return UaLang;
                case "jp":
                case "jap":
                case "яп":
                    return JpLang;
                default:
                    return name;
            }
        }

        public static string GetFriendlyLanguageTitle(string? key, string? otherwise = null)
        {
            switch (key?.ToLower())
            {
                case "en":
                case "eng":
                    return Strings.Language_English;
                case "ru":
                case "rus":
                    return Strings.Language_Russian;
                case "ua":
                case "uk":
                case "ukr":
                    return Strings.Language_Ukrainian;
                case "jp":
                case "jap":
                    return Strings.Language_Japanese;
                case "orig":
                    return Strings.Language_Original;
                default:
                    return otherwise ?? Strings.Language_Unknown;
            }
        }

        public static IEnumerable<string> DetectLanguageNames(string input)
        {
            return LangDetectionRegex
                .Matches(input).OfType<Match>()
                .SelectMany(match => match.Groups.OfType<Group>())
                .Select(group => NormalizeLanguageName(group.Value))
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct();
        }

        public static string GetElapsedTimeString(this DateTimeOffset date)
        {
            try
            {
                return date.Humanize(DateTimeOffset.Now).Transform(To.SentenceCase);
            }
            catch
            {
                var elapsed = DateTimeOffset.Now - date;

                if (elapsed < TimeSpan.FromMinutes(1))
                {
                    return "Только что";
                }

                if (elapsed < TimeSpan.FromMinutes(30))
                {
                    return "За последние пол часа";
                }

                if (elapsed < TimeSpan.FromHours(1))
                {
                    return "За последний час";
                }

                if (date.Date == DateTime.Today)
                {
                    return "Сегодня";
                }

                if (date.Date == (DateTime.Today - TimeSpan.FromDays(1)))
                {
                    return "Вчера";
                }

                if ((DateTime.Today - date.Date) < TimeSpan.FromDays(7))
                {
                    return "За последнюю неделю";
                }

                if ((DateTime.Today - date.Date) < TimeSpan.FromDays(30))
                {
                    return "За последний месяц";
                }

                return "Ранее";
            }
        }
    }
}
