namespace FSClient.Providers
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    public class PlayerJsAbstractSiteConfig
    {
        public PlayerJsAbstractSiteConfig(
            string nodeIdPrefix,
            string? idRegex = null,
            IEnumerable<Uri>? allowedMirrors = null,
            PlayerJsConfig? playerJsConfig = null)
        {
            NodeIdPrefix = nodeIdPrefix;
            IdRegex = idRegex;
            AllowedMirrors = allowedMirrors ?? Enumerable.Empty<Uri>();
            PlayerJsConfig = playerJsConfig;
        }

        public string NodeIdPrefix { get; }

        public string? IdRegex { get; }

        public IEnumerable<Uri> AllowedMirrors { get; }

        public PlayerJsConfig? PlayerJsConfig { get; }
    }
}
