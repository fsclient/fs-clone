namespace FSClient.Shared.Providers
{
    using System;

    using FSClient.Shared.Models;

    public record Review(Site Site, string Id,
        string? Description,
        string? Reviewer,
        bool? IsUserReview = null,
        Uri? Avatar = null,
        DateTime? Date = null);
}
