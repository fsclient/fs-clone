namespace FSClient.Providers
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Runtime.CompilerServices;
    using System.Threading;
    using System.Threading.Tasks;

    using AngleSharp.Dom;

    using FSClient.Shared.Helpers;
    using FSClient.Shared.Helpers.Web;
    using FSClient.Shared.Models;
    using FSClient.Shared.Providers;

    using Newtonsoft.Json.Linq;

    public class FilmixItemInfoProvider : IItemInfoProvider
    {
        private readonly CultureInfo ruCulture;
        private readonly FilmixSiteProvider siteProvider;

        public FilmixItemInfoProvider(FilmixSiteProvider siteProvider)
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
                || !link.Host.Contains("filmix"))
            {
                return false;
            }

            var id = FilmixSiteProvider.GetIdFromUrl(link);
            return !string.IsNullOrEmpty(id);
        }

        public async Task<ItemInfo?> OpenFromLinkAsync(Uri link, CancellationToken cancellationToken)
        {
            var id = FilmixSiteProvider.GetIdFromUrl(link);
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
                || item.Site != siteProvider.Site)
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
            if (cancellationToken.IsCancellationRequested)
            {
                return false;
            }

            if (item.Link?.GetPath().Contains(item.SiteId) != true)
            {
                item.Link = new Uri(domain, $"drama/{item.SiteId}-l.html");
            }
            else if (!item.Link.IsAbsoluteUri)
            {
                item.Link = new Uri(domain, item.Link);
            }
            else if (!item.Link.AbsoluteUri.StartsWith(domain.AbsoluteUri, StringComparison.Ordinal))
            {
                var uriBuild = new UriBuilder(item.Link.AbsoluteUri)
                {
                    Host = domain.Host,
                    Scheme = domain.Scheme,
                    Port = domain.Port
                };

                item.Link = uriBuild.Uri;
            }

            var html = await siteProvider
                .HttpClient
                .GetBuilder(item.Link)
                .WithTimeSpanSemaphore(siteProvider.RequestSemaphore)
                .SendAsync(cancellationToken)
                .AsHtml(cancellationToken)
                .ConfigureAwait(false);

            var info = html?.QuerySelector(".fullstory");

            if (html == null || info == null)
            {
                return false;
            }

            var ratingSpan = info.QuerySelector("[itemprop=aggregateRating]");
            if (ratingSpan != null)
            {
                item.Details.Rating = new UpDownRating(
                    int.TryParse(ratingSpan.QuerySelector(".ratePos")?.TextContent, out var positive) ? positive : 0,
                    int.TryParse(ratingSpan.QuerySelector(".rateNeg")?.TextContent, out var negative) ? negative : 0,
                    ratingSpan.QuerySelector(".positive.active") != null,
                    ratingSpan.QuerySelector(".negative.active") != null,
                    CanVote: true);
            }

            item.Poster = FilmixSiteProvider.GetImage(domain, info.QuerySelector("[itemprop=image]")?.GetAttribute("src") ?? item.Poster.ToString())
                ?? item.Poster;

            item.Title = info.QuerySelector("[itemprop=name]")?.TextContent ?? item.Title;
            item.Details.TitleOrigin = info.QuerySelector("[itemprop=alternativeHeadline]")?.TextContent ?? item.Details.TitleOrigin;

            item.Details.Description = info.QuerySelector("[itemprop=description] .full-story")?.TextContent ?? item.Details.Description;

            item.Details.Quality = FilmixSiteProvider.CleanQuality(info.QuerySelector(".quality")?.TextContent);

            item.Details.Images = html
                .QuerySelectorAll(".frames-list a")
                .Select(img => FilmixSiteProvider.GetImage(domain, img.GetAttribute("href")))
                .Where(image => image?.Count > 0)
                .OfType<WebImage>()
                .ToArray();

            item.Details.Similar = html
                .QuerySelectorAll("#careers-actor a")
                .Select(a =>
                {
                    if (!Uri.TryCreate(domain, a.GetAttribute("href"), out var l))
                    {
                        return null;
                    }

                    return new ItemInfo(Site, FilmixSiteProvider.GetIdFromUrl(l))
                    {
                        Link = l,
                        Section = item.Section,
                        Poster = FilmixSiteProvider.GetImage(domain, a.QuerySelector("img")?.GetAttribute("src")) ?? default,
                        Title = a.QuerySelector(".film-name")?.TextContent
                    };
                })
                .Where(i => i != null)
                .ToArray()!;

            var years = info.QuerySelectorAll("[itemprop=copyrightYear]")
                .Select(y => y.TextContent?.ToIntOrNull())
                .Where(y => y > 0)
                .ToArray();

            item.Details.Year = years.FirstOrDefault() ?? item.Details.Year;
            item.Details.YearEnd = years.Skip(1).FirstOrDefault() ?? item.Details.YearEnd;

            item.Details.Tags = GetTagsFromHtml(info).ToArray();

            var isClosed = info.QuerySelector(".status-2") != null;

            item.Details.Status = FilmixSiteProvider.GetStatusFromString(
                info.QuerySelector("span.added-info")?.TextContent,
                isClosed ? StatusType.Canceled : StatusType.Unknown);

            var genresContainer = item.Details.Tags.FirstOrDefault(c => c.TagType == TagType.Genre);
            if (genresContainer != null)
            {
                var genres = genresContainer.Items.Reverse().Where(c => c.Title != null).Select(c => c.Title!.ToLower());
                var mustBeCartoon = html.QuerySelector("[itemtype='http://schema.org/BreadcrumbList'] a[itemprop='item'][href*=multf]") != null;

                item.Section = FilmixSiteProvider.GetSectionFromGenresAndStatus(genres, mustBeCartoon, item.Details.Status);
            }
            if (html.QuerySelector(".series-block") != null
                && item.SiteId != null)
            {
                item.Details.EpisodesCalendar = GetEpisodesCalendar(item.SiteId, default);
            }

            return true;
        }

        private async IAsyncEnumerable<EpisodeInfo> GetEpisodesCalendar(
            string id,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var page = 0;
            while (true)
            {
                var currentPage = ++page;

                var domain = await siteProvider.GetMirrorAsync(cancellationToken).ConfigureAwait(false);

                var json = await siteProvider.HttpClient
                    .PostBuilder(new Uri(domain, "api/episodes/get"))
                    .WithTimeSpanSemaphore(siteProvider.RequestSemaphore)
                    .WithHeader("Origin", domain.GetOrigin())
                    .WithHeader("Referer", new Uri(domain, "/drama/" + id + "-l.html").ToString())
                    .WithBody(new Dictionary<string, string>
                    {
                        ["post_id"] = id,
                        ["page"] = currentPage.ToString()
                    })
                    .WithAjax()
                    .SendAsync(cancellationToken)
                    .AsNewtonsoftJson<JObject>()
                    .ConfigureAwait(false);

                var items = ((json?["message"] as JObject)?["episodes"] as JObject)?
                    .Children<JProperty>()
                    .Select(seasonNode =>
                    {
                        var season = seasonNode.Name?.ToIntOrNull();
                        return (seasonNode.Value as JArray)?
                            .Select(ep =>
                            {
                                var episode = new EpisodeInfo
                                {
                                    Title = ep["n"]?.ToString(),
                                    Episode = ep["e"]?.ToIntOrNull(),
                                    Season = season ?? 1
                                };
                                if (DateTimeOffset.TryParseExact(
                                    ep["d"]?.ToString() ?? string.Empty,
                                    "d MMMM yyyy",
                                    ruCulture,
                                    DateTimeStyles.None,
                                    out var temp))
                                {
                                    episode.DateTime = temp;
                                }

                                return episode;
                            })
                            ?? Enumerable.Empty<EpisodeInfo>();
                    })
                    .SelectMany(s => s)
                    .ToArray()
                    ?? Array.Empty<EpisodeInfo>();
                if (items.Length == 0)
                {
                    yield break;
                }
                foreach (var item in items)
                {
                    yield return item;
                }
            }
        }

        private IEnumerable<TagsContainer> GetTagsFromHtml(IElement html)
        {
            #region Duration
            var durationStr = html.QuerySelector("[itemprop='duration'] .item-content")?.TextContent;
            if (!string.IsNullOrWhiteSpace(durationStr))
            {
                yield return new TagsContainer("Время", new TitledTag(durationStr));
            }

            #endregion

            var unknownTags = html.QuerySelectorAll(".item.category");

            #region Genre            
            var genres = unknownTags
                .FirstOrDefault(t => t.FirstElementChild?.TextContent.Contains("Жанр") ?? false)?
                .QuerySelectorAll(".item-content > span")
                .Select(d => (
                    title: (d.QuerySelector("[itemprop=genre]")?.TextContent.Trim() ?? d.TextContent.Trim().Trim(','))?.ToLower(),
                    value: d.QuerySelector("a")?.GetAttribute("href")))
                .Where(t => t.title != null && t.title != "Трейлеры")
                .Select(t => FilmixSiteProvider.GetTagFromUriString(t.title!, t.value))
                .ToArray() ?? Array.Empty<TitledTag>();

            if (genres.Length > 0)
            {
                yield return new TagsContainer(TagType.Genre, genres);
            }

            #endregion
            #region County
            var contry = html.QuerySelectorAll(".item.contry a")
                .Select(c => FilmixSiteProvider.GetTagFromUriString(
                    c.TextContent,
                    c.GetAttribute("href")))
                .Where(t => t.Title != null)
                .ToArray();

            if (contry.Length > 0)
            {
                yield return new TagsContainer(TagType.County, contry);
            }

            #endregion

            #region Kinopoisk IMDB
            var ci = (CultureInfo)CultureInfo.CurrentCulture.Clone();
            ci.NumberFormat.CurrencyDecimalSeparator = ".";

            if (float.TryParse(html.QuerySelector(".kinopoisk p, .kinopoisk div")?.TextContent,
                        NumberStyles.Any, ci, out var kp)
                && kp > 0)
            {
                yield return new TagsContainer("Кинопоиск", new TitledTag(kp.ToString("F")));
            }

            if (float.TryParse(html.QuerySelector(".imdb p, .imdb div")?.TextContent,
                        NumberStyles.Any, ci, out var imdb)
                && imdb > 0)
            {
                yield return new TagsContainer("IMDb", new TitledTag(imdb.ToString("F")));
            }

            #endregion

            #region Actors
            var actors = html.QuerySelectorAll(".item.actors .item-content > span")
                .Select(d => FilmixSiteProvider.GetTagFromUriString(
                    d.QuerySelector("[itemprop=name]")?.TextContent.Trim() ?? d.TextContent.Trim().Trim(','),
                    d.QuerySelector("a")?.GetAttribute("href")))
                .Where(t => t.Title != null)
                .ToArray();

            if (actors.Length > 0)
            {
                yield return new TagsContainer(TagType.Actor, actors);
            }

            #endregion
            #region Directors
            var directors = html.QuerySelectorAll(".item.directors .item-content > span")
                .Select(d => FilmixSiteProvider.GetTagFromUriString(
                    d.QuerySelector("[itemprop=name]")?.TextContent.Trim() ?? d.TextContent.Trim().Trim(','),
                    d.QuerySelector("a")?.GetAttribute("href")))
                .Where(t => t.Title != null)
                .ToArray();

            if (directors.Length > 0)
            {
                yield return new TagsContainer(TagType.Director, directors);
            }

            #endregion
            #region Plot            
            var plot = unknownTags
                .FirstOrDefault(t => t.FirstElementChild?.TextContent.Contains("Сценарист") ?? false)?
                .QuerySelectorAll(".item-content > span")
                .Select(d => FilmixSiteProvider.GetTagFromUriString(
                    d.QuerySelector("[itemprop=name]")?.TextContent.Trim() ?? d.TextContent.Trim().Trim(','),
                    d.QuerySelector("a")?.GetAttribute("href")))
                .Where(t => t.Title != null)
                .ToArray() ?? Array.Empty<TitledTag>();

            if (plot.Length > 0)
            {
                yield return new TagsContainer(TagType.Writter, plot);
            }

            #endregion
        }
    }
}
