namespace FSClient.Shared.Providers
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    using FSClient.Shared.Models;

    /// <summary>
    /// Provider for search functionality.
    /// </summary>
    public interface ISearchProvider : IProviderWithRequirements
    {
        /// <summary>
        /// List of <see cref="Section"/> that is supported for search.
        /// <see cref="Section.Any"/> should be included in provider, if it is supported by it.
        /// </summary>
        IReadOnlyList<Section> Sections { get; }

        /// <summary>
        /// Gets <see cref="SearchPageParams"/> that describes possible search filters and sort types per specific <see cref="Section"/>.
        /// <para>Different sections are allowed to have different params sets.</para>
        /// </summary>
        /// <param name="section">Specific <see cref="Section"/> or <see cref="Section.Any"/>.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Returns async task of <see cref="SearchPageParams"/> or null, if <see cref="Section"/> is not supported.</returns>
        ValueTask<SearchPageParams?> GetSearchPageParamsAsync(
            Section section,
            CancellationToken cancellationToken);

        /// <summary>
        /// Gets full search result enumerable by specific <see cref="SearchPageFilter"/>.
        /// </summary>
        /// <param name="filter"><see cref="SearchPageFilter"/> that is build on top of <see cref="SearchPageParams"/> from <see cref="GetSearchPageParamsAsync(Section, CancellationToken)"/></param>
        /// <returns>Returns async enumerable of <see cref="ItemInfo"/>.</returns>
        IAsyncEnumerable<ItemInfo> GetFullResult(
            SearchPageFilter filter);

        /// <summary>
        /// Gets short (fast) search results without any other filters.
        /// </summary>
        /// <param name="request">Search request.</param>
        /// <param name="section">Section to filter.</param>
        /// <returns>Returns async enumerable of <see cref="ItemInfo"/>.</returns>
        IAsyncEnumerable<ItemInfo> GetShortResult(
            string request,
            Section section);

        /// <summary>
        /// Gets enumerable of equivalent items from this provider.
        /// </summary>
        /// <param name="original"><see cref="ItemInfo"/> from any provider. Also can be from the same provider.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Returns async task of <see cref="ItemInfo"/> enumerable.</returns>
        Task<IEnumerable<ItemInfo>> FindSimilarAsync(
            ItemInfo original,
            CancellationToken cancellationToken);
    }
}
