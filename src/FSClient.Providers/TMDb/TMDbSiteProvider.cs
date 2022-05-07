namespace FSClient.Providers
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;

    using FSClient.Localization.Resources;
    using FSClient.Shared;
    using FSClient.Shared.Helpers.TimeSpanSemaphore;
    using FSClient.Shared.Helpers.Web;
    using FSClient.Shared.Models;
    using FSClient.Shared.Providers;
    using FSClient.Shared.Services;

    using Humanizer;

    using Newtonsoft.Json.Linq;

    public class TMDbSiteProvider : BaseSiteProvider
    {
        internal const string IgnoredKeywordsKey = "IgnoredKeywords";

        public static readonly Site SiteKey
            = Sites.TMDb;

        private readonly IAppLanguageService languageService;

        public TMDbSiteProvider(IProviderConfigService providerConfigService, IAppLanguageService languageService) : base(
            providerConfigService,
            new ProviderConfig(SiteKey,
                canBeMain: true,
                enforceDisabled: string.IsNullOrEmpty(Secrets.TMDbApiKey),
                priority: 10,
                mirrors: new[] { new Uri("https://www.themoviedb.org") }))
        {
            this.languageService = languageService;
        }

        public static readonly Uri Domain = new Uri("https://www.themoviedb.org");
        public static readonly string CartoonId = "16";

        public static readonly List<string> SupportedTagsForSearch = new List<string>
        {
            "genres",
            "crew",
            "cast",
            "companies",
            "keywords",
            "people",
            "networks"
        };

        public static IReadOnlyList<Section> Sections => new Section[]
        {
            Section.Any,
            new Section("films", Strings.Section_Films) { Modifier = SectionModifiers.Film },
            new Section("serialy", Strings.Section_Serials) { Modifier = SectionModifiers.Serial },
            new Section("multfilmy", Strings.Section_CartoonFilm) { Modifier = SectionModifiers.Cartoon | SectionModifiers.Film },
            new Section("multserialy", Strings.Section_CartoonSerial) { Modifier = SectionModifiers.Cartoon | SectionModifiers.Serial }
        };

        /// <summary>
        /// <seealso cref="https://developers.themoviedb.org/3/getting-started/request-rate-limiting"/>
        /// </summary>      
        private static readonly ITimeSpanSemaphore semaphore = TimeSpanSemaphore.Create(40, TimeSpan.FromSeconds(10));

        private static readonly Uri apiEndpoint = new Uri("https://api.themoviedb.org/3/");
        private static readonly Uri imageEndpoint = new Uri("https://image.tmdb.org/t/p/");

        public static WebImage GetImage(string? filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                return null;
            }

            return new WebImage
            {
                [ImageSize.Original] = new Uri(imageEndpoint, $"original{filePath}"),
                [ImageSize.Preview] = new Uri(imageEndpoint, $"w300{filePath}"),
                [ImageSize.Thumb] = new Uri(imageEndpoint, $"w185{filePath}")
            };
        }

        public override ITimeSpanSemaphore RequestSemaphore => semaphore;

        public Task<HttpResponseMessage?> GetFromApiAsync(string request,
            CancellationToken token = default)
        {
            return GetFromApiAsync(request, new Dictionary<string, string?>(), token);
        }

        public Task<HttpResponseMessage?> GetFromApiAsync(string request,
            IEnumerable<KeyValuePair<string, string?>> arguments, CancellationToken token = default)
        {
            return HttpClient
                .GetBuilder(new Uri(apiEndpoint, request))
                .WithTimeSpanSemaphore(semaphore)
                .WithArgument("api_key", Secrets.TMDbApiKey)
                .WithArgument("language", languageService.GetCurrentLanguage())
                .WithArgument("include_adult", Settings.Instance.IncludeAdult ? "true" : "false")
                .WithArguments(arguments)
                .WithAjax()
                .SendAsync(token);
        }

        internal ValueTask<TitledTag[]> FetchGenresAsync(ICacheService cacheService, bool isTv, CancellationToken cancellationToken)
        {
            const string CacheGenresMoviesKey = "TMDb_Genres_Movies";
            const string CacheGenresTvKey = "TMDb_Genres_Tv";

            // Short ISO form
            var langKey = languageService.GetCurrentLanguage().Substring(0, 2);

            return cacheService.GetOrAddAsync(
                $"{(isTv ? CacheGenresTvKey : CacheGenresMoviesKey)}_{langKey}_2",
                FetchGenresFromApiAsync,
                TimeSpan.FromDays(30),
                cancellationToken);

            Task<TitledTag[]> FetchGenresFromApiAsync(string key, CancellationToken cancellationToken)
            {
                var endpoint = key.StartsWith(CacheGenresMoviesKey, StringComparison.Ordinal)
                    ? "genre/movie/list"
                    : "genre/tv/list";
                return GetFromApiAsync(endpoint, cancellationToken)
                    .AsNewtonsoftJson<JObject>()
                    .ToAsyncEnumerable()
                    .TakeWhile(json => json?["genres"] != null)
                    .SelectMany(json => json!["genres"]!.ToAsyncEnumerable())
                    .OfType<JObject>()
                    .Select(json => new TitledTag(
                        json["name"]?.ToString().Transform(To.TitleCase) ?? "unknown",
                        Site,
                        "genres",
                        json["id"]?.ToString() ?? ""))
                    .Where(i => i.Value != CartoonId)
                    .ToArrayAsync(cancellationToken)
                    .AsTask();
            }
        }
    }
}
