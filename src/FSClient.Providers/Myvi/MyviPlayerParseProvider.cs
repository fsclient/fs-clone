namespace FSClient.Providers
{
    using System;
    using System.Linq;
    using System.Net.Http;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Threading.Tasks;

    using FSClient.Shared.Helpers;
    using FSClient.Shared.Helpers.Web;
    using FSClient.Shared.Models;
    using FSClient.Shared.Providers;

    using Newtonsoft.Json.Linq;

    /// <inheritdoc/>
    public class MyviPlayerParseProvider : IPlayerParseProvider
    {
        private static readonly Regex MyViRegex = new Regex(@"v=([^\\&]*)");
        private static readonly Regex MyViRegexLegacy = new Regex("dataUrl\\s?:\\s?'([^']+)");

        private readonly MyviSiteProvider siteProvider;

        public MyviPlayerParseProvider(
            MyviSiteProvider siteProvider)
        {
            this.siteProvider = siteProvider;
        }

        public Site Site => siteProvider.Site;

        public ProviderRequirements ReadRequirements => ProviderRequirements.None;

        public bool CanOpenFromLinkOrHostingName(Uri link)
        {
            var hosting = link.Host.GetLettersAndDigits().ToLower();
            var rootDomain = link.GetRootDomain();
            return hosting.Contains("myvi")
                || hosting.Contains("otakustudio")
                || hosting.Contains("ourvideo")
                || siteProvider.Mirrors.Any(m => m.GetRootDomain() == rootDomain);
        }

        public async Task<File?> ParseFromUriAsync(Uri httpUri, CancellationToken cancellationToken)
        {
            var builder = siteProvider
                .HttpClient
                .GetBuilder(httpUri);

            if (httpUri.ToString().Contains("/embed/"))
            {
                builder = builder.WithAjax()
                    .WithArgument("Referer", httpUri.ToString());
            }

            var resp = await builder
                .SendAsync(cancellationToken)
                .ConfigureAwait(false);
            var html = await resp.AsHtml(cancellationToken).ConfigureAwait(false);
            if (html == null)
            {
                return null;
            }

            var frameLink = html.QuerySelectorAll("#player-container iframe, .iframe-container iframe")
                .Select(f => f.GetAttribute("src"))
                .Select(href => Uri.TryCreate(httpUri, href, out var temp) ? temp : null)
                .FirstOrDefault(l => l != null);

            if (frameLink == null
                && html.QuerySelector("meta[property='og:video']")?.GetAttribute("content") is string ogVideoMeta)
            {
                var src = WebHelper.ParseHtml(ogVideoMeta)?.QuerySelector("iframe")?.GetAttribute("src");
                Uri.TryCreate(httpUri, src, out frameLink);
            }

            if (frameLink != null)
            {
                resp = await siteProvider.HttpClient
                    .GetBuilder(frameLink)
                    .WithHeader("Referer", frameLink.ToString())
                    .WithAjax()
                    .SendAsync(cancellationToken)
                    .ConfigureAwait(false);
                if (resp == null)
                {
                    return null;
                }

                html = await resp.AsHtml(cancellationToken).ConfigureAwait(false);
            }

            var scripts = html?
                .Scripts
                .Select(s => s.InnerHtml)
                .Where(s => !string.IsNullOrEmpty(s));

            if (scripts == null)
            {
                return null;
            }

            var host = new Uri("http://myvi.ru");

            foreach (var script in scripts)
            {
                var link = (Uri?)null;
                if (MyViRegex.Match(script) is var match
                    && match.Success && match.Groups.Count > 0)
                {
                    link = Uri.UnescapeDataString(match.Groups[1].Value).ToUriOrNull(host);
                }
                else if (MyViRegexLegacy.Match(script) is var matchLegacy
                    && matchLegacy.Success && matchLegacy.Groups.Count > 0)
                {
                    var json = await siteProvider.HttpClient
                        .GetBuilder(new Uri(host, matchLegacy.Groups[1].Value))
                        .SendAsync(cancellationToken)
                        .AsNewtonsoftJson<JObject>()
                        .ConfigureAwait(false);

                    var playlist = json?["sprutoData"]?["playlist"] as JArray;
                    link = playlist?[0]?["video"]?[0]?["url"]?.ToUriOrNull(host);
                }
                if (link == null)
                {
                    continue;
                }

                var fileHead = await siteProvider.HttpClient
                    .HeadBuilder(link)
                    .SendAsync(HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                    .ConfigureAwait(false);

                link = fileHead?.Headers?.Location ?? fileHead?.RequestMessage?.RequestUri;
                if (link == null)
                {
                    continue;
                }

                var cooks = string.Join("; ", fileHead!
                    .GetCookies()
                    .Concat(resp!.GetCookies())
                    .Select(c => c.Name + "=" + c.Value)
                    .Distinct());

                var video = new Video(link)
                {
                    CustomHeaders =
                    {
                        ["Cookie"] = cooks
                    }
                };

                var id = httpUri.Segments.LastOrDefault()?.GetLettersAndDigits();
                var file = new File(Site, "mvi" + id);
                file.FrameLink = link;
                file.SetVideos(video);

                return file;
            }
            return null;
        }
    }
}
