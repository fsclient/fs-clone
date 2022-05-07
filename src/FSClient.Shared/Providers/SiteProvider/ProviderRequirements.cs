namespace FSClient.Shared.Providers
{
    using System;

    [Flags]
    public enum ProviderRequirements
    {
        None,
        AccountForSpecial = 1,
        AccountForAny = AccountForSpecial << 1,
        ProForSpecial = (AccountForAny << 2) | AccountForSpecial,
        ProForAny = (ProForSpecial << 1) | AccountForAny
    }
}
