namespace FSClient.Providers
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    public class PlayerJsConfig
    {
        public PlayerJsConfig(
            Uri? playerJsFileLink = null,
            IEnumerable<string>? keys = null,
            IEnumerable<string>? trash = null,
            string? separator = "//",
            string? oyKey = null)
        {
            PlayerJsFileLink = playerJsFileLink;
            Keys = keys ?? Enumerable.Empty<string>();
            Trash = trash ?? Enumerable.Empty<string>();
            Separator = separator ?? "//";
            OyKey = oyKey;
        }

        public Uri? PlayerJsFileLink { get; }

        public IEnumerable<string> Keys { get; }

        public IEnumerable<string> Trash { get; }

        public string Separator { get; }

        public string? OyKey { get; }
    }
}
