namespace FSClient.Shared.Services
{
    using System.Collections.Generic;
    using System.Threading.Tasks;

    public interface ISystemFeaturesService
    {
        Task<IEnumerable<MissedSystemFeature>> GetMissedFeaturesAsync();
    }
}
