namespace FSClient.Providers
{
    using System.Collections.Generic;

    using FSClient.Shared.Models;
    using FSClient.Shared.Providers;

    public class SmotretAnimeSearchProvider : ShikiSearchProvider
    {
        private readonly SmotretAnimeSiteProvider siteProvider;

        public SmotretAnimeSearchProvider(
            SmotretAnimeSiteProvider siteProvider,
            ShikiSiteProvider shikiSiteProvider)
            : base(shikiSiteProvider, null)
        {
            this.siteProvider = siteProvider;
        }

        public override IReadOnlyList<Section> Sections => new List<Section>();

        public override Site Site => siteProvider.Site;

        public override ProviderRequirements ReadRequirements => ProviderRequirements.ProForAny;
    }
}
