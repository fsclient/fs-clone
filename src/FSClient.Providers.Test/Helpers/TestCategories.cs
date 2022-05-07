namespace FSClient.Providers.Test.Helpers
{
    using FSClient.Shared.Providers;

    public static class TestCategories
    {
        public const string SearchProvider = nameof(ISearchProvider);

        public const string ReviewProvider = nameof(IReviewProvider);

        public const string ItemInfoProvider = nameof(IItemInfoProvider);

        public const string ItemProvider = nameof(IItemProvider);

        public const string FileProvider = nameof(IFileProvider);

        public const string AuthProvider = nameof(IAuthProvider);

        public const string FavoriteProvider = nameof(IFavoriteProvider);

        public const string PlayerParseProvider = nameof(IPlayerParseProvider);
    }
}
