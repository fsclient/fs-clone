namespace FSClient.Providers
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Net.Http;
    using System.Text;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Threading.Tasks;

    using FSClient.Shared;
    using FSClient.Shared.Helpers;
    using FSClient.Shared.Helpers.Web;
    using FSClient.Shared.Services;

    using Microsoft.Extensions.Logging;

    using Nito.AsyncEx;

    public sealed class PlayerJsParserService : IDisposable
    {
        private const string PlayerJsKeysCacheKey = nameof(PlayerJsKeysCacheKey);
        private const string PlayerJsKeysCacheSeparator = "+FS+";

        private readonly ConcurrentDictionary<string, (SemaphoreSlim semaphore, int attemptPerInstance)> perHostSemaphore;
        private readonly HttpClient httpClient;
        private readonly ISettingService settingService;
        private readonly ILogger logger;

        public PlayerJsParserService(
            ISettingService settingService,
            ILogger logger)
        {
            this.settingService = settingService;
            this.logger = logger;

            httpClient = new HttpClient(new HttpClientHandler
            {
                AllowAutoRedirect = false,
                UseCookies = false
            });
            perHostSemaphore = new ConcurrentDictionary<string, (SemaphoreSlim, int attemptPerInstance)>();
        }

        public void Dispose()
        {
            httpClient.Dispose();
            foreach (var (semaphore, _) in perHostSemaphore.Values)
            {
                semaphore.Dispose();
            }
        }

        public ValueTask<string?> DecodeAsync(
            string? input,
            PlayerJsConfig config,
            CancellationToken cancellationToken)
        {
            if (Decode(input, config) is string decoded)
            {
                return new ValueTask<string?>(decoded);
            }
            return DecodeSlowAsync(input, config, cancellationToken);

            ValueTask<string?> DecodeSlowAsync(string? input, PlayerJsConfig config, CancellationToken cancellationToken)
            {
                if (input == null
                    || config.PlayerJsFileLink == null)
                {
                    return new ValueTask<string?>(input);
                }

                var host = config.PlayerJsFileLink.GetRootDomain().SplitLazy(2, '.').First();
                var cache = (settingService?.GetSetting(Settings.StateSettingsContainer, $"{PlayerJsKeysCacheKey}_{host}", "//", SettingStrategy.Local) ?? "//")
                    .Split(new[] { PlayerJsKeysCacheSeparator }, StringSplitOptions.RemoveEmptyEntries);
                var keysSeparator = cache.FirstOrDefault();
                var newKeys = cache.Skip(1).ToArray();

                if (newKeys.Length > 0)
                {
                    var decoded = Decode(input, new PlayerJsConfig(keys: newKeys, separator: keysSeparator));
                    if (decoded != null)
                    {
                        return new ValueTask<string?>(decoded);
                    }
                }

                return DecodeInternalAsync();

                async ValueTask<string?> DecodeInternalAsync()
                {
                    var (semaphore, attempts) = perHostSemaphore.GetOrAdd(host, _ => (new SemaphoreSlim(1), 0));

                    using (await semaphore.LockAsync(cancellationToken))
                    {
                        attempts++;
                        perHostSemaphore.AddOrUpdate(host, (semaphore, attempts), (_, t) => (semaphore, attempts + t.attemptPerInstance));

                        var decoded = Decode(input, new PlayerJsConfig(keys: newKeys, separator: keysSeparator));
                        if (decoded == null)
                        {
                            (newKeys, keysSeparator) = await FetchKeysAsync(config.PlayerJsFileLink, cancellationToken).ConfigureAwait(false);
                            if (settingService != null)
                            {
                                var toCache = string.Join(PlayerJsKeysCacheSeparator, new[] { keysSeparator }.Concat(newKeys));
                                settingService.SetSetting(Settings.StateSettingsContainer, $"{PlayerJsKeysCacheKey}_{host}", toCache, SettingStrategy.Local);
                            }
                        }
                    }
                    return Decode(input, new PlayerJsConfig(keys: newKeys, separator: keysSeparator));
                }
            }
        }

        private static string? Decode(
            string? input,
            PlayerJsConfig config)
        {
            const string abc = "ABCDEFGHIJKLMabcdefghijklmNOPQRSTUVWXYZnopqrstuvwxyz";
            const string keyStr = abc + "0123456789+/=";

            try
            {
                if (input == null
                    || input.Length < 2
                    || input[0] != '#')
                {
                    return input;
                }

                if (input[1] == '3')
                {
                    return input;
                }
                else if (input[1] == '1')
                {
                    if (config.OyKey == null)
                    {
                        return null;
                    }
                    return SaltD(Pepper(input.Substring(2), -1, config.OyKey));
                }
                else if (input[1] == '0')
                {
                    try
                    {
                        if (input.IndexOf('.') == -1)
                        {
                            var s2 = new StringBuilder(input.Length);
                            for (var i = 1; i < input.Length; i += 3)
                            {
                                s2.Append((char)int.Parse(input.Substring(i, 3), NumberStyles.HexNumber));
                            }

                            return s2.ToString();
                        }
                    }
                    catch
                    {
                        return SaltD(input.Substring(2));
                    }
                    return null;
                }
                else
                {
                    if (config.Keys == null || config.Separator == null)
                    {
                        return null;
                    }

                    input = input.Substring(2);
                    foreach (var current in config.Keys)
                    {
                        input = input.Replace(config.Separator + current, "");
                    }
                    foreach (var current in config.Trash)
                    {
                        input = input.Replace(config.Separator + B1(current), "");
                    }

                    var data = Convert.FromBase64String(input);
                    return Encoding.UTF8.GetString(data);
                }
            }
            catch (Exception ex)
            {
                Logger.Instance?.LogWarning(ex);
                return null;
            }

            static string SaltD(string input)
            {
                var builder = new StringBuilder(input.Length);
                var index = 0;
                input = Regex.Replace(input, @"/[^A-Za-z0-9\+\/\=]/g", "");
                while (index < input.Length)
                {
                    var char1 = keyStr.IndexOf(JsCharAt(input, index++), StringComparison.Ordinal);
                    var char2 = keyStr.IndexOf(JsCharAt(input, index++), StringComparison.Ordinal);
                    var char3 = keyStr.IndexOf(JsCharAt(input, index++), StringComparison.Ordinal);
                    var char4 = keyStr.IndexOf(JsCharAt(input, index++), StringComparison.Ordinal);
                    var char12 = (char1 << 2) | (char2 >> 4);
                    var char23 = ((char2 & 15) << 4) | (char3 >> 2);
                    var char34 = ((char3 & 3) << 6) | char4;
                    builder.Append((char)(char12));
                    if (char3 != 64)
                    {
                        builder.Append((char)(char23));
                    }
                    if (char4 != 64)
                    {
                        builder.Append((char)(char34));
                    }
                }
                return SaltUD(builder.ToString());
            }

            static string SaltUD(string input)
            {
                var builder = new StringBuilder(input.Length);
                var index = 0;
                var current = 0;
                while (index < input.Length)
                {
                    current = JsCharCodeAt(input, index);
                    if (current < 128)
                    {
                        if (current < 0)
                        {
                            builder.Append(' ');
                        }
                        else
                        {
                            builder.Append((char)(current));
                        }
                        index++;
                    }
                    else if (current > 191 && current < 224)
                    {
                        var c2 = JsCharCodeAt(input, index + 1);
                        if (c2 < 0)
                        {
                            builder.Append((char)((current & 31) << 6));
                        }
                        else
                        {
                            builder.Append((char)(((current & 31) << 6) | (c2 & 63)));
                        }
                        index += 2;
                    }
                    else
                    {
                        var c2 = JsCharCodeAt(input, index + 1);
                        var c3 = JsCharCodeAt(input, index + 2);
                        var temp = (current & 15) << 12;
                        if (c2 >= 0)
                        {
                            temp |= (c2 & 63) << 6;
                        }
                        if (c3 >= 0)
                        {
                            temp |= c3 & 63;
                        }
                        builder.Append((char)(temp));
                        index += 3;
                    }
                }
                return builder.ToString();
            }

            static string Pepper(string s, int n, string oy)
            {
                s = Regex.Replace(s, @"/\+/g", "#");
                s = Regex.Replace(s, @"/#/g", "+");
                var a = Sugar(oy) * n;
                if (n < 0)
                {
                    a += abc.Length / 2;
                }
                var r = abc.Substring(a * 2) + abc.Substring(0, a * 2);

                var builder = new StringBuilder(s.Length);
                foreach (var c in s)
                {
                    if ((c >= 'A' && c <= 'Z')
                        || (c >= 'a' && c <= 'z'))
                    {
                        var temp = JsCharAt(r, abc.IndexOf(c));
                        builder.Append(temp);
                    }
                    else
                    {
                        builder.Append(c);
                    }
                }
                return builder.ToString();
            }

            static int Sugar(string input)
            {
                var x = input.Split((char)61);
                var result = new StringBuilder(10);
                var c1 = (char)120;
                foreach (var i in x)
                {
                    var encoded = new StringBuilder(10);
                    foreach (var j in i)
                    {
                        encoded.Append((j == c1) ? (char)49 : (char)48);
                    }
                    if (encoded.Length != 0)
                    {
                        var chr = Convert.ToInt32(encoded.ToString(), 2);
                        result.Append((char)chr);
                    }
                }
                return int.Parse(result.ToString());
            }

            static string JsCharAt(string input, int index)
            {
                return index < 0 || index >= input.Length ? string.Empty : new string(input[index], 1);
            }

            static int JsCharCodeAt(string input, int index)
            {
                return index < 0 || index >= input.Length ? -1 : (int)input[index];
            }
        }

        private async Task<(string[] keys, string? keysSeparator)> FetchKeysAsync(Uri playerJsFileLink, CancellationToken cancellationToken)
        {
            var script = await httpClient
                .GetBuilder(playerJsFileLink)
                .SendAsync(cancellationToken)
                .AsText()
                .ConfigureAwait(false);
            if (script == null)
            {
                return (Array.Empty<string>(), null);
            }

            return await Task.Run(() => ParseFromScript(script)).ConfigureAwait(false);
        }

        private (string[] keys, string? keysSeparator) ParseFromScript(string script)
        {
            try
            {
                script = script
                    .Split(new[] { "\"undefined\"!=typeof window" }, StringSplitOptions.RemoveEmptyEntries)
                    .First();

                var mitmScript = GetType().ReadResourceFromTypeAssembly("PlayerJsMitmScript.inline.js");

                var engine = new Jint.Engine()
                    .SetValue("userAgent", WebHelper.DefaultUserAgent)
                    .Execute(mitmScript)
                    .Execute(script.Split(new[] { "\"undefined\" != typeof window" }, StringSplitOptions.RemoveEmptyEntries).First())
                    .Execute("var playerjsStr = evals[0]");

                var playerjsStr = engine
                    .GetValue("playerjsStr")
                    .ToString();

                var startIndex = playerjsStr.IndexOf("Playerjs(options)", StringComparison.Ordinal);
                engine.Execute(playerjsStr[(startIndex + "Playerjs(options)".Length)..]);
                var oy = engine.Execute("o.y").GetCompletionValue()?.ToString();
                var config = Decode(engine.Execute("o.u").GetCompletionValue()?.ToString(), new PlayerJsConfig(oyKey: oy));
                if (config == null)
                {
                    return (Array.Empty<string>(), null);
                }

                var items = new List<string>();
                for (var i = 10; i > -1; i--)
                {
                    var value = GetJsonValueFromString(config, "bk" + i)?.NotEmptyOrNull();
                    if (value != null)
                    {
                        items.Add(B1(value));
                    }
                }

                var keysSeparator = GetJsonValueFromString(config, "file3_separator");

                return (items.ToArray(), keysSeparator);
            }
            catch (Exception ex)
            {
                logger.LogError(ex);
                return (Array.Empty<string>(), null);
            }

            static string? GetJsonValueFromString(string source, string name)
            {
                var startIndex = source.LastIndexOf(name, StringComparison.Ordinal);
                if (startIndex >= 0)
                {
                    startIndex += name.Length + "\":\"".Length;
                    var endIndex = source.IndexOf('"', startIndex);
                    if (endIndex >= 0)
                    {
                        return source[startIndex..endIndex];
                    }
                }
                return null;
            }
        }

        private static string B1(string input)
        {
            input = Regex.Replace(input, "%([0-9A-F]{2})",
                new MatchEvaluator(match => new string((char)Convert.ToInt32("0x" + match.Value, 16), 1)));

            var bytes = Encoding.GetEncoding(28591).GetBytes(input);
            return Convert.ToBase64String(bytes);
        }
    }
}
