namespace FSClient.Shared.Services.Specifications
{
    using System;

    using FSClient.Shared.Helpers;
    using FSClient.Shared.Models;

    public class SearchTreeNodeSpecification
    {
        private readonly (string text, bool isConcrete)[] filterStrings;

        public SearchTreeNodeSpecification(string? searchString)
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

        public bool IsSatisfiedBy(ITreeNode candidate)
        {
            var fileCandidate = candidate as File;
            var folderCandidate = candidate as IFolderTreeNode;
            var candidateAsToString = candidate.ToString()?.Replace(candidate.Id, string.Empty);

            foreach (var filterPair in filterStrings)
            {
                var (filter, isConcreteFilter) = filterPair;

                var result = SearchStringSpecificationHelper.CheckPlainString(candidate.Group, filter, isConcreteFilter)
                    || SearchStringSpecificationHelper.CheckPlainString(candidate.Title, filter, isConcreteFilter)
                    || SearchStringSpecificationHelper.CheckPlainString(candidateAsToString, filter, isConcreteFilter)
                    || (folderCandidate != null && SearchStringSpecificationHelper.CheckPlainString(folderCandidate.Details, filter, isConcreteFilter))
                    || (fileCandidate != null && SearchStringSpecificationHelper.CheckPlainString(fileCandidate.Quality, filter, isConcreteFilter));

                if (!result)
                {
                    return false;
                }
            }
            return true;
        }
    }
}
