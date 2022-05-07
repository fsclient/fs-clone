namespace FSClient.Shared.Managers
{
    using System;

    using FSClient.Shared.Providers;

    public class FavoriteChangedEventArgs : EventArgs
    {
        public FavoriteChangedEventArgs(
            FavoriteItemChangedReason reason,
            FavoriteListKind favoriteListKind)
        {
            Reason = reason;
            FavoriteListKind = favoriteListKind;
        }

        public FavoriteItemChangedReason Reason { get; }

        public FavoriteListKind FavoriteListKind { get; }
    }
}
