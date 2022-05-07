namespace FSClient.Shared.Providers
{
    /// <summary>
    /// Provider internface with requirements contract.
    /// </summary>
    public interface IProviderWithRequirements : IProvider
    {
        /// <summary>
        /// Provider requirements that should be checked before any request.
        /// </summary>
        ProviderRequirements ReadRequirements { get; }
    }
}
