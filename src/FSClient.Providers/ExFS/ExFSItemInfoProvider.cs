namespace FSClient.Providers
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    using AngleSharp.Html.Dom;

    using FSClient.Localization.Resources;
    using FSClient.Shared.Helpers;
    using FSClient.Shared.Helpers.Web;
    using FSClient.Shared.Models;
    using FSClient.Shared.Providers;

    public class ExFSItemInfoProvider : IItemInfoProvider
    {
        private readonly CultureInfo ruCulture;
        private readonly ExFSSiteProvider siteProvider;

        public ExFSItemInfoProvider(ExFSSiteProvider siteProvider)
        {
            this.siteProvider = siteProvider;

            ruCulture = new CultureInfo("ru-RU");
        }

        public Site Site => siteProvider.Site;

        public bool CanPreload => true;

        public ProviderRequirements ReadRequirements => ProviderRequirements.None;

        public bool CanOpenFromLink(Uri link)
        {
            if (link == null
                || siteProvider.Mirrors.All(m => link.Host != m.Host))
            {
                return false;
            }

            var id = ExFSSiteProvider.GetIdFromUrl(link);
            return !string.IsNullOrEmpty(id);
        }

        public async Task<ItemInfo?> OpenFromLinkAsync(Uri link, CancellationToken cancellationToken)
        {
            var id = ExFSSiteProvider.GetIdFromUrl(link);
            if (string.IsNullOrEmpty(id))
            {
                return null;
            }

            var item = new ItemInfo(Site, id)
            {
                Link = link
            };

            var result = await PreloadItemAsync(item, PreloadItemStrategy.Full, cancellationToken).ConfigureAwait(false);
            return result ? item : null;
        }

        public async Task<bool> PreloadItemAsync(ItemInfo item, PreloadItemStrategy preloadItemStrategy, CancellationToken cancellationToken)
        {
            if (item == null
                || item.Site != Site)
            {
                return false;
            }

            if (preloadItemStrategy == PreloadItemStrategy.Poster
                && item.Details.Quality != null
                && item.Details.Rating != null)
            {
                return true;
            }

            var domain = await siteProvider.GetMirrorAsync(cancellationToken).ConfigureAwait(false);

            if (item.Link == null
                || !item.Link.IsAbsoluteUri
                || !item.Link.GetPath().Contains(item.SiteId!))
            {
                item.Link = item.Section != Section.Any
                    ? new Uri(domain, $"/{item.Section.Value}/{item.SiteId}-link.html")
                    : new Uri(domain, $"/index.php?newsid={item.SiteId}");
            }

            var response = await siteProvider
                .HttpClient
                .GetBuilder(item.Link)
                .WithTimeSpanSemaphore(siteProvider.RequestSemaphore)
                .SendAsync(cancellationToken)
                .ConfigureAwait(false);
            var html = response != null ? await response.AsHtml(cancellationToken).ConfigureAwait(false) : null;
            if (response == null || html == null)
            {
                return false;
            }

            item.Link = response.RequestMessage!.RequestUri;

            if (!item.Poster.ContainsKey(ImageSize.Original))
            {
                item.Poster = ExFSSiteProvider.GetPoster(domain, html.QuerySelector(".FullstoryFormLeft img")?.GetAttribute("src"))
                              ?? item.Poster;
            }

            var s = html.QuerySelector(".rating-mg .progress");
            if (s != null)
            {
                _ = int.TryParse(s.GetAttribute("data-id"), out var vl);
                _ = int.TryParse(s.GetAttribute("data-nm"), out var su);
                item.Details.Rating = new UpDownRating((su + vl) / 2, (su - vl) / 2, false, false, CanVote: true);
            }

            item.Details.Titles = html.QuerySelector(".view-caption")?
                .TextContent
                .NotEmptyOrNull()?
                .Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(p => p.Trim())
                .ToArray()
                ?? html.Title?
                    .Split(new[] { "смотреть онлайн" }, StringSplitOptions.None)
                    .Select(p => p.Trim())
                    .Take(1)
                    .ToArray()
                ?? item.Details.Titles;
            item.Details.TitleOrigin = html.QuerySelector(".view-caption2")?.TextContent ?? item.Details.TitleOrigin;
            item.Details.Description = html.QuerySelector(".FullstorySubFormText")?.TextContent ?? item.Details.Description;

            item.Section = ExFSSiteProvider.GetSectionFromUrl(item.Link) is var section && section != Section.Any
                ? section
                : item.Section;

            var links = html
                .QuerySelectorAll(".FullstoryInfo a")
                .Select(el => ExFSSiteProvider.GetTagFromLinkString(el.TextContent, el.GetAttribute("href")))
                .Where(l => l != TitledTag.Any)
                .ToArray();

            if (int.TryParse(links.FirstOrDefault(l => l.Type == "year").Title, out var year))
            {
                item.Details.Year = year;
            }

            item.Details.Similar = html
                .QuerySelectorAll(".MiniPostAllFormRelated")
                .Select(el =>
                {
                    var link = el.QuerySelector("a")?
                        .GetAttribute("href")?
                        .ToUriOrNull(domain);
                    var itemId = link?
                        .Segments
                        .LastOrDefault()?
                        .Split('-')
                        .FirstOrDefault();

                    return new ItemInfo(Site, itemId)
                    {
                        Link = link,
                        Title = el.TextContent?.Trim(),
                        Poster = ExFSSiteProvider.GetPoster(domain, el.QuerySelector("img")?.GetAttribute("src")) ?? default
                    };
                })
                .Where(i => i?.SiteId != null)
                .ToArray();

            item.Details.Images = html
                .QuerySelectorAll(".FullstoryKadrFormImg a")
                .Select(el => el.GetAttribute("href")?.ToUriOrNull(domain))
                .Where(link => link != null)
                .Select(link => (WebImage)link)
                .ToArray();

            item.Details.Status = ParseStatusFromPage(html) ?? item.Details.Status;
            item.Details.Tags = GetTagsFromHtml(html);

            item.Details.Quality = item.Details.Tags.FirstOrDefault(t => t.Title == Strings.TagType_Quality)?
                .Items.FirstOrDefault().Title;

            item.Details.EpisodesCalendar = ParseEpisodesCalendar(html);

            if (!item.Details.LinkedIds.ContainsKey(Sites.Kinopoisk))
            {
                var trailersValue = html
                    .QuerySelector(".FullstoryFormRight object param[name='flashvars']")?
                    .GetAttribute("value");

                var trailerStr = trailersValue?[(trailersValue.LastIndexOf('=') + 1)..];

                if (Uri.TryCreate(trailerStr, UriKind.Absolute, out var trailer))
                {
                    var kpId = trailer.Segments.Skip(1).FirstOrDefault()?.Trim('/');
                    if (!string.IsNullOrEmpty(kpId))
                    {
                        item.Details.LinkedIds[Sites.Kinopoisk] = kpId!;
                    }
                }
                else
                {
                    var kpId = html.QuerySelector("div[data-kinopoisk]")?.GetAttribute("data-kinopoisk")?.ToIntOrNull();
                    if (kpId?.ToString() is string kpIdStr)
                    {
                        item.Details.LinkedIds[Sites.Kinopoisk] = kpIdStr;
                    }
                }
            }

            return true;
        }

        private IAsyncEnumerable<EpisodeInfo>? ParseEpisodesCalendar(IHtmlDocument document)
        {
            var previousSeason = 1;
            var result = document.QuerySelectorAll("[id*=dateblock] .epscape_tr")
               .Select(tr =>
               {
                   var tds = tr.QuerySelectorAll("td");
                   var firstTd = tds.FirstOrDefault()?.TextContent?.Trim();
                   var firstTdParts = firstTd?.Split(' ').Select(p => p.GetDigits().ToIntOrNull()).Where(p => p.HasValue).ToArray();

                   var episode = new EpisodeInfo
                   {
                       Title = tds.Skip(1).FirstOrDefault()?.TextContent?.Trim(),
                       IsSpecial = firstTd == "Special",
                       Episode = firstTdParts?.LastOrDefault() ?? 0,
                       Season = previousSeason = firstTdParts?.Reverse().Skip(1).FirstOrDefault() ?? previousSeason
                   };

                   var date = tds.Skip(2).FirstOrDefault()?.TextContent?.Split(',').First().Trim();
                   if (DateTimeOffset.TryParseExact(date ?? "", "d MMM yyyy", ruCulture, DateTimeStyles.None, out var temp))
                   {
                       episode.DateTime = temp;
                   }

                   return episode;
               })
               .ToArray();

            if (result.Length == 0)
            {
                return null;
            }

            return result.ToAsyncEnumerable();
        }

        private TagsContainer[] GetTagsFromHtml(IHtmlDocument html)
        {
            var tags = new List<TagsContainer>();

            var leftItems = html
                .QuerySelectorAll(".FullstoryInfo .FullstoryInfoTitle")
                .Select(tag =>
                {
                    var title = tag
                        .TextContent?
                        .Trim()
                        .Replace(":", "");
                    var value = tag.NextElementSibling;
                    if (value == null || title == null)
                    {
                        return null;
                    }

                    var items = value
                        .QuerySelectorAll("a")
                        .Select(item => ExFSSiteProvider.GetTagFromLinkString(item.GetAttribute("title") ?? item.TextContent, item.GetAttribute("href")))
                        .ToArray();

                    if (items.Length == 0)
                    {
                        items = new[] { new TitledTag(value.TextContent?.Trim()) };
                    }

                    return title.IndexOf("жанр", StringComparison.OrdinalIgnoreCase) >= 0
                        ? new TagsContainer(TagType.Genre, items.ToArray())
                        : new TagsContainer(title, items.ToArray());
                })
                .Where(l => l != null
                    && l.Title != "Год"
                    && l.Items.Any());

            tags.AddRange(leftItems!);

            var ci = (CultureInfo)CultureInfo.CurrentCulture.Clone();
            ci.NumberFormat.CurrencyDecimalSeparator = ".";
            if (float.TryParse(
                    html.QuerySelector(".in_name_kp")?.TextContent,
                    NumberStyles.Any, ci,
                    out var rating)
                && rating > 0)
            {
                tags.Add(new TagsContainer("Кинопоиск", new TitledTag(rating.ToString("F"))));
            }

            if (float.TryParse(
                        html.QuerySelector(".in_name_imdb")?.TextContent,
                        NumberStyles.Any, ci,
                        out rating)
                    && rating > 0)
            {
                tags.Add(new TagsContainer("IMDb", new TitledTag(rating.ToString("F"))));
            }

            var actors = html
                .QuerySelectorAll("a.MiniPostNameActors")
                .Select(el => ExFSSiteProvider.GetTagFromLinkString(el.TextContent?.Trim(), el.GetAttribute("href")))
                .Where(l => l != TitledTag.Any)
                .ToArray();
            if (actors.Length > 0)
            {
                tags.Add(new TagsContainer(TagType.Actor, actors));
            }

            var rightItems = html
                .QuerySelectorAll(".TabDopInfoBlockOne")
                .Select(tag =>
                {
                    var title = tag
                        .QuerySelector(".TabDopInfoBlockOneTitle")?
                        .TextContent?
                        .Trim()
                        .Replace(":", "");

                    var items = tag
                        .QuerySelectorAll("a")
                        .Select(item => ExFSSiteProvider.GetTagFromLinkString(item.TextContent?.Trim(), item.GetAttribute("href")))
                        .ToArray();

                    if (items.Length == 0)
                    {
                        items = new[] { new TitledTag(tag.LastChild?.TextContent?.Trim()) };
                    }

                    return title == "Режиссер" ? new TagsContainer(TagType.Director, items.ToArray())
                        : title == "Сценарист" ? new TagsContainer(TagType.Writter, items.ToArray())
                        : title == "Композитор" ? new TagsContainer(TagType.Composer, items.ToArray())
                        : title != null ? new TagsContainer(title, items)
                        : null;
                })
                .Where(l => l?.Items.Any() == true);

            tags.AddRange(rightItems!);

            return tags.ToArray();
        }

        private static Status? ParseStatusFromPage(IHtmlDocument html)
        {
            // 2 сезон 43 серия
            var episodes = html
                .QuerySelectorAll(".tab-content div[id^=dateblock] .epscape_tr")
                .Select(tr =>
                {
                    var parts = tr
                        .QuerySelector("td")?
                        .TextContent?
                        .Split(' ')
                        .Select(part => part.ToIntOrNull())
                        .Where(part => part.HasValue)
                        ?? Enumerable.Empty<int?>();

                    var released = tr.QuerySelector(".released") != null;
                    return (released, episode: parts.LastOrDefault(), season: parts.Reverse().Skip(1).FirstOrDefault());
                });
            var lastReleasedEpisode = episodes.FirstOrDefault(e => e.released);

            var statusTypeStr = html
                .QuerySelectorAll("#tab4 li")
                .FirstOrDefault(e => e.TextContent.StartsWith("Статус", StringComparison.Ordinal))?
                .TextContent;

            if (statusTypeStr == null)
            {
                return null;
            }

            var type = statusTypeStr.Contains("продолжается") ? StatusType.Ongoing
                    : statusTypeStr.Contains("отменен") ? StatusType.Canceled
                    : statusTypeStr.Contains("закончен") ? StatusType.Released
                    : statusTypeStr.Contains("паузе") ? StatusType.Paused
                    : StatusType.Unknown;
            return new Status(
                currentSeason: lastReleasedEpisode.season,
                currentEpisode: lastReleasedEpisode.episode,
                type: type);
        }
    }
}
