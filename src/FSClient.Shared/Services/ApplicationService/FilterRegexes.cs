namespace FSClient.Shared.Services
{
    using System.Text.RegularExpressions;

    public record FilterRegexes(
        Regex? SearchInputAdultFilter = null,
        Regex? ItemsByTitleAdultFilter = null);
}
