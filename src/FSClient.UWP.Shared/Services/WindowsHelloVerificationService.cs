namespace FSClient.UWP.Shared.Services
{
    using System;
    using System.Threading.Tasks;

    using Windows.Security.Credentials.UI;

    using FSClient.Localization.Resources;
    using FSClient.Shared.Services;
    using FSClient.UWP.Shared.Helpers;

    using Microsoft.Extensions.Logging;

    /// <inheritdoc/>
    public class WindowsHelloVerificationService : IVerificationService
    {
        private readonly ILogger logger;

        public WindowsHelloVerificationService(ILogger logger)
        {
            this.logger = logger;
        }

        /// <inheritdoc/>
        public Task<bool> IsAvailableAsync()
        {
            return DispatcherHelper.GetForCurrentOrMainView().CheckBeginInvokeOnUI(async () =>
            {
                try
                {
                    var result = await UserConsentVerifier.CheckAvailabilityAsync();

                    return result == UserConsentVerifierAvailability.Available;
                }
                catch (Exception ex)
                {
                    logger?.LogError(ex);

                    return false;
                }
            });
        }

        /// <inheritdoc/>
        public Task<(bool, string)> RequestVerificationAsync()
        {
            return DispatcherHelper.GetForCurrentOrMainView().CheckBeginInvokeOnUI(async () =>
            {
                try
                {
                    var result =
                        await UserConsentVerifier.RequestVerificationAsync(Strings
                            .WindowsHelloVerificationService_RequestVerificationHeader);
                    var success = result == UserConsentVerificationResult.Verified;

                    return (success, result.ToString());
                }
                catch (Exception ex)
                {
                    logger?.LogError(ex);

                    return (false, ex.Message);
                }
            });
        }
    }
}
