namespace FSClient.Shared.Services
{
    using System.Collections.Generic;

    public interface ILogState
    {
        IDictionary<string, string> GetLogProperties(bool verbose);
    }
}
