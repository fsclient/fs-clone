namespace FSClient.Providers
{
    using System;
    using System.Collections.Generic;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Threading.Tasks;

    using FSClient.Shared;
    using FSClient.Shared.Helpers.Web;
    using FSClient.Shared.Models;
    using FSClient.Shared.Providers;
    using FSClient.Shared.Services;

    public class HDVBSiteProvider : BaseSiteProvider
    {
        internal const string HDVBRefererKey = "HDVBReferer";
        internal const string HDVBActualizerScriptKey = "HDVBActualizerScript";

        public HDVBSiteProvider(IProviderConfigService providerConfigService) : base(
            providerConfigService,
            new ProviderConfig(Sites.HDVB,
                enforceDisabled: string.IsNullOrEmpty(Secrets.HDVBApiKey),
                properties: new Dictionary<string, string?>
                {
                    [HDVBRefererKey] = null, // set 'null' to use Host as Referer
                    [HDVBActualizerScriptKey] = "https://weblion777.github.io/hdvb.js"
                },
                mirrors: new[] { new Uri("https://vid1610156835256.vb17120ayeshajenkins.pw") }))
        {
        }

        protected override MirrorGetterConfig PrepareMirrorGetterConfig()
        {
            var config = base.PrepareMirrorGetterConfig();
            if (Properties.TryGetValue(HDVBActualizerScriptKey, out var actualizerScriptStr)
                && Uri.TryCreate(actualizerScriptStr, UriKind.Absolute, out var actualizerScript))
            {
                config = config with
                {
                    MirrorFinder = (_, ct) => MirrorFinder(actualizerScript, ct)
                };
            }
            return config;
        }

        private async ValueTask<Uri?> MirrorFinder(Uri actualizerScript, CancellationToken cancellationToken)
        {
            var scriptText = await HttpClient
                .GetBuilder(actualizerScript)
                .SendAsync(cancellationToken)
                .AsText()
                .ConfigureAwait(false);
            if (string.IsNullOrEmpty(scriptText))
            {
                return null;
            }
            var groups = Regex.Match(scriptText, @"\""(?<beforeDate>http.*?)"".*?""(?<afterDate>.*?)""").Groups;
            if (Uri.TryCreate($"{groups["beforeDate"]}{DateTime.UtcNow.Ticks}{groups["afterDate"]}", UriKind.Absolute, out var result))
            {
                return result;
            }
            return null;
        }
    }
}
