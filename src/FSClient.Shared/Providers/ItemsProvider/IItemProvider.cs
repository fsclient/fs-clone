namespace FSClient.Shared.Providers
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    using FSClient.Shared.Models;

    /// <summary>
    /// Provider for item lists functionality.
    /// </summary>
    public interface IItemProvider : IProviderWithRequirements
    {
        /// <summary>
        /// List of <see cref="Section"/> that is supported for item lists.
        /// <see cref="Section.Any"/> should be included in provider, if it is supported by it.
        /// </summary>
        IReadOnlyList<Section> Sections { get; }

        /// <summary>
        /// Returns true, if provider supports special home page.
        /// </summary>
        bool HasHomePage { get; }

        /// <summary>
        /// Gets <see cref="SectionPageParams"/> that describes possible item lists filters and sort types per specific <see cref="Section"/>.
        /// <para>Different sections are allowed to have different params sets.</para>
        /// </summary>
        /// <param name="section">Specific <see cref="Section"/> or <see cref="Section.Any"/>.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Returns async task of <see cref="SectionPageParams"/> or null, if <see cref="Section"/> is not supported.</returns>
        ValueTask<SectionPageParams?> GetSectionPageParamsAsync(
            Section section,
            CancellationToken cancellationToken);

        /// <summary>
        /// Gets <see cref="SectionPageParams"/> for specific <see cref="TitledTag"/> that describes possible item lists filters and sort types per specific <see cref="Section"/>.
        /// <para>Different sections are allowed to have different params sets.</para>
        /// <para>Provider can provider different set of params for specific <see cref="TitledTag"/>, that can't be achieved with <see cref="GetSectionPageParamsAsync(Section, CancellationToken)"/></para>
        /// </summary>
        /// <param name="section">Specific <see cref="Section"/> or <see cref="Section.Any"/>.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Returns async task of <see cref="SectionPageParams"/> or null, if <see cref="Section"/> is not supported.</returns>
        ValueTask<SectionPageParams?> GetSectionPageParamsForTagAsync(
            Section section, TitledTag titledTag,
            CancellationToken cancellationToken);

        /// <summary>
        /// Gets full items list enumerable by specific <see cref="SectionPageFilter"/>.
        /// </summary>
        /// <param name="filter"><see cref="SectionPageFilter"/> that is build on top of <see cref="SectionPageParams"/> from <see cref="GetSectionPageParamsAsync(Section, CancellationToken)"/> or <see cref="GetSectionPageParamsForTagAsync(Section, TitledTag, CancellationToken)"/>.</param>
        /// <returns>Returns async enumerable of <see cref="ItemInfo"/>.</returns>
        IAsyncEnumerable<ItemInfo> GetFullResult(
            SectionPageFilter filter);

        /// <summary>
        /// Gets special <see cref="HomePageModel"/> if provider supports it.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns><see cref="HomePageModel"/> instance of null, if it isn't supported.</returns>
        Task<HomePageModel?> GetHomePageModelAsync(
            CancellationToken cancellationToken);
    }
}
