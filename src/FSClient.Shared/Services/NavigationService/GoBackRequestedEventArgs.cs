namespace FSClient.Shared.Services
{
    using Nito.AsyncEx;

    public class GoBackRequestedEventArgs
    {
        public GoBackRequestedEventArgs(IDeferralSource deferralSource)
        {
            DeferralSource = deferralSource;
        }

        public IDeferralSource DeferralSource { get; }

        public bool Handled { get; set; }
    }
}
