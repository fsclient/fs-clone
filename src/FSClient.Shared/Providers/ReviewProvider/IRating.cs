namespace FSClient.Shared.Providers
{
    public interface IRating
    {
        double Value { get; }
        bool HasAnyVote { get; }
        bool CanVote { get; }
    }
}
