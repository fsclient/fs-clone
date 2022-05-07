namespace FSClient.Shared.Providers
{
    public record NumberBasedRating(double BaseNumber,
        double Value,
        int? VotesCount = null,
        double? UserVote = null) : IRating
    {
        public bool Voted => UserVote.HasValue;

        public bool HasAnyVote => VotesCount > 0;

        // TODO NotImplemented on View layer.
        public bool CanVote => false;
    }
}
