namespace FSClient.Providers
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;

    using AngleSharp.Dom;

    using FSClient.Shared.Helpers;
    using FSClient.Shared.Helpers.TimeSpanSemaphore;
    using FSClient.Shared.Helpers.Web;
    using FSClient.Shared.Models;
    using FSClient.Shared.Providers;
    using FSClient.Shared.Services;

    using Newtonsoft.Json.Linq;

    public class FilmixSiteProvider : BasePlayerJsSiteProvider
    {
        private static readonly ITimeSpanSemaphore requestSemaphore =
            TimeSpanSemaphore.Create(1, TimeSpan.FromSeconds(1));

        internal const string FullHDOnlyForProKey = "FullHDOnlyForPro";

        public FilmixSiteProvider(IProviderConfigService providerConfigService) : base(
            providerConfigService,
            new ProviderConfig(Sites.Filmix,
                canBeMain: true,
                priority: 9,
                requirements: ProviderRequirements.ProForSpecial,
                properties: new Dictionary<string, string?>
                {
                    [PlayerJsConfigKey] = "{\"Keys\":[\"bzl3UHQwaWk0MkdXZVM3TDdB\", \"bE5qSTlWNVUxZ01uc3h0NFFy\", \"SURhQnQwOEM5V2Y3bFlyMGVI\", \"MTluMWlLQnI4OXVic2tTNXpU\", \"Mm93S0RVb0d6c3VMTkV5aE54\"],\"Separator\":\":<:\"}",
                    [FullHDOnlyForProKey] = "true"
                },
                healthCheckRelativeLink: new Uri("/healthz.html", UriKind.Relative),
                mirrors: new[] { new Uri("https://filmix.ac") }))
        {
        }

        public override ITimeSpanSemaphore RequestSemaphore => requestSemaphore;

        public override ValueTask<ItemInfo> EnsureItemAsync(ItemInfo itemInfo, CancellationToken cancellationToken)
        {
            itemInfo.Link = itemInfo.Link?.GetPath().ToUriOrNull();

            if (Sections.FirstOrDefault(s => s.Value == itemInfo.Section.Value) is var section
                && section != Section.Any)
            {
                itemInfo.Section = section;
            }

            return new ValueTask<ItemInfo>(itemInfo);
        }

        protected override async ValueTask<bool> IsValidMirrorResponse(HttpResponseMessage responseMessage, CancellationToken cancellationToken)
        {
            if (!responseMessage.IsSuccessStatusCode)
            {
                return false;
            }

            var pageText = await responseMessage.AsText().ConfigureAwait(false);

            if (JsonHelper.ParseOrNull(pageText) is JObject json
                && json?["status"]?.ToString() == "ok")
            {
                return true;
            }

            var isValid = pageText != null && (
                pageText.Contains("/films") == true
                || pageText.Contains("/filmy") == true
                // PRO mirror doesn't contain link to /films
                || pageText.Contains("Доступ исключительно для пользователей"));

            return isValid;
        }

        public static readonly IReadOnlyList<Section> Sections = new List<Section>
        {
            Section.Any,
            new Section("films", "Фильмы") { Modifier = SectionModifiers.Film },
            new Section("serialy", "Сериалы") { Modifier = SectionModifiers.Serial },
            new Section("multfilmy", "Мультфильмы") { Modifier = SectionModifiers.Cartoon | SectionModifiers.Film },
            new Section("multserialy", "Мультсериалы") { Modifier = SectionModifiers.Serial | SectionModifiers.Cartoon }
        };

        public static readonly TitledTag[] Genres =
        {
            TitledTag.Any,
            new TitledTag("Аниме", Sites.Filmix, "genre", "g21"),
            new TitledTag("Биография", Sites.Filmix, "genre", "g76"),
            new TitledTag("Боевики", Sites.Filmix, "genre", "g3"),
            new TitledTag("Вестерн", Sites.Filmix, "genre", "g81"),
            new TitledTag("Военный", Sites.Filmix, "genre", "g20"),
            new TitledTag("Детектив", Sites.Filmix, "genre", "g18"),
            new TitledTag("Детский", Sites.Filmix, "genre", "g84"),
            new TitledTag("Для взрослых", Sites.Filmix, "genre", "g100"),
            new TitledTag("Документальные", Sites.Filmix, "genre", "g15"),
            new TitledTag("Дорамы", Sites.Filmix, "genre", "g102"),
            new TitledTag("Драмы", Sites.Filmix, "genre", "g1"),
            new TitledTag("Игра", Sites.Filmix, "genre", "g95"),
            new TitledTag("Исторический", Sites.Filmix, "genre", "g75"),
            new TitledTag("Комедия", Sites.Filmix, "genre", "g6"),
            new TitledTag("Короткометражка", Sites.Filmix, "genre", "g80"),
            new TitledTag("Криминал", Sites.Filmix, "genre", "g71"),
            new TitledTag("Мелодрама", Sites.Filmix, "genre", "g12"),
            new TitledTag("Мистика", Sites.Filmix, "genre", "g5"),
            new TitledTag("Музыка", Sites.Filmix, "genre", "g96"),
            new TitledTag("Мюзикл", Sites.Filmix, "genre", "g72"),
            new TitledTag("Новости", Sites.Filmix, "genre", "g103"),
            new TitledTag("Оригинал", Sites.Filmix, "genre", "g83"),
            new TitledTag("Отечественные", Sites.Filmix, "genre", "g79"),
            new TitledTag("Передачи с ТВ", Sites.Filmix, "genre", "g16"),
            new TitledTag("Приключения", Sites.Filmix, "genre", "g74"),
            new TitledTag("Реальное ТВ", Sites.Filmix, "genre", "g99"),
            new TitledTag("Семейный", Sites.Filmix, "genre", "g73"),
            new TitledTag("Ситком", Sites.Filmix, "genre", "g101"),
            new TitledTag("Спорт", Sites.Filmix, "genre", "g77"),
            new TitledTag("Ток-шоу", Sites.Filmix, "genre", "g98"),
            new TitledTag("Трейлеры", Sites.Filmix, "genre", "g10"),
            new TitledTag("Триллеры", Sites.Filmix, "genre", "g8"),
            new TitledTag("Ужасы", Sites.Filmix, "genre", "g2"),
            new TitledTag("Фантастика", Sites.Filmix, "genre", "g13"),
            new TitledTag("Фильм-нуар", Sites.Filmix, "genre", "g97"),
            new TitledTag("Фэнтези", Sites.Filmix, "genre", "g4")
        };

        private static readonly Dictionary<string, string> genreCodes = new Dictionary<string, string>
        {
            ["trejlery"] = "g10",
            ["sr_trejlery"] = "g10",
            ["mf_trejlery"] = "g10",
            ["anime"] = "g21",
            ["biografia"] = "g76",
            ["boeviki"] = "g3",
            ["vestern"] = "g81",
            ["voenniy"] = "g20",
            ["game"] = "g95",
            ["detektiv"] = "g18",
            ["detskij"] = "g84",
            ["dokumentalenye"] = "g15",
            ["dorama"] = "g102",
            ["dramy"] = "g1",
            ["istoricheskij"] = "g75",
            ["komedii"] = "g6",
            ["kriminal"] = "g71",
            ["melodramy"] = "g12",
            ["mistika"] = "g5",
            ["music"] = "g96",
            ["muzkl"] = "g72",
            ["novosti"] = "g103",
            ["original"] = "g83",
            ["otechestvennye"] = "g79",
            ["tv"] = "g16",
            ["priklucheniya"] = "g74",
            ["real_tv"] = "g99",
            ["semejnyj"] = "g73",
            ["sitcom"] = "g101",
            ["sport"] = "g77",
            ["talk_show"] = "g98",
            ["trillery"] = "g8",
            ["uzhasy"] = "g2",
            ["fantastika"] = "g13",
            ["film_noir"] = "g97",
            ["fjuntezi"] = "g4",
            ["for_adults"] = "g100"
        };

        public static readonly TitledTag[] Countries =
        {
            TitledTag.Any,
            new TitledTag("Зарубежные", Sites.Filmix, "county", "c996"),
            new TitledTag("США", Sites.Filmix, "country", "c2"),
            new TitledTag("Россия", Sites.Filmix, "country", "c6"),
            new TitledTag("Украина", Sites.Filmix, "country", "c34"),
            new TitledTag("СССР", Sites.Filmix, "country", "c64"),
            new TitledTag("Австралия", Sites.Filmix, "country", "c53"),
            new TitledTag("Австрия", Sites.Filmix, "country", "c72"),
            new TitledTag("Азербайджан", Sites.Filmix, "country", "c73"),
            new TitledTag("Албания", Sites.Filmix, "country", "c74"),
            new TitledTag("Алжир", Sites.Filmix, "country", "c75"),
            new TitledTag("Англия", Sites.Filmix, "country", "c76"),
            new TitledTag("Ангола", Sites.Filmix, "country", "c31"),
            new TitledTag("Андорра", Sites.Filmix, "country", "c30"),
            new TitledTag("Аргентина", Sites.Filmix, "country", "c21"),
            new TitledTag("Армения", Sites.Filmix, "country", "c78"),
            new TitledTag("Афганистан", Sites.Filmix, "country", "c79"),
            new TitledTag("Багамы", Sites.Filmix, "country", "c80"),
            new TitledTag("Бангладеш", Sites.Filmix, "country", "c32"),
            new TitledTag("Барбадос", Sites.Filmix, "country", "c81"),
            new TitledTag("Бахрейн", Sites.Filmix, "country", "c82"),
            new TitledTag("Беларусь", Sites.Filmix, "country", "c35"),
            new TitledTag("Белиз", Sites.Filmix, "country", "c83"),
            new TitledTag("Бельгия", Sites.Filmix, "country", "c36"),
            new TitledTag("Бенин", Sites.Filmix, "country", "c84"),
            new TitledTag("Болгария", Sites.Filmix, "country", "c66"),
            new TitledTag("Боливия", Sites.Filmix, "country", "c22"),
            new TitledTag("Босния и Герцеговина", Sites.Filmix, "country", "c85"),
            new TitledTag("Ботсвана", Sites.Filmix, "country", "c86"),
            new TitledTag("Бразилия", Sites.Filmix, "country", "c19"),
            new TitledTag("Бутан", Sites.Filmix, "country", "c90"),
            new TitledTag("Вануату", Sites.Filmix, "country", "c91"),
            new TitledTag("Великобритания", Sites.Filmix, "country", "c13"),
            new TitledTag("Венгрия", Sites.Filmix, "country", "c37"),
            new TitledTag("Венесуэла", Sites.Filmix, "country", "c93"),
            new TitledTag("Вьетнам", Sites.Filmix, "country", "c95"),
            new TitledTag("Гаити", Sites.Filmix, "country", "c97"),
            new TitledTag("Гана", Sites.Filmix, "country", "c100"),
            new TitledTag("Гватемала", Sites.Filmix, "country", "c101"),
            new TitledTag("Германия", Sites.Filmix, "country", "c3"),
            new TitledTag("Голландия", Sites.Filmix, "country", "c9"),
            new TitledTag("Гондурас", Sites.Filmix, "country", "c104"),
            new TitledTag("Гонконг", Sites.Filmix, "country", "c105"),
            new TitledTag("Греция", Sites.Filmix, "country", "c62"),
            new TitledTag("Грузия", Sites.Filmix, "country", "c55"),
            new TitledTag("Дания", Sites.Filmix, "country", "c65"),
            new TitledTag("Доминика", Sites.Filmix, "country", "c108"),
            new TitledTag("ДР Конго", Sites.Filmix, "country", "c110"),
            new TitledTag("Евросоюз", Sites.Filmix, "country", "c111"),
            new TitledTag("Египет", Sites.Filmix, "country", "c23"),
            new TitledTag("Замбия", Sites.Filmix, "country", "c112"),
            new TitledTag("Израиль", Sites.Filmix, "country", "c18"),
            new TitledTag("Индия", Sites.Filmix, "country", "c54"),
            new TitledTag("Индонезия", Sites.Filmix, "country", "c114"),
            new TitledTag("Иордания", Sites.Filmix, "country", "c59"),
            new TitledTag("Ирак", Sites.Filmix, "country", "c38"),
            new TitledTag("Иран", Sites.Filmix, "country", "c39"),
            new TitledTag("Ирландия", Sites.Filmix, "country", "c115"),
            new TitledTag("Исландия", Sites.Filmix, "country", "c69"),
            new TitledTag("Испания", Sites.Filmix, "country", "c27"),
            new TitledTag("Италия", Sites.Filmix, "country", "c7"),
            new TitledTag("Йемен", Sites.Filmix, "country", "c116"),
            new TitledTag("Казахстан", Sites.Filmix, "country", "c14"),
            new TitledTag("Камбоджа", Sites.Filmix, "country", "c118"),
            new TitledTag("Камерун", Sites.Filmix, "country", "c119"),
            new TitledTag("Канада", Sites.Filmix, "country", "c28"),
            new TitledTag("Катар", Sites.Filmix, "country", "c120"),
            new TitledTag("Кения", Sites.Filmix, "country", "c121"),
            new TitledTag("Кипр", Sites.Filmix, "country", "c122"),
            new TitledTag("Киргизия", Sites.Filmix, "country", "c123"),
            new TitledTag("Китай", Sites.Filmix, "country", "c10"),
            new TitledTag("КНР", Sites.Filmix, "country", "c56"),
            new TitledTag("Колумбия", Sites.Filmix, "country", "c125"),
            new TitledTag("Корея", Sites.Filmix, "country", "c12"),
            new TitledTag("Корея Северная", Sites.Filmix, "country", "c178"),
            new TitledTag("Корея Южная", Sites.Filmix, "country", "c25"),
            new TitledTag("Косово", Sites.Filmix, "country", "c127"),
            new TitledTag("Коста-Рика", Sites.Filmix, "country", "c128"),
            new TitledTag("Куба", Sites.Filmix, "country", "c48"),
            new TitledTag("Кувейт", Sites.Filmix, "country", "c130"),
            new TitledTag("Лаос", Sites.Filmix, "country", "c131"),
            new TitledTag("Латвия", Sites.Filmix, "country", "c16"),
            new TitledTag("Либерия", Sites.Filmix, "country", "c133"),
            new TitledTag("Ливан", Sites.Filmix, "country", "c60"),
            new TitledTag("Ливия", Sites.Filmix, "country", "c134"),
            new TitledTag("Литва", Sites.Filmix, "country", "c20"),
            new TitledTag("Лихтенштейн", Sites.Filmix, "country", "c135"),
            new TitledTag("Люксембург", Sites.Filmix, "country", "c136"),
            new TitledTag("Мадагаскар", Sites.Filmix, "country", "c139"),
            new TitledTag("Макао", Sites.Filmix, "country", "c140"),
            new TitledTag("Македония", Sites.Filmix, "country", "c141"),
            new TitledTag("Малайзия", Sites.Filmix, "country", "c58"),
            new TitledTag("Мальта", Sites.Filmix, "country", "c145"),
            new TitledTag("Марокко", Sites.Filmix, "country", "c146"),
            new TitledTag("Мексика", Sites.Filmix, "country", "c57"),
            new TitledTag("Мозамбик", Sites.Filmix, "country", "c149"),
            new TitledTag("Молдавия", Sites.Filmix, "country", "c150"),
            new TitledTag("Монако", Sites.Filmix, "country", "c151"),
            new TitledTag("Монголия", Sites.Filmix, "country", "c29"),
            new TitledTag("Намибия", Sites.Filmix, "country", "c153"),
            new TitledTag("Непал", Sites.Filmix, "country", "c155"),
            new TitledTag("Нигерия", Sites.Filmix, "country", "c157"),
            new TitledTag("Нидерланды", Sites.Filmix, "country", "c158"),
            new TitledTag("Новая Зеландия", Sites.Filmix, "country", "c71"),
            new TitledTag("Норвегия", Sites.Filmix, "country", "c51"),
            new TitledTag("Объединённые Арабские Эмираты", Sites.Filmix, "country", "c160"),
            new TitledTag("Остров Мэн", Sites.Filmix, "country", "c162"),
            new TitledTag("Пакистан", Sites.Filmix, "country", "c163"),
            new TitledTag("Панама", Sites.Filmix, "country", "c165"),
            new TitledTag("Парагвай", Sites.Filmix, "country", "c167"),
            new TitledTag("Перу", Sites.Filmix, "country", "c168"),
            new TitledTag("Польша", Sites.Filmix, "country", "c41"),
            new TitledTag("Португалия", Sites.Filmix, "country", "c40"),
            new TitledTag("Пуэрто Рико", Sites.Filmix, "country", "c70"),
            new TitledTag("Руанда", Sites.Filmix, "country", "c171"),
            new TitledTag("Румыния", Sites.Filmix, "country", "c24"),
            new TitledTag("Саудовская Аравия", Sites.Filmix, "country", "c176"),
            new TitledTag("Сербия", Sites.Filmix, "country", "c68"),
            new TitledTag("Сингапур", Sites.Filmix, "country", "c61"),
            new TitledTag("Сирия", Sites.Filmix, "country", "c184"),
            new TitledTag("Словакия", Sites.Filmix, "country", "c43"),
            new TitledTag("Словения", Sites.Filmix, "country", "c42"),
            new TitledTag("Таджикистан", Sites.Filmix, "country", "c190"),
            new TitledTag("Таиланд", Sites.Filmix, "country", "c63"),
            new TitledTag("Тайвань", Sites.Filmix, "country", "c191"),
            new TitledTag("Танзания", Sites.Filmix, "country", "c192"),
            new TitledTag("Тунис", Sites.Filmix, "country", "c197"),
            new TitledTag("Турция", Sites.Filmix, "country", "c26"),
            new TitledTag("Уганда", Sites.Filmix, "country", "c199"),
            new TitledTag("Узбекистан", Sites.Filmix, "country", "c200"),
            new TitledTag("Уругвай", Sites.Filmix, "country", "c201"),
            new TitledTag("Филиппины", Sites.Filmix, "country", "c205"),
            new TitledTag("Финляндия", Sites.Filmix, "country", "c44"),
            new TitledTag("Франция", Sites.Filmix, "country", "c8"),
            new TitledTag("Хорватия", Sites.Filmix, "country", "c45"),
            new TitledTag("Черногория", Sites.Filmix, "country", "c46"),
            new TitledTag("Чехия", Sites.Filmix, "country", "c47"),
            new TitledTag("Чехословакия", Sites.Filmix, "country", "c217"),
            new TitledTag("Чили", Sites.Filmix, "country", "c208"),
            new TitledTag("Швейцария", Sites.Filmix, "country", "c49"),
            new TitledTag("Швеция", Sites.Filmix, "country", "c50"),
            new TitledTag("Шотландия", Sites.Filmix, "country", "c209"),
            new TitledTag("Шри-Ланка", Sites.Filmix, "country", "c210"),
            new TitledTag("Эквадор", Sites.Filmix, "country", "c211"),
            new TitledTag("Эстония", Sites.Filmix, "country", "c15"),
            new TitledTag("Эфиопия", Sites.Filmix, "country", "c214"),
            new TitledTag("ЮАР", Sites.Filmix, "country", "c52"),
            new TitledTag("Югославия", Sites.Filmix, "country", "c218"),
            new TitledTag("Ямайка", Sites.Filmix, "country", "c216"),
            new TitledTag("Япония", Sites.Filmix, "country", "c11")
        };

        public static readonly TitledTag[] Qualities =
        {
            TitledTag.Any,
            new TitledTag("FHD 1080", Sites.Filmix, "q", "h"),
            new TitledTag("HD 720", Sites.Filmix, "q", "g"),
            new TitledTag("DVDRip, HDRip, SAT", Sites.Filmix, "q", "n"),
            new TitledTag("CAM, TS", Sites.Filmix, "q", "b")
        };

        public static string? GetIdFromUrl(Uri? link)
        {
            // https://filmix.me/fantastika/112159-passazhiry-2016.html
            // https://filmix.me/play/112159

            return link?
                .GetPath()
                .TrimEnd('/')
                .Split('/')
                .LastOrDefault()?
                .Split('-')
                .FirstOrDefault();
        }

        public static WebImage? GetImage(Uri mirror, string? link)
        {
            // http://filmix.info/uploads/posters/big/serial-kremnievaya-dolina-2016_87570_0.jpg
            // http://filmix.info/uploads/posters/thumbs/w220/serial-kremnievaya-dolina-2016_87570_0.jpg
            // https://filmix.me/uploads/frames/124913/f261439-taynaya-vlast-2018_original.jpg
            // https://filmix.me/uploads/frames/124913/f261439-taynaya-vlast-2018_small.jpg

            if (string.IsNullOrWhiteSpace(link))
            {
                return null;
            }

            if (link!.Contains("/frames/"))
            {
                return new WebImage
                {
                    [ImageSize.Preview] = new Uri(mirror, link.Replace("_original.", "_small.")),
                    [ImageSize.Original] = new Uri(mirror, link.Replace("_small.", "_original."))
                };
            }

            var lastSegment = link.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries).LastOrDefault();
            if (string.IsNullOrWhiteSpace(lastSegment))
            {
                return null;
            }

            return new WebImage
            {
                [ImageSize.Thumb] = new Uri(mirror, "uploads/posters/thumbs/w160/" + lastSegment),
                [ImageSize.Preview] = new Uri(mirror, "uploads/posters/thumbs/w220/" + lastSegment),
                [ImageSize.Original] = new Uri(mirror, "uploads/posters/big/" + lastSegment)
            };
        }

        public static TitledTag GetTagFromUriString(string name, string? v)
        {
            if (!Uri.TryCreate(v, UriKind.RelativeOrAbsolute, out var uri))
            {
                return new TitledTag(name);
            }

            var pathSegments = uri?.GetPath().Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            var firstSegment = pathSegments?.FirstOrDefault();
            if (pathSegments == null || firstSegment == null)
            {
                return new TitledTag(name);
            }

            if (firstSegment == "person")
            {
                var personValue = pathSegments.Skip(1).FirstOrDefault();
                return new TitledTag(name, Sites.Filmix, "person", personValue);
            }
            if (!genreCodes.TryGetValue(firstSegment, out var code))
            {
                code = pathSegments.Skip(1).FirstOrDefault();
            }

            var tag = Genres.Concat(Countries).FirstOrDefault(t => t.Value == code);
            return tag == default
                ? new TitledTag(name)
                : tag;
        }

        public static ItemInfo? ParseElement(Uri mirror, IElement i)
        {
            var id = i.GetAttribute("data-id");
            if (id == null)
            {
                return null;
            }

            var status = GetStatusFromString(i.QuerySelector("span.added-info")?.TextContent, StatusType.Unknown);

            var tags = new List<TagsContainer>();

            var category = i
                .QuerySelector(".item.category .item-content")?
                .TextContent
                .Split(',')
                .Select(c => c.Trim())
                .ToList()
                ?? new List<string>();

            if (category.Count > 0)
            {
                tags.Add(new TagsContainer(TagType.Genre, category.Select(c => new TitledTag(c.ToLower())).ToArray()));
            }

            var actors = i
                .QuerySelectorAll(".item.actors .item-content > span")
                .Select(d => GetTagFromUriString(
                    d.QuerySelector("[itemprop=name]")?.TextContent.Trim() ?? d.TextContent.Trim().Trim(','),
                    d.QuerySelector("a")?.GetAttribute("href")))
                .Where(tag => tag.Title != null)
                .ToArray();

            if (actors.Length > 0)
            {
                tags.Add(new TagsContainer(TagType.Actor, actors));
            }

            var link = i.QuerySelector("a[itemprop=url]")?.GetAttribute("href")?.ToUriOrNull(mirror)
                ?? new Uri(mirror, $"drama/{id}-l.html");

            var years = i.QuerySelectorAll("[itemprop=copyrightYear]")
                .Select(y => y.TextContent?.ToIntOrNull())
                .Where(y => y > 0)
                .ToArray();

            var quality = CleanQuality(i.QuerySelector(".quality")?.TextContent);

            var ratingSpan = i.QuerySelector(".rating");
            IRating? rating = null;
            if (ratingSpan != null)
            {
                rating = new UpDownRating(
                    int.TryParse(ratingSpan.QuerySelector(".ratePos")?.TextContent, out var positive) ? positive : 0,
                    int.TryParse(ratingSpan.QuerySelector(".rateNeg")?.TextContent, out var negative) ? negative : 0,
                    ratingSpan.QuerySelector(".positive.active") != null,
                    ratingSpan.QuerySelector(".negative.active") != null,
                    CanVote: false);
            }

            return new ItemInfo(Sites.Filmix, id)
            {
                Section = GetSectionFromGenresAndStatus(category, false, status),
                Title = i.QuerySelector("[itemprop=name]")?.GetAttribute("content"),
                Poster = GetImage(mirror, i.QuerySelector("img[itemprop=image]")?.GetAttribute("src")) ?? default,
                Link = link,
                Details =
                {
                    Status = status,
                    Rating = rating,
                    TitleOrigin = i.QuerySelector("[itemprop=alternativeHeadline]")?.GetAttribute("content"),
                    Year = years.FirstOrDefault(),
                    YearEnd = years.Skip(1).FirstOrDefault(),
                    Quality = quality,
                    Tags = tags.ToArray()
                }
            };
        }

        public static Section GetSectionFromGenresAndStatus(IEnumerable<string> genreTitles, bool mustBeCartoon, Status status)
        {
            var genres = genreTitles.ToList();
            var isAnime = false;
            var isTv = false;
            if (status.CurrentEpisode.HasValue
                || status.CurrentSeason.HasValue)
            {
                genres.Add("сериалы");
            }
            if (mustBeCartoon
                || genres.Contains("аниме"))
            {
                genres.Add("мультфильмы");
                isAnime = true;
            }
            if (genres.Contains("реальное тв")
                || genres.Contains("передачи с тв"))
            {
                isTv = true;
            }

            var itemSection = Sections
                .Where(s => genres.Any(g => g.IndexOf(s.Title, StringComparison.OrdinalIgnoreCase) >= 0))
                .OrderByDescending(s => s.Modifier.HasFlag(SectionModifiers.Cartoon))
                .ThenByDescending(s => s.Modifier.HasFlag(SectionModifiers.Serial))
                .FirstOrDefault();

            if (itemSection.Value == "multfilmy"
                && genres.Contains("сериалы"))
            {
                itemSection = Sections.FirstOrDefault(s => s.Value == "multserialy");
            }

            if (itemSection == Section.Any)
            {
                itemSection = Sections.FirstOrDefault(s => s.Value == "films");
            }

            return new Section(itemSection.Value, itemSection.Title)
            {
                Modifier = itemSection.Modifier
                    | (isAnime ? SectionModifiers.Anime : SectionModifiers.None)
                    | (isTv ? SectionModifiers.TVShow : SectionModifiers.None)
            };
        }

        public static Status GetStatusFromString(string? statusStr, StatusType statusType)
        {
            // 1-5, 7-13 серия (2 сезон)
            // 2-3,6,8-9,12 серия (1 сезон) - Lord32x
            // 20 серия (5 сезон) - ColdFilm
            // 16 Серия - NewStudio

            if (string.IsNullOrEmpty(statusStr))
            {
                return default;
            }

            var parts = statusStr!
                .Split(new[] { " - " }, StringSplitOptions.None)
                .First()
                .Split('-', ',')
                .Last()
                .Split(new[] { ' ', '(' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(e => e.ToIntOrNull())
                .Where(e => e.HasValue)
                .ToArray();

            var hasNoSeason = parts.Length == 1;

            return new Status(
                currentSeason: hasNoSeason ? null : parts.LastOrDefault(),
                currentEpisode: parts.Reverse().Skip(hasNoSeason ? 0 : 1).FirstOrDefault(),
                type: statusType
            );
        }

        public static string? GetSectionStringFromLink(Uri? link)
        {
            if (link == null)
            {
                return null;
            }

            var str = link.IsAbsoluteUri ? link.LocalPath : link.OriginalString;

            var parts = str.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
            {
                return null;
            }

            return parts[0];
        }

        public static string? CleanQuality(string? input)
        {
            return input?
                .Replace("4K", "")
                .Replace("2880", "")
                .Replace("2160", "")
                .Replace("1440", "")
                .Replace("1080", "")
                .Replace("720", "")
                .Replace("480", "")
                .Trim();
        }
    }
}
