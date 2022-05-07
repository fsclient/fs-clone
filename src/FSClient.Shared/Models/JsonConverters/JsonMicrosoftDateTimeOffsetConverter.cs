namespace FSClient.Shared.Models
{
    using System;
    using System.Globalization;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using System.Text.RegularExpressions;

    /// <summary>
    /// <see cref="JsonConverterFactory"/> to convert <see cref="DateTimeOffset"/> to and from strings in the Microsoft "\/Date()\/" format. Supports <see cref="Nullable{DateTimeOffset}"/>.
    /// </summary>
    /// <remarks>Adapted from code posted on: <a href="https://github.com/dotnet/runtime/issues/30776">dotnet/runtime #30776</a>.</remarks>
    public class JsonMicrosoftDateTimeOffsetConverter : JsonConverterFactory
    {
        public override bool CanConvert(Type typeToConvert)
        {
            return typeToConvert == typeof(DateTimeOffset)
                || (typeToConvert.IsGenericType && IsNullableDateTimeOffset(typeToConvert));
        }

        public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
        {
            return typeToConvert.IsGenericType
                ? (JsonConverter)new JsonNullableDateTimeOffsetConverter()
                : new JsonStandardDateTimeOffsetConverter();
        }

        private static bool IsNullableDateTimeOffset(Type typeToConvert)
        {
            var UnderlyingType = Nullable.GetUnderlyingType(typeToConvert);

            return UnderlyingType != null && UnderlyingType == typeof(DateTimeOffset);
        }

        internal class JsonStandardDateTimeOffsetConverter : JsonDateTimeOffsetConverter<DateTimeOffset>
        {
            public override DateTimeOffset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                return ReadDateTimeOffset(ref reader);
            }

            public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options)
            {
                WriteDateTimeOffset(writer, value);
            }
        }

        internal class JsonNullableDateTimeOffsetConverter : JsonDateTimeOffsetConverter<DateTimeOffset?>
        {
            public override DateTimeOffset? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                return ReadDateTimeOffset(ref reader);
            }

            public override void Write(Utf8JsonWriter writer, DateTimeOffset? value, JsonSerializerOptions options)
            {
                WriteDateTimeOffset(writer, value!.Value);
            }
        }

        internal abstract class JsonDateTimeOffsetConverter<T> : JsonConverter<T>
        {
            private static readonly DateTimeOffset s_Epoch = new DateTimeOffset(1970, 1, 1, 0, 0, 0, TimeSpan.Zero);
            private static readonly Regex s_Regex = new Regex("^/Date\\((-?)([^+-]+)([+-])(\\d{2})(\\d{2})\\)/$", RegexOptions.CultureInvariant | RegexOptions.Compiled);

            public static DateTimeOffset ReadDateTimeOffset(ref Utf8JsonReader reader)
            {
                if (reader.TokenType != JsonTokenType.String)
                {
                    throw new JsonException("DeserializeUnableToConvertValue");
                }

                var formatted = reader.GetString()!;
                var match = s_Regex.Match(formatted);

                if (!match.Success
                    || !long.TryParse(match.Groups[2].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var unixTime)
                    || !int.TryParse(match.Groups[4].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var hours)
                    || !int.TryParse(match.Groups[5].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var minutes))
                {
                    throw new JsonException("DeserializeUnableToConvertValue");
                }

                // Invalid NewtonsoftJson value, negative timestamp.
                if (match.Groups[1].Value == "-")
                {
                    return s_Epoch;
                }

                var sign = match.Groups[3].Value[0] == '+' ? 1 : -1;
                var utcOffset = TimeSpan.FromMinutes((sign * hours * 60) + minutes);

                return s_Epoch.AddMilliseconds(unixTime).ToOffset(utcOffset);
            }

            public static void WriteDateTimeOffset(Utf8JsonWriter writer, DateTimeOffset value)
            {
                var unixTime = Convert.ToInt64((value - s_Epoch).TotalMilliseconds);
                var utcOffset = value.Offset;

                var formatted = FormattableString.Invariant($"/Date({unixTime}{(utcOffset >= TimeSpan.Zero ? "+" : "-")}{utcOffset:hhmm})/");
                writer.WriteStringValue(formatted);
            }
        }
    }

}
