namespace FSClient.Shared.Models
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json;
    using System.Text.Json.Serialization;

    public class TitledTagJsonConverter : JsonConverter<TitledTag>
    {
        public override TitledTag Read(
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartObject)
            {
                return default;
            }

            var obj = JsonSerializer.Deserialize<Dictionary<string, string>>(ref reader, options);
            if (obj == null)
            {
                return default;
            }
            return new TitledTag(
                obj[nameof(TitledTag.Title)]?.ToString(),
                Site.Parse(obj[nameof(TitledTag.Site)]?.ToString()),
                obj[nameof(TitledTag.Type)]!.ToString(),
                obj[nameof(TitledTag.Value)]!.ToString());
        }

        public override void Write(
            Utf8JsonWriter writer,
            TitledTag value,
            JsonSerializerOptions options)
        {
            JsonSerializer.Serialize(writer, new { value.Title, value.Site, value.Type, value.Value }, options);
        }
    }
}
