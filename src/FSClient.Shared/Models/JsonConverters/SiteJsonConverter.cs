namespace FSClient.Shared.Models
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json;
    using System.Text.Json.Serialization;

    public class SiteJsonConverter : JsonConverter<Site>
    {
        public override Site Read(
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options)
        {
            switch (reader.TokenType)
            {
                case JsonTokenType.String:
                    {
                        var siteValue = reader.GetString();
                        return Site.Parse(siteValue, allowUnknown: true);
                    }
                case JsonTokenType.StartObject:
                    {
                        var site = JsonSerializer.Deserialize<Dictionary<string, string>>(ref reader, options)?[nameof(Site.Value)];
                        return Site.Parse(site, allowUnknown: true);
                    }
                default:
                    return Site.Any;
            }
        }

        public override void Write(
            Utf8JsonWriter writer,
            Site value,
            JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.Value);
        }
    }
}
