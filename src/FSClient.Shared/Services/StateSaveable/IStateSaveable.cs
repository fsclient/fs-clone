namespace FSClient.Shared.Services
{
    using System;

    public interface IStateSaveable
    {
        Uri? SaveStateToUri();
    }
}
