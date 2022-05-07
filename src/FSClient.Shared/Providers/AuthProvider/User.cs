namespace FSClient.Shared.Providers
{
    using System;

    using FSClient.Shared.Models;

    public class User : IEquatable<User?>
    {
        public User(Site site, string nickname, Uri? avatar,
            string? accessToken = null, string? refreshToken = null, bool hasProStatus = false)
        {
            Site = site;
            Nickname = nickname;
            Avatar = avatar;
            AccessToken = accessToken;
            RefreshToken = refreshToken;
            HasProStatus = hasProStatus;
        }

        public Site Site { get; }
        public Uri? Avatar { get; }
        public string Nickname { get; }

        public string? AccessToken { get; }
        public string? RefreshToken { get; }

        public bool HasProStatus { get; }

        public bool Equals(User? other)
        {
            return !(other is null)
                && Site == other.Site
                && (AccessToken is null ? Nickname == other.Nickname : AccessToken == other.AccessToken);
        }

        public override bool Equals(object obj)
        {
            return obj is User user && Equals(user);
        }

        public override int GetHashCode()
        {
            return (Site, AccessToken ?? Nickname).GetHashCode();
        }

        public static bool operator ==(User? left, User? right)
        {
            return left is null
                ? right is null
                : left.Equals(right);
        }

        public static bool operator !=(User? left, User? right)
        {
            return !(left == right);
        }
    }
}
