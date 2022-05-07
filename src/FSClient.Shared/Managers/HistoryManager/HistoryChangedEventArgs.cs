namespace FSClient.Shared.Managers
{
    using System;

    public class HistoryChangedEventArgs : EventArgs
    {
        public HistoryChangedEventArgs(HistoryItemChangedReason reason)
        {
            Reason = reason;
        }

        public HistoryItemChangedReason Reason { get; }
    }
}
