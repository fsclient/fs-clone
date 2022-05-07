namespace FSClient.Providers
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;

    using FSClient.Shared;
    using FSClient.Shared.Models;
    using FSClient.Shared.Services;

    public class VideoCDNSiteProvider : BasePlayerJsSiteProvider
    {
        public VideoCDNSiteProvider(IProviderConfigService providerConfigService) : base(
            providerConfigService,
            new ProviderConfig(Sites.VideoCDN,
                enforceDisabled: string.IsNullOrEmpty(Secrets.VideoCDNApiKey),
                mirrors: new[] { new Uri("http://1.svetacdn.in") }))
        {
        }

        public IReadOnlyList<Section> Sections => new List<Section>
        {
            Section.Any,
            new Section("movies", Section.GetTitleByModifier(SectionModifiers.Film))
            { Modifier = SectionModifiers.Film },
            new Section("tv-series", Section.GetTitleByModifier(SectionModifiers.Serial))
            { Modifier = SectionModifiers.Serial },
            new Section("animes", Section.GetTitleByModifier(SectionModifiers.Anime | SectionModifiers.Film))
            { Modifier = SectionModifiers.Anime | SectionModifiers.Film },
            new Section("anime-tv", Section.GetTitleByModifier(SectionModifiers.Anime | SectionModifiers.Serial))
            { Modifier = SectionModifiers.Anime | SectionModifiers.Film },
            new Section("show-tv-series", Section.GetTitleByModifier(SectionModifiers.TVShow))
            { Modifier = SectionModifiers.TVShow }
        };

        protected override ValueTask<bool> IsValidMirrorResponse(HttpResponseMessage responseMessage, CancellationToken cancellationToken)
        {
            return new ValueTask<bool>(responseMessage.IsSuccessStatusCode
                || responseMessage.StatusCode == HttpStatusCode.NotFound);
        }
    }
}
