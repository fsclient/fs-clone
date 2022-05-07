namespace FSClient.Providers
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    using AngleSharp.Dom;

    using FSClient.Shared.Helpers;
    using FSClient.Shared.Helpers.Web;
    using FSClient.Shared.Models;
    using FSClient.Shared.Providers;

    public class RezkaItemInfoProvider : IItemInfoProvider
    {
        private readonly RezkaSiteProvider siteProvider;
        private readonly CultureInfo ruCultureInfo;

        public RezkaItemInfoProvider(RezkaSiteProvider rezkaSiteProvider)
        {
            siteProvider = rezkaSiteProvider;
            ruCultureInfo = new CultureInfo("ru-RU");
        }

        public Site Site => siteProvider.Site;

        public bool CanPreload => true;

        public ProviderRequirements ReadRequirements => ProviderRequirements.None;

        public bool CanOpenFromLink(Uri link)
        {
            return link != null && link.Host.Contains("rezka");
        }

        public async Task<ItemInfo?> OpenFromLinkAsync(Uri link, CancellationToken cancellationToken)
        {
            var id = RezkaSiteProvider.GetIdFromUrl(link);
            if (!id.HasValue)
            {
                return null;
            }

            var item = new ItemInfo(Site, id.ToString())
            {
                Link = link
            };

            var result = await PreloadItemAsync(item, PreloadItemStrategy.Full, cancellationToken).ConfigureAwait(false);
            return result ? item : null;
        }

        public async Task<bool> PreloadItemAsync(ItemInfo item, PreloadItemStrategy preloadItemStrategy, CancellationToken cancellationToken)
        {
            if (item.Link == null)
            {
                item.Link = new Uri($"/film/drama/{item.SiteId}-title.html", UriKind.Relative);
            }

            if (preloadItemStrategy == PreloadItemStrategy.Poster
                && item.Details.Quality != null
                && item.Details.Rating != null)
            {
                return true;
            }

            var domain = await siteProvider.GetMirrorAsync(cancellationToken).ConfigureAwait(false);
            var link = new Uri(domain, item.Link);
            var html = (await siteProvider
                .HttpClient
                .GetBuilder(link)
                .SendAsync(cancellationToken)
                .AsHtml(cancellationToken)
                .ConfigureAwait(false))?
                .QuerySelector(".b-content__main");
            if (html == null)
            {
                return false;
            }
            var episodes = GetEpisodesCalendar(html);

            item.Poster = html.QuerySelector(".b-sidecover img[src]")?.GetAttribute("src")?.ToUriOrNull(domain);
            item.Details.EpisodesCalendar = episodes.Length == 0 ? null : episodes.ToAsyncEnumerable();
            item.Title = html.QuerySelector("[itemprop=name]")?.TextContent?.Trim();
            item.Details.TitleOrigin = html.QuerySelector("[itemprop=alternativeHeadline]")?.TextContent?.Trim();
            item.Details.Description = html.QuerySelector(".b-post__description_text")?.TextContent?.Trim();
            item.Details.Quality = html.QuerySelector(".b-post__info td h2:contains('В качестве')")?
                .ParentElement?.NextElementSibling?.TextContent;
            item.Details.Year = html.QuerySelector(".b-post__info td a[href*='/year/']")?.TextContent?.GetDigits().ToIntOrNull();
            item.Details.Tags = GetTagsContainers(html.QuerySelector(".b-post__info")).ToArray();

            item.Section = Section.CreateDefault(SectionModifiers.None
                | (episodes.Length > 0 ? SectionModifiers.Serial : SectionModifiers.Film)
                | (link.AbsolutePath.Contains("animation") ? SectionModifiers.Anime : SectionModifiers.None)
                | (link.AbsolutePath.Contains("cartoons") ? SectionModifiers.Cartoon : SectionModifiers.None));

            if (episodes.Length > 0)
            {
                item.Details.Status = new Status(
                    currentSeason: episodes.Where(ep => ep.DateTime != null && ep.DateTime < DateTime.Now)
                        .OrderByDescending(ep => ep.Season).FirstOrDefault()?.Season,
                    currentEpisode: episodes.Where(ep => ep.DateTime != null && ep.DateTime < DateTime.Now)
                        .OrderByDescending(ep => ep.Episode).FirstOrDefault()?.Episode,
                    totalEpisodes: episodes.OrderByDescending(ep => ep.Episode).First()?.Episode);
            }

            var ratingNumber = html.QuerySelector("[itemtype='http://data-vocabulary.org/Rating'] [itemprop=average]")?
                .TextContent?.ToDoubleOrNull();
            if (ratingNumber.HasValue)
            {
                item.Details.Rating = new NumberBasedRating(10, ratingNumber.Value);
            }

            var base64ImbdLink = html.QuerySelector(".b-post__info_rates.imdb a[href]")?.GetAttribute("href")?
                .Split(new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries).Last();
            var imdbId = DecodeUriFromBase64(base64ImbdLink)?.Segments.LastOrDefault()?.Trim('/');
            if (!string.IsNullOrEmpty(imdbId)
                && !item.Details.LinkedIds.ContainsKey(Sites.IMDb))
            {
                item.Details.LinkedIds[Sites.IMDb] = imdbId!;
            }

            var base64KpLink = html.QuerySelector(".b-post__info_rates.kp a[href]")?.GetAttribute("href")?
                .Split(new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries).Last();
            var kpId = DecodeUriFromBase64(base64KpLink)?.Segments.LastOrDefault()?.Trim('/');
            if (!string.IsNullOrEmpty(kpId)
                && !item.Details.LinkedIds.ContainsKey(Sites.Kinopoisk))
            {
                item.Details.LinkedIds[Sites.Kinopoisk] = kpId!;
            }

            return true;
        }

        private static Uri? DecodeUriFromBase64(string? base64)
        {
            if (base64 == null)
            {
                return null;
            }

            try
            {
                var unescapedLinkStr = Uri.UnescapeDataString(Encoding.UTF8.GetString(Convert.FromBase64String(base64)));
                if (Uri.TryCreate(unescapedLinkStr, UriKind.Absolute, out var result))
                {
                    return result;
                }
            }
            catch
            {
                // ignore
            }
            return null;
        }

        private EpisodeInfo[] GetEpisodesCalendar(IElement htmlDocument)
        {
            return htmlDocument
                .QuerySelectorAll(".b-post__schedule_list tr")
                .Select(tr =>
                {
                    var td1 = tr.QuerySelector(".td-1")?.TextContent?
                        .Split(' ').Select(p => p.GetDigits().ToIntOrNull()).Where(num => num.HasValue)
                        .ToArray();
                    var episode = td1?.LastOrDefault();
                    var season = td1?.Reverse().Skip(1).FirstOrDefault();
                    if (episode == null)
                    {
                        return null;
                    }

                    DateTimeOffset? parsedDate = null;
                    if (!DateTimeOffset.TryParseExact(tr.QuerySelector(".td-4")?.TextContent, "dd MMMM yyyy",
                        ruCultureInfo, DateTimeStyles.None, out var parsedDateE))
                    {
                        parsedDate = parsedDateE;
                    }

                    return new EpisodeInfo()
                    {
                        Season = season ?? 1,
                        Episode = episode,
                        Title = tr.QuerySelector(".td-2")?.TextContent.Trim(),
                        DateTime = parsedDate
                    };
                })
                .Where(ep => ep != null)
                .ToArray()!;
        }

        private IEnumerable<TagsContainer> GetTagsContainers(IElement? htmlDocument)
        {
            if (htmlDocument is null)
            {
                yield break;
            }

            var genres = htmlDocument.QuerySelector(".b-post__info td h2:contains('Жанр')")?
                .ParentElement?.NextElementSibling?.TextContent?
                .Split(',').Select(genre => genre.Trim())
                .Where(name => !string.IsNullOrEmpty(name))
                .Select(genre => new TitledTag(genre))
                .ToArray();
            if (genres?.Length > 0)
            {
                yield return new TagsContainer(TagType.Genre, genres);
            }

            var directors = htmlDocument.QuerySelector(".b-post__info td h2:contains('Режиссер')")?
                .ParentElement?.NextElementSibling?.TextContent?
                .Split(',').Select(director => director.Trim())
                .Where(name => !string.IsNullOrEmpty(name))
                .Select(director => new TitledTag(director))
                .ToArray();
            if (directors?.Length > 0)
            {
                yield return new TagsContainer(TagType.Director, directors);
            }

            var countries = htmlDocument.QuerySelector(".b-post__info td h2:contains('Страна')")?
                .ParentElement?.NextElementSibling?.TextContent?
                .Split(',').Select(country => country.Trim())
                .Where(name => !string.IsNullOrEmpty(name))
                .Select(country => new TitledTag(country))
                .ToArray();
            if (countries?.Length > 0)
            {
                yield return new TagsContainer(TagType.County, countries);
            }

            var actors = htmlDocument.QuerySelector(".b-post__info td h2:contains('актеры')")?
                .ParentElement?.NextElementSibling?.TextContent?
                .Split(',').Select(actor => actor.Trim())
                .Where(name => !string.IsNullOrEmpty(name))
                .Select(actor => new TitledTag(actor))
                .ToArray();
            if (actors?.Length > 0)
            {
                yield return new TagsContainer(TagType.Actor, actors);
            }
        }

        public static ItemInfo ParseItemInfoFromTileHtml(Site site, Uri domain, IElement htmlItem)
        {
            var link = htmlItem.GetAttribute("data-url")?.ToUriOrNull(domain);
            var id = htmlItem.GetAttribute("data-id")?.ToIntOrNull();
            var title = htmlItem.QuerySelector(".b-content__inline_item-link a")?.TextContent?.Trim();
            var poster = htmlItem.QuerySelector("img[src]")?.GetAttribute("src")?.ToUriOrNull(domain);
            var year = htmlItem.QuerySelector(".b-content__inline_item-link div")?.TextContent?.Split('-', ',')
                .First()?.GetDigits().ToIntOrNull();

            var section = Section.CreateDefault(SectionModifiers.None
                | (((htmlItem.QuerySelector(".info") != null && !(link?.AbsolutePath.Contains("films") ?? false))
                    || (link?.AbsolutePath.Contains("series") ?? false)) ? SectionModifiers.Serial
                    : SectionModifiers.Film)
                | (link?.AbsolutePath.Contains("animation") ?? false ? SectionModifiers.Anime : SectionModifiers.None)
                | (link?.AbsolutePath.Contains("cartoons") ?? false ? SectionModifiers.Cartoon : SectionModifiers.None));

            return new ItemInfo(site, id?.ToString())
            {
                Link = link,
                Poster = poster,
                Section = section,
                Title = title,
                Details =
                {
                    Year = year > 1900 && year < 2100 ? year : null
                }
            };
        }
    }
}
