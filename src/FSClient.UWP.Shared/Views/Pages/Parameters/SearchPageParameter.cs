namespace FSClient.UWP.Shared.Views.Pages.Parameters
{
    using FSClient.Shared.Models;

    public class SearchPageParameter
    {
        public SearchPageParameter(string searchRequest, Site site = default)
        {
            SearchRequest = searchRequest;
            Site = site;
        }

        public string SearchRequest { get; }

        public Site Site { get; }
    }
}
