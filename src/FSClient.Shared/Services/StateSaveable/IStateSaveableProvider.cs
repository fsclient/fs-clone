namespace FSClient.Shared.Services
{
    public interface IStateSaveableProvider
    {
        IStateSaveable StateSaveable { get; }
    }
}
