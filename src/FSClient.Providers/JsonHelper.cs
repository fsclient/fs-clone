namespace FSClient.Providers
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Net.Http;
    using System.Text;
    using System.Text.Json;
    using System.Threading.Tasks;

    using FSClient.Shared.Helpers;
    using FSClient.Shared.Helpers.Web;
    using FSClient.Shared.Services;

    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    public static class JsonHelper
    {
        public static JsonElement? ToPropertyOrNull(this JsonElement token, string key)
        {
            return token.ValueKind == JsonValueKind.Object && token.TryGetProperty(key, out var value) && value.ValueKind != JsonValueKind.Undefined ? value : null;
        }

        public static IEnumerable<JsonElement> ToItems(this JsonElement token)
        {
            return token.ValueKind == JsonValueKind.Array ? token.EnumerateArray() : Enumerable.Empty<JsonElement>();
        }

        public static int? ToIntOrNull(this JsonElement token)
        {
            return token.ValueKind == JsonValueKind.Number && token.TryGetInt32(out var value) ? value
                : int.TryParse(token.ToStringOrNull(), out var strValue) ? strValue
                : null;
        }

        public static long? ToLongOrNull(this JsonElement token)
        {
            return token.ValueKind == JsonValueKind.Number && token.TryGetInt64(out var value) ? value
                : long.TryParse(token.ToStringOrNull(), out var strValue) ? strValue
                : null;
        }

        public static double? ToDoubleOrNull(this JsonElement token)
        {
            return token.ValueKind == JsonValueKind.Number && token.TryGetDouble(out var value) ? value
                : double.TryParse(token.ToStringOrNull()?.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out var strValue) ? strValue
                : null;
        }

        public static bool? ToBoolOrNull(this JsonElement token)
        {
            return token.ValueKind == JsonValueKind.True ? true
                : token.ValueKind == JsonValueKind.False ? false
                : bool.TryParse(token.ToStringOrNull(), out var strValue) ? strValue : null;
        }

        public static string? ToStringOrNull(this JsonElement token)
        {
            return token.ValueKind switch
            {
                JsonValueKind.String => token.GetString(),
                JsonValueKind.Number => token.ToString(),
                JsonValueKind.True => token.ToString(),
                JsonValueKind.False => token.ToString(),
                _ => null
            };
        }

        public static Uri? ToUriOrNull(this JsonElement token, Uri? baseUri = null)
        {
            return baseUri != null
                ? token.ToStringOrNull()?.ToUriOrNull(baseUri)
                : token.ToStringOrNull()?.ToUriOrNull();
        }

        public static int? ToIntOrNull(this JToken token)
        {
            if (token.Type == JTokenType.Integer)
            {
                // json numeric value can be bigger, than int.MaxValue
                var longValue = token.ToObject<long>();
                return longValue < int.MaxValue && longValue > int.MinValue
                    ? (int)longValue
                    : (int?)null;
            }

            if (token.Type == JTokenType.String
                && int.TryParse(token.ToString(), out var i))
            {
                return i;
            }

            return null;
        }

        public static long? ToLongOrNull(this JToken token)
        {
            if (token.Type == JTokenType.Integer)
            {
                var longValue = token.ToObject<long>();
                return longValue;
            }

            if (token.Type == JTokenType.String
                && long.TryParse(token.ToString(), out var i))
            {
                return i;
            }

            return null;
        }

        public static double? ToDoubleOrNull(this JToken token)
        {
            if (token.Type == JTokenType.Integer)
            {
                return token.ToObject<int>();
            }

            if (token.Type == JTokenType.Float)
            {
                return token.ToObject<float>();
            }

            if (token.Type == JTokenType.String
                && double.TryParse(token.ToString().Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out var i))
            {
                return i;
            }

            return null;
        }

        public static bool? ToBoolOrNull(this JToken token)
        {
            if (token.Type == JTokenType.Boolean)
            {
                return token.ToObject<bool>();
            }

            if (token.Type == JTokenType.String
                && bool.TryParse(token.ToString(), out var b))
            {
                return b;
            }

            return null;
        }

        public static Uri? ToUriOrNull(this JToken token, Uri? baseUri = null)
        {
            if (token.Type == JTokenType.Uri)
            {
                return baseUri == null
                    ? token.ToObject<Uri>()
                    : new Uri(baseUri, token.ToObject<Uri>());
            }

            if (token.Type == JTokenType.String)
            {
                return baseUri != null
                    ? token.ToString()?.ToUriOrNull(baseUri)
                    : token.ToString()?.ToUriOrNull();
            }

            return null;
        }

        public static T? ParseOrNull<T>(string? json) where T : JToken
        {
            if (string.IsNullOrEmpty(json))
            {
                return null;
            }

            try
            {
                return JToken.Parse(json!) as T;
            }
            catch (Newtonsoft.Json.JsonException)
            {
                return null;
            }
        }

        public static JToken? ParseOrNull(string? json)
        {
            return ParseOrNull<JToken>(json);
        }

        public static async Task<TResult?> AsNewtonsoftJson<TResult>(this Task<HttpResponseMessage?> responseTask, Encoding? encoding = null, bool throwOnError = false)
            where TResult : JToken
        {
            var response = await responseTask.ConfigureAwait(false);

            return await response.AsNewtonsoftJson<TResult>(encoding, throwOnError).ConfigureAwait(false);
        }

        public static async Task<TResult?> AsNewtonsoftJson<TResult>(this HttpResponseMessage? response, Encoding? encoding = null, bool throwOnError = false)
            where TResult : JToken
        {
            if (response == null)
            {
                return null;
            }

            try
            {
                var ser = new Newtonsoft.Json.JsonSerializer();

                if (encoding == null)
                {
                    var jsonStream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
                    if (jsonStream == null)
                    {
                        return default;
                    }

                    using var reader = new StreamReader(jsonStream);
                    using var jsonReader = new JsonTextReader(reader);
                    return ser.Deserialize<TResult>(jsonReader);
                }
                else
                {
                    var jsonString = await response.AsText(encoding, throwOnError).ConfigureAwait(false);
                    if (jsonString == null)
                    {
                        return default;
                    }

                    using var reader = new StringReader(jsonString);
                    using var jsonReader = new JsonTextReader(reader);
                    return ser.Deserialize<TResult>(jsonReader);
                }
            }
            catch (JsonReaderException)
            {
                if (throwOnError)
                {
                    throw;
                }

                return null;
            }
            catch (Exception ex)
            {
                Logger.Instance.LogWarning(ex);

                if (throwOnError)
                {
                    throw;
                }

                return null;
            }
        }
    }
}
