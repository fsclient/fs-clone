namespace FSClient.Shared.Services.Specifications
{
    using System;

    using FSClient.Shared.Helpers;
    using FSClient.Shared.Models;

    public class DownloadFileSpecification
    {
        private readonly (string text, bool isConcrete)[] filterStrings;

        public DownloadFileSpecification(string? searchString)
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

        public bool IsSatisfiedBy(DownloadFile candidate)
        {
            foreach (var filterPair in filterStrings)
            {
                var (filter, isConcreteFilter) = filterPair;

                var result = candidate.File == null
                    ? SearchStringSpecificationHelper.CheckPlainString(candidate.FileName, filter, isConcreteFilter)
                    : SearchStringSpecificationHelper.CheckPlainString(candidate.File.Path, filter, isConcreteFilter);

                if (!result)
                {
                    return false;
                }
            }
            return true;
        }
    }
}
