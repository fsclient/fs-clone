namespace FSClient.Shared.Services
{
    /// <summary>
    /// Video opening in third party player result
    /// </summary>
    public enum ThirdPartyPlayerOpenResult
    {
        /// <summary>
        /// Opened with success result
        /// </summary>
        Success = 1,

        /// <summary>
        /// Opened with success result, but some information was missed
        /// </summary>
        SuccessWithMissedInfo,

        /// <summary>
        /// Player is not available
        /// </summary>
        NotAvailable,

        /// <summary>
        /// Opened with unknown error
        /// </summary>
        FailedUnknown
    }
}
