namespace FSClient.Shared.Models
{
    using System;
    using System.Text.Json;
    using System.Text.Json.Serialization;

    using FSClient.Shared.Helpers;

    public class RangeJsonConverter : JsonConverter<Range?>
    {
        public override Range? Read(
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.String)
            {
                return default;
            }

            return RangeExtensions.TryParse(reader.GetString(), out var r)
                ? r
                : null;
        }

        public override void Write(
            Utf8JsonWriter writer,
            Range? value,
            JsonSerializerOptions options)
        {
            writer.WriteStringValue(value?.ToFormattedString());
        }
    }
}
