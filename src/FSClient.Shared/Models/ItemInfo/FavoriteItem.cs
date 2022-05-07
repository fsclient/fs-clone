namespace FSClient.Shared.Models
{
    using System;
    using System.Collections.Generic;

    using FSClient.Shared.Providers;
    using FSClient.Shared.Services;

    public class FavoriteItem : IEquatable<FavoriteItem>, ILogState
    {
#nullable disable
        private FavoriteItem()
#nullable restore
        {

        }

        public FavoriteItem(ItemInfo itemInfo, FavoriteListKind listKind)
        {
            ItemInfo = itemInfo ?? throw new ArgumentNullException(nameof(itemInfo));
            ListKind = listKind;
            if (listKind == FavoriteListKind.None)
            {
                throw new ArgumentOutOfRangeException(nameof(listKind));
            }
        }

        public ItemInfo ItemInfo { get; private set; }

        public FavoriteListKind ListKind { get; private set; }

        public string Key => $"{ItemInfo.Key}-{ListKind.ToString().Replace(',', '_')}";

        public override string ToString()
        {
            return $"{Key}: {ItemInfo?.Title}";
        }

        public override bool Equals(object obj)
        {
            return obj is FavoriteItem favoriteItem && Equals(favoriteItem);
        }

        public bool Equals(FavoriteItem? favoriteItem)
        {
            return favoriteItem?.ItemInfo?.Equals(ItemInfo) == true
                && favoriteItem.ListKind == ListKind;
        }

        public override int GetHashCode()
        {
            return (ItemInfo, ListKind).GetHashCode();
        }

        public IDictionary<string, string> GetLogProperties(bool verbose)
        {
            return ItemInfo.GetLogProperties(verbose);
        }

        public static bool operator ==(FavoriteItem? left, FavoriteItem? right)
        {
            return left?.Equals(right) ?? right is null;
        }

        public static bool operator !=(FavoriteItem? left, FavoriteItem? right)
        {
            return !(left == right);
        }
    }
}
