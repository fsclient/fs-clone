namespace FSClient.Shared.Services.Specifications
{
    using System;
    using System.Linq;

    using FSClient.Shared.Helpers;
    using FSClient.Shared.Models;

    public class ItemInfoTreeNodeSpecification
    {
        private readonly (string text, bool isConcrete)[] filterStrings;

        public ItemInfoTreeNodeSpecification(string? searchString)
        {
            if (string.IsNullOrWhiteSpace(searchString))
            {
                filterStrings = Array.Empty<(string text, bool isConcrete)>();
            }
            else
            {
                filterStrings = SearchStringSpecificationHelper.PrepareFilters(searchString!);
            }
        }

        public bool IsSatisfiedBy(ItemInfo candidate)
        {
            foreach (var filterPair in filterStrings)
            {
                var (filter, isConcreteFilter) = filterPair;

                var result = candidate.Details.Titles.Any(title => SearchStringSpecificationHelper.CheckPlainString(title, filter, isConcreteFilter))
                    || SearchStringSpecificationHelper.CheckPlainString(candidate.Details.TitleOrigin, filter, isConcreteFilter)
                    || SearchStringSpecificationHelper.CheckPlainString(candidate.Site.Title, filter, isConcreteFilter)
                    || SearchStringSpecificationHelper.CheckPlainString(candidate.Section.Title, filter, isConcreteFilter)
                    || SearchStringSpecificationHelper.CheckPlainString(candidate.Details.Quality, filter, isConcreteFilter);

                if (!result)
                {
                    return false;
                }
            }
            return true;
        }
    }
}
