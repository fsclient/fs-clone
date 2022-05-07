namespace FSClient.Providers
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    using FSClient.Shared.Helpers;
    using FSClient.Shared.Helpers.TimeSpanSemaphore;
    using FSClient.Shared.Models;
    using FSClient.Shared.Providers;
    using FSClient.Shared.Services;

    public class ExFSSiteProvider : BaseSiteProvider
    {
        private static readonly ITimeSpanSemaphore requestSemaphore =
            TimeSpanSemaphore.Create(5, TimeSpan.FromSeconds(1));

        public ExFSSiteProvider(IProviderConfigService providerConfigService) : base(
            providerConfigService,
            new ProviderConfig(Sites.ExFS,
                canBeMain: true,
                priority: int.MinValue,
                mirrors: new[] { new Uri("https://ex-fs.net") }))
        {
        }

        public override ITimeSpanSemaphore RequestSemaphore => requestSemaphore;

        public override async ValueTask<ItemInfo> EnsureItemAsync(ItemInfo itemInfo, CancellationToken cancellationToken)
        {
            var domain = await GetMirrorAsync(cancellationToken).ConfigureAwait(false);

            itemInfo.Link = Uri.TryCreate(domain, itemInfo.Link?.GetPath(), out var tempLink)
                ? tempLink
                : itemInfo.Link;

            itemInfo.Poster = GetPoster(domain, itemInfo.Poster[ImageSize.Preview]?.GetPath()) ?? itemInfo.Poster;

            itemInfo.Section = GetSectionFromUrl(itemInfo.Link) is var section && section != Section.Any
                ? section
                : itemInfo.Section;

            return itemInfo;
        }

        public static readonly IReadOnlyList<Section> Sections = new List<Section>
        {
            Section.Any,
            new Section("films", "Фильмы") { Modifier = SectionModifiers.Film },
            new Section("series", "Сериалы") { Modifier = SectionModifiers.Serial },
            new Section("cartoon", "Мультфильмы") { Modifier = SectionModifiers.Cartoon },
            new Section("show", "Передачи и шоу") { Modifier = SectionModifiers.Serial | SectionModifiers.TVShow }
        };

        public static readonly TitledTag[] Genres =
        {
            TitledTag.Any,
            new TitledTag("Аниме",  Sites.ExFS, "genre", "аниме"),
            new TitledTag("Биография", Sites.ExFS, "genre", "биография"),
            new TitledTag("Боевик", Sites.ExFS, "genre", "боевик"),
            new TitledTag("Вестерн", Sites.ExFS, "genre", "вестерн"),
            new TitledTag("Военный", Sites.ExFS, "genre", "военный"),
            new TitledTag("Детектив", Sites.ExFS, "genre", "детектив"),
            new TitledTag("Детский", Sites.ExFS, "genre", "детский"),
            new TitledTag("Для взрослых", Sites.ExFS, "genre", "для взрослых"),
            new TitledTag("Документальный", Sites.ExFS, "genre", "документальный"),
            new TitledTag("Драма", Sites.ExFS, "genre", "драма"),
            new TitledTag("Игра", Sites.ExFS, "genre", "игра"),
            new TitledTag("История", Sites.ExFS, "genre", "история"),
            new TitledTag("Комедия", Sites.ExFS, "genre", "комедия"),
            new TitledTag("Короткометражка", Sites.ExFS, "genre", "короткометражка"),
            new TitledTag("Концерт", Sites.ExFS, "genre", "концерт"),
            new TitledTag("Криминал", Sites.ExFS, "genre", "криминал"),
            new TitledTag("Мелодрама", Sites.ExFS, "genre", "мелодрама"),
            new TitledTag("Музыка", Sites.ExFS, "genre", "музыка"),
            new TitledTag("Мюзикл", Sites.ExFS, "genre", "мюзикл"),
            new TitledTag("Новости", Sites.ExFS, "genre", "новости"),
            new TitledTag("Приключения", Sites.ExFS, "genre", "приключения"),
            new TitledTag("Реальное ТВ", Sites.ExFS, "genre", "реальное ТВ"),
            new TitledTag("Семейный", Sites.ExFS, "genre", "семейный"),
            new TitledTag("Спорт", Sites.ExFS, "genre", "спорт"),
            new TitledTag("Ток-шоу", Sites.ExFS, "genre", "ток-шоу"),
            new TitledTag("Триллер", Sites.ExFS, "genre", "триллер"),
            new TitledTag("Ужасы", Sites.ExFS, "genre", "ужасы"),
            new TitledTag("Фильм-Нуар", Sites.ExFS, "genre", "фильм-нуар"),
            new TitledTag("Фантастика", Sites.ExFS, "genre", "фантастика"),
            new TitledTag("Фэнтези", Sites.ExFS, "genre", "фэнтези")
        };

        public static readonly TitledTag[] Countries =
        {
            TitledTag.Any,
            new TitledTag("Россия", Sites.ExFS, "country", "Россия"),
            new TitledTag("Украина", Sites.ExFS, "country", "Украина"),
            new TitledTag("США", Sites.ExFS, "country", "США"),
            new TitledTag("СССР", Sites.ExFS, "country", "СССР"),
            new TitledTag("Австралия", Sites.ExFS, "country", "Австралия"),
            new TitledTag("Австрия", Sites.ExFS, "country", "Австрия"),
            new TitledTag("Алжир", Sites.ExFS, "country", "Алжир"),
            new TitledTag("Аргентина", Sites.ExFS, "country", "Аргентина"),
            new TitledTag("Армения", Sites.ExFS, "country", "Армения"),
            new TitledTag("Аруба", Sites.ExFS, "country", "Аруба"),
            new TitledTag("Багамы", Sites.ExFS, "country", "Багамы"),
            new TitledTag("Беларусь", Sites.ExFS, "country", "Беларусь"),
            new TitledTag("Бельгия", Sites.ExFS, "country", "Бельгия"),
            new TitledTag("Болгария", Sites.ExFS, "country", "Болгария"),
            new TitledTag("Боливия", Sites.ExFS, "country", "Боливия"),
            new TitledTag("Босния-Герцеговина", Sites.ExFS, "country", "Босния-Герцеговина"),
            new TitledTag("Бразилия", Sites.ExFS, "country", "Бразилия"),
            new TitledTag("Великобритания", Sites.ExFS, "country", "Великобритания"),
            new TitledTag("Венгрия", Sites.ExFS, "country", "Венгрия"),
            new TitledTag("Венесуэла", Sites.ExFS, "country", "Венесуэла"),
            new TitledTag("Вьетнам", Sites.ExFS, "country", "Вьетнам"),
            new TitledTag("Гана", Sites.ExFS, "country", "Гана"),
            new TitledTag("Гватемала", Sites.ExFS, "country", "Гватемала"),
            new TitledTag("Германия", Sites.ExFS, "country", "Германия"),
            new TitledTag("Гонконг", Sites.ExFS, "country", "Гонконг"),
            new TitledTag("Греция", Sites.ExFS, "country", "Греция"),
            new TitledTag("Грузия", Sites.ExFS, "country", "Грузия"),
            new TitledTag("Дания", Sites.ExFS, "country", "Дания"),
            new TitledTag("Доминикана", Sites.ExFS, "country", "Доминикана"),
            new TitledTag("Египет", Sites.ExFS, "country", "Египет"),
            new TitledTag("Израиль", Sites.ExFS, "country", "Израиль"),
            new TitledTag("Индия", Sites.ExFS, "country", "Индия"),
            new TitledTag("Индонезия", Sites.ExFS, "country", "Индонезия"),
            new TitledTag("Иордания", Sites.ExFS, "country", "Иордания"),
            new TitledTag("Иран", Sites.ExFS, "country", "Иран"),
            new TitledTag("Ирландия", Sites.ExFS, "country", "Ирландия"),
            new TitledTag("Исландия", Sites.ExFS, "country", "Исландия"),
            new TitledTag("Испания", Sites.ExFS, "country", "Испания"),
            new TitledTag("Италия", Sites.ExFS, "country", "Италия"),
            new TitledTag("Казахстан", Sites.ExFS, "country", "Казахстан"),
            new TitledTag("Камбоджа", Sites.ExFS, "country", "Камбоджа"),
            new TitledTag("Камерун", Sites.ExFS, "country", "Камерун"),
            new TitledTag("Канада", Sites.ExFS, "country", "Канада"),
            new TitledTag("Катар", Sites.ExFS, "country", "Катар"),
            new TitledTag("Кения", Sites.ExFS, "country", "Кения"),
            new TitledTag("Китай", Sites.ExFS, "country", "Китай"),
            new TitledTag("Колумбия", Sites.ExFS, "country", "Колумбия"),
            new TitledTag("Корея Южная", Sites.ExFS, "country", "Корея Южная"),
            new TitledTag("Косово", Sites.ExFS, "country", "Косово"),
            new TitledTag("Куба", Sites.ExFS, "country", "Куба"),
            new TitledTag("Лаос", Sites.ExFS, "country", "Лаос"),
            new TitledTag("Латвия", Sites.ExFS, "country", "Латвия"),
            new TitledTag("Ливан", Sites.ExFS, "country", "Ливан"),
            new TitledTag("Литва", Sites.ExFS, "country", "Литва"),
            new TitledTag("Лихтенштейн", Sites.ExFS, "country", "Лихтенштейн"),
            new TitledTag("Люксембург", Sites.ExFS, "country", "Люксембург"),
            new TitledTag("Мадагаскар", Sites.ExFS, "country", "Мадагаскар"),
            new TitledTag("Македония", Sites.ExFS, "country", "Македония"),
            new TitledTag("Малайзия", Sites.ExFS, "country", "Малайзия"),
            new TitledTag("Мальта", Sites.ExFS, "country", "Мальта"),
            new TitledTag("Марокко", Sites.ExFS, "country", "Марокко"),
            new TitledTag("Мексика", Sites.ExFS, "country", "Мексика"),
            new TitledTag("Монако", Sites.ExFS, "country", "Монако"),
            new TitledTag("Непал", Sites.ExFS, "country", "Непал"),
            new TitledTag("Нидерланды", Sites.ExFS, "country", "Нидерланды"),
            new TitledTag("Новая Зеландия", Sites.ExFS, "country", "Новая Зеландия"),
            new TitledTag("Норвегия", Sites.ExFS, "country", "Норвегия"),
            new TitledTag("ОАЭ", Sites.ExFS, "country", "ОАЭ"),
            new TitledTag("Остров Мэн", Sites.ExFS, "country", "Остров Мэн"),
            new TitledTag("Палестина", Sites.ExFS, "country", "Палестина"),
            new TitledTag("Панама", Sites.ExFS, "country", "Панама"),
            new TitledTag("Парагвай", Sites.ExFS, "country", "Парагвай"),
            new TitledTag("Перу", Sites.ExFS, "country", "Перу"),
            new TitledTag("Польша", Sites.ExFS, "country", "Польша"),
            new TitledTag("Португалия", Sites.ExFS, "country", "Португалия"),
            new TitledTag("Пуэрто Рико", Sites.ExFS, "country", "Пуэрто Рико"),
            new TitledTag("Румыния", Sites.ExFS, "country", "Румыния"),
            new TitledTag("Саудовская Аравия", Sites.ExFS, "country", "Саудовская Аравия"),
            new TitledTag("Сербия", Sites.ExFS, "country", "Сербия"),
            new TitledTag("Сингапур", Sites.ExFS, "country", "Сингапур"),
            new TitledTag("Словакия", Sites.ExFS, "country", "Словакия"),
            new TitledTag("Словения", Sites.ExFS, "country", "Словения"),
            new TitledTag("Таиланд", Sites.ExFS, "country", "Таиланд"),
            new TitledTag("Тайвань", Sites.ExFS, "country", "Тайвань"),
            new TitledTag("Тунис", Sites.ExFS, "country", "Тунис"),
            new TitledTag("Турция", Sites.ExFS, "country", "Турция"),
            new TitledTag("Уругвай", Sites.ExFS, "country", "Уругвай"),
            new TitledTag("Филиппины", Sites.ExFS, "country", "Филиппины"),
            new TitledTag("Финляндия", Sites.ExFS, "country", "Финляндия"),
            new TitledTag("Франция", Sites.ExFS, "country", "Франция"),
            new TitledTag("Хорватия", Sites.ExFS, "country", "Хорватия"),
            new TitledTag("Черногория", Sites.ExFS, "country", "Черногория"),
            new TitledTag("Чехия", Sites.ExFS, "country", "Чехия"),
            new TitledTag("Чили", Sites.ExFS, "country", "Чили"),
            new TitledTag("Швейцария", Sites.ExFS, "country", "Швейцария"),
            new TitledTag("Швеция", Sites.ExFS, "country", "Швеция"),
            new TitledTag("Шри-Ланка", Sites.ExFS, "country", "Шри-Ланка"),
            new TitledTag("Эстония", Sites.ExFS, "country", "Эстония"),
            new TitledTag("ЮАР", Sites.ExFS, "country", "ЮАР"),
            new TitledTag("Югославия", Sites.ExFS, "country", "Югославия"),
            new TitledTag("Япония", Sites.ExFS, "country", "Япония")
        };

        public static readonly TitledTag[] Qualities =
        {
            TitledTag.Any,
            new TitledTag("BDRip", Sites.ExFS, "quality", "BDRip"),
            new TitledTag("HDRip", Sites.ExFS, "quality", "HDRip"),
            new TitledTag("WEBRip", Sites.ExFS, "quality", "WEBRip"),
            new TitledTag("HDTVRip", Sites.ExFS, "quality", "HDTVRip"),
            new TitledTag("DVDRip", Sites.ExFS, "quality", "DVDRip"),
            new TitledTag("CAMRip", Sites.ExFS, "quality", "CAMRip"),
            new TitledTag("CAMRip PROPER", Sites.ExFS, "quality", "CAMRip PROPER"),
            new TitledTag("TS", Sites.ExFS, "quality", "TS"),
            new TitledTag("TS PROPER", Sites.ExFS, "quality", "TS PROPER"),
            new TitledTag("SuperTS", Sites.ExFS, "quality", "SuperTS"),
            new TitledTag("TC", Sites.ExFS, "quality", "TC"),
            new TitledTag("DVDScr", Sites.ExFS, "quality", "DVDScr"),
            new TitledTag("TVRip", Sites.ExFS, "quality", "TVRip"),
            new TitledTag("SATRip", Sites.ExFS, "quality", "SATRip")
        };

        public static readonly TitledTag[] AgeLimits =
        {
            TitledTag.Any,
            new TitledTag("0+", Sites.ExFS, "age_limit", "0"),
            new TitledTag("6+", Sites.ExFS, "age_limit", "6"),
            new TitledTag("12+", Sites.ExFS, "age_limit", "12"),
            new TitledTag("16+", Sites.ExFS, "age_limit", "16"),
            new TitledTag("18+", Sites.ExFS, "age_limit", "18")
        };

        public static string? GetIdFromUrl(Uri? link)
        {
            // http://ex-fs.net/films/63748-varkraft.html
            // http://ex-fs.net/index.php?newsid=63748

            if (link == null)
            {
                return string.Empty;
            }

            var str = link.GetPath();
            return str
                .Split(new char[] { '/', '=' }, StringSplitOptions.RemoveEmptyEntries)
                .LastOrDefault()?
                .Split('-')
                .FirstOrDefault()?
                .GetDigits()
                .NotEmptyOrNull();
        }

        public static Section GetSectionFromUrl(Uri? link)
        {
            if (link == null)
            {
                return Section.Any;
            }

            var str = link.IsAbsoluteUri ? link.LocalPath : link.OriginalString;
            var parts = str.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
            {
                return Section.Any;
            }

            var sec = Sections.FirstOrDefault(s => parts.Contains(s.Value));
            return sec.Value == null
                ? Section.Any
                : sec;
        }

        public static TitledTag GetTagFromLinkString(string? name, string? link)
        {
            try
            {
                if (name == null || link == null)
                {
                    return default;
                }

                var parts = link.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 2)
                {
                    return default;
                }

                var type = Uri.UnescapeDataString(parts[^2]);
                var value = Uri.UnescapeDataString(parts[^1]);

                return new TitledTag(name, Sites.ExFS, type, value);
            }
            catch
            {
                return default;
            }
        }

        public static WebImage? GetPoster(Uri domain, string? poster)
        {
            // http://ex-fs.net/uploads/posts/2018-01/1515448342_f1d74f5.jpg
            // http://ex-fs.net/thumbs.php?src=http://ex-fs.net/uploads/posts/2018-04/1524120281_ccb06b3.jpg&w=152&h=220&zc=1

            if (!Uri.TryCreate(domain, poster, out var exFsLink))
            {
                return null;
            }

            var original = GetClearPosterUri(exFsLink);
            if (original == null)
            {
                return null;
            }

            return new WebImage
            {
                [ImageSize.Original] = original,
                [ImageSize.Preview] = original,
                [ImageSize.Thumb] = new Uri(domain, $"/thumbs.php?src={original}&w=152&h=220")
            };
        }

        private static Uri? GetClearPosterUri(Uri? uri)
        {
            if (string.IsNullOrEmpty(uri?.Query))
            {
                return uri;
            }

            var parts = uri!.Query.Split(new[] { "?", "&", "=" }, StringSplitOptions.RemoveEmptyEntries);
            var srcIndex = Array.IndexOf(parts, "src") + 1;
            if (parts.Length > srcIndex
                && Uri.TryCreate(uri, parts[srcIndex], out var src))
            {
                return src;
            }

            return uri;
        }
    }
}
