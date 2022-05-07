namespace FSClient.Providers
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;

    using FSClient.Shared;
    using FSClient.Shared.Helpers;
    using FSClient.Shared.Helpers.Web;
    using FSClient.Shared.Models;
    using FSClient.Shared.Providers;
    using FSClient.Shared.Services;

    using Newtonsoft.Json.Linq;

    using Nito.AsyncEx;

    public sealed class KinoPubSiteProvider : BaseSiteProvider
    {
        public const int ItemsPerPage = 20;

        public static List<Section> KinoPubSections { get; } = new List<Section>
        {
            Section.Any,
            new Section("movie", "Фильмы") { Modifier = SectionModifiers.Film },
            new Section("serial", "Сериалы") { Modifier = SectionModifiers.Serial },
            new Section("docuserial", "Док.сериалы") { Modifier = SectionModifiers.Serial | SectionModifiers.TVShow },
            new Section("documovie", "Док.фильмы") { Modifier = SectionModifiers.Film | SectionModifiers.TVShow },
            new Section("tvshow", "ТВ-шоу") { Modifier = SectionModifiers.TVShow }
        };

        public static readonly Uri ApiDomain = new Uri("https://api.service-kp.com/v1/");

        private readonly SemaphoreSlim deviceInfoSemaphore;
        private (JObject? deviceInfo, User? deviceUser) cacheTuple;

        public KinoPubSiteProvider(IProviderConfigService providerConfigService) : base(
            providerConfigService,
            new ProviderConfig(Sites.KinoPub,
                enforceDisabled: string.IsNullOrEmpty(Secrets.KinoPubClient)
                    || string.IsNullOrEmpty(Secrets.KinoPubApiKey),
                isEnabledByDefault: false,
                requirements: ProviderRequirements.ProForAny,
                mirrors: new[] { new Uri("https://kino.pub") }))
        {
            deviceInfoSemaphore = new SemaphoreSlim(1);
        }

        public override ValueTask<ItemInfo> EnsureItemAsync(ItemInfo itemInfo, CancellationToken cancellationToken)
        {
            itemInfo.Link = itemInfo.Link?.GetPath().ToUriOrNull();
            return new ValueTask<ItemInfo>(itemInfo);
        }

        public ItemInfo ParseFromJson(JObject obj, Uri domain)
        {
            var id = obj["id"]?.ToString();
            var type = obj["type"]?.ToString();
            var posters = obj["posters"];
            var titles = (obj["title"]?.ToString() ?? "").Split(new[] { " / " }, StringSplitOptions.None);

            return new ItemInfo(Site, id)
            {
                Link = new Uri(domain, "/item/view/" + id),
                Title = titles.FirstOrDefault(),
                Poster = new WebImage
                {
                    [ImageSize.Thumb] = posters?["small"]?.ToUriOrNull(domain),
                    [ImageSize.Preview] = posters?["medium"]?.ToUriOrNull(domain),
                    [ImageSize.Original] = posters?["big"]?.ToUriOrNull(domain)
                },
                Section = KinoPubSections.Find(s => s.Value == type),
                Details =
                {
                    TitleOrigin = titles.Skip(1).LastOrDefault(),
                    Description = obj["plot"]?.ToString(),
                    Year = obj["year"]?.ToIntOrNull()
                }
            };
        }

        internal async Task<JObject?> GetDeviceInfoAsync(CancellationToken cancellationToken)
        {
            if (cacheTuple.deviceUser == CurrentUser)
            {
                return cacheTuple.deviceInfo;
            }

            using var _ = await deviceInfoSemaphore.LockAsync(cancellationToken);
            if (cacheTuple.deviceUser == CurrentUser)
            {
                return cacheTuple.deviceInfo;
            }

            var deviceInfo = await GetAsync("/v1/device/info", null, cancellationToken).ConfigureAwait(false);

            cacheTuple = (deviceInfo, CurrentUser);
            return deviceInfo;
        }

        internal Task<JObject?> GetAsync(string link, Dictionary<string, string>? args = null, CancellationToken cancellationToken = default)
        {
            return SendAsync(HttpMethod.Get, link, CurrentUser?.AccessToken, args, cancellationToken);
        }

        internal Task<JObject?> PostAsync(string link, Dictionary<string, string>? args = null, CancellationToken cancellationToken = default)
        {
            return SendAsync(HttpMethod.Post, link, CurrentUser?.AccessToken, args, cancellationToken);
        }

        internal async Task<JObject?> SendAsync(HttpMethod method, string link, string? accessToken, Dictionary<string, string>? args, CancellationToken cancellationToken)
        {
            var request = HttpClient
                .RequestBuilder(method, new Uri(ApiDomain, link))
                .WithBody(args)
                .WithArguments(args!);

            if (accessToken != null)
            {
                request = request.WithHeader("Authorization", "Bearer " + accessToken);
            }

            var response = await request
                .SendAsync(cancellationToken)
                .ConfigureAwait(false);

            if (response == null)
            {
                return null;
            }

            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                CurrentUser = null;
                return null;
            }

            return await response.AsNewtonsoftJson<JObject>().ConfigureAwait(false);
        }
    }
}
