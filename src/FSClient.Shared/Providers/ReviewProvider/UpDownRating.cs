namespace FSClient.Shared.Providers
{
    public record UpDownRating(int UpCount, int DownCount,
        bool UpVoted, bool DownVoted,
        bool DownVoteVisible = true,
        bool CanVote = false) : IRating
    {
        public double Value => UpCount + DownCount is var total && total == 0 ? 0.5f : (double)UpCount / total;

        public bool HasAnyVote => UpCount > 0 || DownCount > 0;

        public override string ToString()
        {
            return Value.ToString("0.00");
        }
    }
}
