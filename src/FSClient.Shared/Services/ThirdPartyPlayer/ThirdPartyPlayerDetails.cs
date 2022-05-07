namespace FSClient.Shared.Services
{
    /// <summary>
    /// Third-party player detailed info
    /// </summary>
    public class ThirdPartyPlayerDetails
    {
        public ThirdPartyPlayerDetails(string key, string name)
        {
            Key = key;
            Name = name;
        }

        /// <summary>
        /// Player key
        /// </summary>
        public string Key { get; }

        /// <summary>
        /// Player name
        /// </summary>
        public string Name { get; }
    }
}
