namespace FSClient.Shared.Providers
{
    using System;

    public class DeviceFlowDialogInput
    {
        public DeviceFlowDialogInput(string code, DateTimeOffset expiresAt, Uri verificationUri)
        {
            Code = code;
            ExpiresAt = expiresAt;
            VerificationUri = verificationUri;
        }

        public string Code { get; }

        public DateTimeOffset ExpiresAt { get; }

        public Uri VerificationUri { get; }
    }
}
