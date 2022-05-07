namespace FSClient.Providers
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    using FSClient.Shared.Models;
    using FSClient.Shared.Providers;

    public class ThePirateBaySearchProvider : ISearchProvider
    {
        private readonly ThePirateBaySiteProvider siteProvider;

        public ThePirateBaySearchProvider(ThePirateBaySiteProvider thePirateBaySiteProvider)
        {
            siteProvider = thePirateBaySiteProvider;
        }

        public IReadOnlyList<Section> Sections { get; } = new List<Section>();

        public Site Site => siteProvider.Site;

        public ProviderRequirements ReadRequirements => ProviderRequirements.None;

        public Task<IEnumerable<ItemInfo>> FindSimilarAsync(ItemInfo original, CancellationToken cancellationToken)
        {
            if (original.Site == Site)
            {
                return Task.FromResult((IEnumerable<ItemInfo>)new[] { original });
            }

            return Task.FromResult(new[] {
                new ItemInfo(Site, $"tpb{original.SiteId}")
                {
                    Title = original.Details.TitleOrigin ?? original.Title,
                    Section = original.Section,
                    Details =
                    {
                        Year = original.Details.Year
                    }
                }
            }.AsEnumerable());
        }

        public IAsyncEnumerable<ItemInfo> GetFullResult(SearchPageFilter filter)
        {
            return AsyncEnumerable.Empty<ItemInfo>();
        }

        public ValueTask<SearchPageParams?> GetSearchPageParamsAsync(Section section, CancellationToken cancellationToken)
        {
            return default;
        }

        public IAsyncEnumerable<ItemInfo> GetShortResult(string request, Section section)
        {
            return AsyncEnumerable.Empty<ItemInfo>();
        }
    }
}
