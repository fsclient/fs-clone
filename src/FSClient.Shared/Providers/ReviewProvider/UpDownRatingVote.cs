namespace FSClient.Shared.Providers
{
    public record UpDownRatingVote(bool? UpVoted, bool? DownVoted) : IRatingVote;
}
