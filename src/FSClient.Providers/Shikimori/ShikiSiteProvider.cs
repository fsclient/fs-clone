namespace FSClient.Providers
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net.Http;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;

    using AngleSharp.Dom;
    using AngleSharp.Html.Dom;

    using FSClient.Shared.Helpers;
    using FSClient.Shared.Helpers.TimeSpanSemaphore;
    using FSClient.Shared.Helpers.Web;
    using FSClient.Shared.Models;
    using FSClient.Shared.Providers;
    using FSClient.Shared.Services;

    using Newtonsoft.Json.Linq;

    public class ShikiSiteProvider : BaseSiteProvider
    {
        // https://github.com/shikimori/shikimori/blob/master/config/initializers/rack-attack.rb
        private static readonly ITimeSpanSemaphore timeSpanSemaphore = TimeSpanSemaphore.Combine(
            TimeSpanSemaphore.Create(4, TimeSpan.FromSeconds(1)),
            TimeSpanSemaphore.Create(89, TimeSpan.FromMinutes(1)));

        public static readonly Site SiteKey
            = Sites.Shikimori;

        public ShikiSiteProvider(IProviderConfigService providerConfigService) : base(
            providerConfigService,
            new ProviderConfig(SiteKey,
                canBeMain: true,
                healthCheckRelativeLink: new Uri("/api/constants/anime", UriKind.Relative),
                mirrors: new[] { new Uri("https://shikimori.one") }))
        {
        }

        public static IEnumerable<Section> Sections { get; } = new[]
        {
            new Section("tv", "TV Сериал") { Modifier = SectionModifiers.Serial | SectionModifiers.Anime },
            new Section("movie", "Фильм") { Modifier = SectionModifiers.Film | SectionModifiers.Anime },
            new Section("ova", "OVA") { Modifier = SectionModifiers.Anime },
            new Section("ona", "ONA") { Modifier = SectionModifiers.Anime },
            new Section("special", "Спешл") { Modifier = SectionModifiers.Anime },
            new Section("music", "Клип") { Modifier = SectionModifiers.Anime }
        };

        private const string ShikiApiUserAgent = "FSClient @ FSDev";

        private static readonly string[] allowedHtmlTags = { "ul", "strong", "a", "br", "span", "del", "em", "i", "s", "u" };

        public override ITimeSpanSemaphore RequestSemaphore => timeSpanSemaphore;

        public Task<HttpResponseMessage?> GetFromApiAsync(string request, CancellationToken token)
        {
            return MakeRequestToApiAsync(HttpMethod.Get, request, new Dictionary<string, string>(), token);
        }

        public Task<HttpResponseMessage?> GetFromApiAsync(string request, Dictionary<string, string> arguments, CancellationToken token)
        {
            return MakeRequestToApiAsync(HttpMethod.Get, request, arguments, token);
        }

        public Task<HttpResponseMessage?> PostToApiAsync(string request, CancellationToken token)
        {
            return MakeRequestToApiAsync(HttpMethod.Post, request, new Dictionary<string, string>(), token);
        }

        public Task<HttpResponseMessage?> PostToApiAsync(string request, Dictionary<string, string> arguments, CancellationToken token)
        {
            return MakeRequestToApiAsync(HttpMethod.Post, request, arguments, token);
        }

        public static WebImage GetImage(JToken? imageNode, Uri domain)
        {
            if (imageNode == null)
            {
                return null;
            }
            if (domain == null)
            {
                throw new ArgumentNullException(nameof(domain));
            }
            if (imageNode is JObject jObject)
            {
                var uriBuilder = new UriBuilder(domain);
                uriBuilder.Host = "desu." + uriBuilder.Host;
                var desuDomain = uriBuilder.Uri;

                var image = new WebImage();

                image[ImageSize.Original] = jObject["original"]?.ToUriOrNull(desuDomain);
                image[ImageSize.Preview] = image[ImageSize.Original] ?? jObject["preview"]?.ToUriOrNull(desuDomain);
                image[ImageSize.Thumb] = jObject["x96"]?.ToUriOrNull(desuDomain);

                return image.Count > 0 ? image : null;
            }
            else
            {
                return imageNode.ToUriOrNull(domain);
            }
        }

        public static int? GetIdFromLink(Uri link)
        {
            return link.GetPath().Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries)
                .Skip(1).FirstOrDefault()?
                .Split('-')?.First()
                .GetDigits()
                .ToIntOrNull();
        }

        public static Uri? GetImageLink(string? image, Uri domain)
        {
            if (domain == null)
            {
                throw new ArgumentNullException(nameof(domain));
            }
            var uriBuilder = new UriBuilder(domain);
            uriBuilder.Host = "desu." + uriBuilder.Host;
            var desuDomain = uriBuilder.Uri;

            return image.ToUriOrNull(desuDomain);
        }

        // TODO: Convert to markdown in future, that we can render in UWP
        public static string ParseShikiHtml(INode? desc)
        {
            if (desc is null)
            {
                return string.Empty;
            }

            if (!desc.HasChildNodes)
            {
                return desc.TextContent;
            }

            return string
                .Concat(desc
                .ChildNodes
                .Select(node =>
                {
                    switch (node)
                    {
                        case IHtmlHtmlElement html:
                            return ParseShikiHtml(html);
                        case IHtmlBodyElement body:
                            return ParseShikiHtml(body);
                        case IHtmlHeadElement:
                            return "";
                        case IText simpleTextNode:
                            return simpleTextNode.Text;
                        case IHtmlListItemElement li:
                            return "● " + ParseShikiHtml(li);
                        case IHtmlBreakRowElement br:
                            return Environment.NewLine;
                        case IHtmlDivElement div when div.ClassName?.Contains("b-prgrph") == true || div.ClassName?.Contains("b-text_with_paragraphs") == true:
                            return ParseShikiHtml(div) + Environment.NewLine;
                        case IHtmlDivElement div when div.ClassName?.Contains("b-spoiler") == true:
                            var label = (div.QuerySelector("label") ?? div.QuerySelector("span[tabindex=0]"))?.TextContent ?? "spoiler";
                            return "[" + label + "]";
                        case IHtmlDivElement div when div.ClassName?.Contains("b-quote") == true:
                            if (div.ChildNodes.LastOrDefault(n => n.NodeType == NodeType.Text) is not IText text)
                            {
                                return Environment.NewLine;
                            }

                            if (!text.Text.StartsWith("\"", StringComparison.Ordinal) && text.Text.EndsWith("\"", StringComparison.Ordinal))
                            {
                                return "\"" + text.Text + "\"" + Environment.NewLine;
                            }

                            return text.Text + Environment.NewLine;
                        case IHtmlDivElement div when div.ClassName?.Contains("content") == true:
                            if (div.QuerySelector(".inner") is { } inner)
                            {
                                return ParseShikiHtml(inner);
                            }
                            return "";
                        case IHtmlDivElement div when div.ClassList.Contains("b-replies"):
                            return "";
                        case IHtmlLinkElement a when a.ClassName?.Contains("b-link") == true || a.ClassName?.Contains("b-mention") == true:
                            if (a.QuerySelector("[data-text]")?.GetAttribute("data-text") is string dataText)
                            {
                                return dataText;
                            }
                            var dataAttrs = JsonSerializer.Deserialize<JsonDocument>(a.QuerySelector("[data-attrs]")?.GetAttribute("data-attrs") ?? "")?
                                .RootElement;
                            if (dataAttrs?.ToPropertyOrNull("text")?.ToStringOrNull() is string textProp)
                            {
                                return textProp;
                            }
                            if (dataAttrs?.ToPropertyOrNull("russian")?.ToStringOrNull() is string russianProp)
                            {
                                return russianProp;
                            }
                            if (dataAttrs?.ToPropertyOrNull("name")?.ToStringOrNull() is string nameProp)
                            {
                                return nameProp;
                            }
                            return ParseShikiHtml(a);
                        case IHtmlImageElement image:
                            return image.AlternativeText ?? "";
                        case var _ when allowedHtmlTags.Contains(node.NodeName?.ToLowerInvariant()):
                            return ParseShikiHtml(node);
                        default:
                            return "";
                    }
                }));
        }

        protected override Uri? EnsureItemLink(Uri? itemLink, string? id, Uri mirror)
        {
            if (itemLink == null
                && id != null)
            {
                itemLink = base.EnsureItemLink(new Uri($"/animes/{id}-anime", UriKind.Relative), id, mirror);
            }
            return base.EnsureItemLink(itemLink, id, mirror);
        }

        protected override WebImage EnsureItemImage(WebImage image, Uri mirror)
        {
            image[ImageSize.Original] = ToDesuLink(image[ImageSize.Original], mirror);
            image[ImageSize.Preview] = ToDesuLink(image[ImageSize.Preview], mirror);
            image[ImageSize.Thumb] = ToDesuLink(image[ImageSize.Thumb], mirror);

            return image;

            Uri? ToDesuLink(Uri? link, Uri domain)
            {
                if (link == null)
                {
                    return null;
                }

                var uriBuilder = new UriBuilder(new Uri(mirror, link));
                uriBuilder.Host = "desu." + domain.Host;
                return uriBuilder.Uri;
            }
        }

        private async Task<HttpResponseMessage?> MakeRequestToApiAsync(HttpMethod method, string request, Dictionary<string, string> arguments, CancellationToken token = default)
        {
            var domain = await GetMirrorAsync(token).ConfigureAwait(false);

            return await HttpClient
                .RequestBuilder(method, new Uri(domain, request))
                .WithTimeSpanSemaphore(RequestSemaphore)
                .WithBody(arguments)
                .WithArguments(arguments!)
                .WithHeader("User-Agent", ShikiApiUserAgent)
                .WithAjax()
                .SendAsync(token)
                .ConfigureAwait(false);
        }
    }
}
