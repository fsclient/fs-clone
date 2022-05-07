namespace FSClient.UWP.Shared.Converters
{
    using System;
    using System.Collections;
    using System.Globalization;
    using System.Linq;

#if WINUI3
    using Microsoft.UI.Xaml.Data;
#else
    using Windows.UI.Xaml.Data;
#endif

    using FSClient.Shared.Helpers;

    public class ToFriendlyStringConverter : IValueConverter
    {
        public string? DefaultFormat { get; set; }
        public string? DefaultLanguage { get; set; }

        public object? Convert(object? value, Type targetType, object? parameter, string? language)
        {
            var format = parameter as string ?? DefaultFormat;
            var limit = 0;
            var limitedArray = format != null && int.TryParse(format, out limit);

            var cultureProvider = (language.NotEmptyOrNull() ?? DefaultLanguage) is string lang
                ? new CultureInfo(lang)
                : CultureInfo.CurrentUICulture;

            switch (value)
            {
                case DateTimeOffset dateTimeOffset
                    when format != null:
                {
                    return dateTimeOffset.ToString(format, cultureProvider);
                }
                case DateTime date
                    when format != null:
                {
                    return date.ToString(format, cultureProvider);
                }
                case string s
                    when format == null:
                {
                    return string.Join(" ", s.Split('.', '_', '+'));
                }
                case Enum en:
                {
                    return en.GetDisplayName() ?? value.ToString();
                }
                case string[] strArr:
                {
                    return string.Join(", ", limitedArray ? strArr.Take(limit) : strArr);
                }
                case object[] objArr:
                {
                    var strArr = ((object[])value).Select(t => t.ToString());
                    return string.Join(", ", limitedArray ? strArr.Take(limit) : strArr);
                }
                case IEnumerable valueEnumerable:
                {
                    var strArr = valueEnumerable.Cast<object>().Select(t => t.ToString());
                    return string.Join(", ", limitedArray ? strArr.Take(limit) : strArr);
                }
                case Range range:
                    return string.Format(cultureProvider, format ?? "{0}", range.ToFormattedString());
                case null:
                    return string.Empty;
                default:
                    return string.Format(cultureProvider, format ?? "{0}", value);
            }
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, string? language)
        {
            if (value == null)
            {
                return null;
            }

            if (targetType == typeof(string))
            {
                return string.Join(".", ((string)value).Split(' '));
            }

            if (targetType == typeof(Array))
            {
                return ((string)value).Split(new[] {", "}, StringSplitOptions.RemoveEmptyEntries);
            }

            return value;
        }
    }
}
