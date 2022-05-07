namespace FSClient.Shared.Models
{
    using System;
    using System.Text.Json;
    using System.Text.Json.Serialization;

    public class WebImageJsonConverter : JsonConverter<WebImage>
    {
        public override WebImage Read(
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.String
                || !Uri.TryCreate(reader.GetString(), UriKind.Absolute, out var link))
            {
                return default;
            }

            return new WebImage
            {
                [ImageSize.Preview] = link
            };
        }

        public override void Write(
            Utf8JsonWriter writer,
            WebImage value,
            JsonSerializerOptions options)
        {
            writer.WriteStringValue(value[ImageSize.Preview]?.ToString());
        }
    }
}
