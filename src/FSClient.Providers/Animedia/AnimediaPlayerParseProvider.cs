namespace FSClient.Providers
{
    using System;
    using System.Linq;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Threading.Tasks;

    using FSClient.Shared.Helpers;
    using FSClient.Shared.Helpers.Web;
    using FSClient.Shared.Models;
    using FSClient.Shared.Providers;

    using Newtonsoft.Json.Linq;

    public class AnimediaPlayerParseProvider : IPlayerParseProvider
    {
        private readonly AnimediaSiteProvider siteProvider;

        public AnimediaPlayerParseProvider(
            AnimediaSiteProvider siteProvider)
        {
            this.siteProvider = siteProvider;
        }

        public Site Site => siteProvider.Site;

        public ProviderRequirements ReadRequirements => ProviderRequirements.None;

        public bool CanOpenFromLinkOrHostingName(Uri link)
        {
            var rootDomain = link.GetRootDomain();
            return siteProvider.Mirrors.Any(m => m.GetRootDomain() == rootDomain);
        }

        public async Task<File?> ParseFromUriAsync(Uri httpUri, CancellationToken cancellationToken)
        {
            var pageText = await siteProvider.HttpClient
                .GetBuilder(httpUri)
                .SendAsync(cancellationToken)
                .AsText()
                .ConfigureAwait(false);

            if (string.IsNullOrWhiteSpace(pageText))
            {
                return null;
            }

            var regex = Regex.Match(pageText, @"file\s*:\s*""(?<link>.+?)""(?:\s*,\s*plstart\s*:\s*""(?<epId>.+?)"")?");
            var link = regex
                .Groups["link"].Value?
                .ToUriOrNull(httpUri);

            var fileText = link == null ? null : await siteProvider.HttpClient
                .GetBuilder(link)
                .WithHeader("Origin", httpUri.GetOrigin())
                .WithHeader("Referer", httpUri.ToString())
                .SendAsync(cancellationToken)
                .AsText()
                .ConfigureAwait(false);

            if (string.IsNullOrWhiteSpace(fileText)
                || fileText!.Contains("Not found"))
            {
                if (httpUri.OriginalString.Contains("/embed/"))
                {
                    var nonEmbedLink = Regex.Match(pageText, @"link:\s*'(?<link>.+?)'")
                        .Groups["link"]
                        .Value?
                        .ToUriOrNull(httpUri);
                    if (nonEmbedLink != null)
                    {
                        return await ParseFromUriAsync(nonEmbedLink, cancellationToken).ConfigureAwait(false);
                    }
                }
                return null;
            }

            if (JsonHelper.ParseOrNull(fileText) is JArray jArray
                && regex.Groups["epId"].Value.NotEmptyOrNull() is string epId)
            {
                link = jArray.OfType<JObject>()
                    .FirstOrDefault(o => o["id"]?.ToString() == epId)?
                    ["file"]?
                    .ToUriOrNull(httpUri);
                if (link == null)
                {
                    return null;
                }

                fileText = await siteProvider.HttpClient
                    .GetBuilder(link)
                    .WithHeader("Origin", httpUri.GetOrigin())
                    .SendAsync(cancellationToken)
                    .AsText()
                    .ConfigureAwait(false);
                if (fileText == null)
                {
                    return null;
                }
            }

            var id = string.Join("_", httpUri.Segments.Reverse().Take(3).Reverse().Select(p => p.Trim('/')));
            var videos = ProviderHelper
                .ParseVideosFromM3U8(fileText.Split('\n'), link)
                .Select(v =>
                {
                    v.CustomHeaders.Add("Origin", httpUri.Host);
                    v.CustomHeaders.Add("Referer", httpUri.ToString());
                    return v;
                })
                .ToArray();
            var file = new File(Site, "anmd" + id);
            file.FrameLink = link;
            file.SetVideos(videos);
            return file;
        }
    }
}
