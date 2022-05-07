namespace FSClient.Shared.Services
{
    using System;

    public class MissedSystemFeature
    {
        public MissedSystemFeature(string localizedFeatureName, Uri featureInstallLink)
        {
            LocalizedFeatureName = localizedFeatureName;
            FeatureInstallLink = featureInstallLink;
        }


        public string LocalizedFeatureName { get; }

        public Uri FeatureInstallLink { get; }
    }
}
