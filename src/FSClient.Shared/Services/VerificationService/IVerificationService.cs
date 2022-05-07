namespace FSClient.Shared.Services
{
    using System.Threading.Tasks;

    /// <summary>
    /// Verification service
    /// </summary>
    public interface IVerificationService
    {
        /// <summary>
        /// Checks is verification available on current device
        /// </summary>
        Task<bool> IsAvailableAsync();

        /// <summary>
        /// Requests system default verification window (like Windows Hello),
        /// which blocks application, while verification is not confirmed
        /// </summary>
        /// <returns>Is success and message tuple</returns>
        Task<(bool success, string message)> RequestVerificationAsync();
    }
}
