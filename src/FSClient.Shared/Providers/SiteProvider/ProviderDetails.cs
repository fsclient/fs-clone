namespace FSClient.Shared.Providers
{
    /// <summary>
    /// Provider details.
    /// </summary>
    public class ProviderDetails
    {
        public ProviderDetails(
            ProviderRequirements requirement = ProviderRequirements.None,
            int priority = 0)
        {
            Requirement = requirement;
            Priority = priority;
        }

        /// <summary>
        /// Provider special requirements.
        /// </summary>
        public ProviderRequirements Requirement { get; }

        /// <summary>
        /// Provider priority, ordered by descending.
        /// </summary>
        public int Priority { get; }
    }
}
