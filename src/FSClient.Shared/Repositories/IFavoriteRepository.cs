namespace FSClient.Shared.Repositories
{
    using System.Collections.Generic;

    using FSClient.Shared.Models;
    using FSClient.Shared.Providers;

    public interface IFavoriteRepository : IRepository<string, FavoriteItem>
    {
        IAsyncEnumerable<FavoriteItem> GetAllByFavoriteListKind(FavoriteListKind listKind);

        IAsyncEnumerable<FavoriteItem> GetFavoritesByItems(IEnumerable<string> itemIds);
    }
}
