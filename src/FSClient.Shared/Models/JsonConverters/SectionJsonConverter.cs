namespace FSClient.Shared.Models
{
    using System;
    using System.Text.Json;
    using System.Text.Json.Serialization;

    public class SectionJsonConverter : JsonConverter<Section>
    {
        public override Section Read(
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.String)
            {
                return default;
            }

            return Section.FromKindNameOrNull(reader.GetString()) ?? Section.Any;
        }

        public override void Write(
            Utf8JsonWriter writer,
            Section value,
            JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.GetKindNameOrNull());
        }
    }
}
