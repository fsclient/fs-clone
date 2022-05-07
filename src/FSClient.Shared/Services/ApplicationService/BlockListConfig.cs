namespace FSClient.Shared.Services
{
    using System.Collections.Generic;
    using System.Linq;

    public record BlockListSettings
    {
        public IEnumerable<string> FullBlockedIds { get; init; } = Enumerable.Empty<string>();

        public IEnumerable<string> BlockedIds { get; init; } = Enumerable.Empty<string>();

        public FilterRegexes FilterRegexes { get; init; } = new FilterRegexes();
    }
}
