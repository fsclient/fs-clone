namespace FSClient.Data.Context
{
    using System;
    using System.IO;
    using System.Threading.Tasks;

    using FSClient.Shared.Helpers;
    using FSClient.Shared.Models;
    using FSClient.Shared.Repositories;

    using LiteDB;

    /// <summary>
    /// Wraps LiteDatabase object with specified params and file
    /// Can be registered in IoC as singleton to reuse same instance of LiteDatabase
    /// </summary>
    public sealed class LiteDatabaseContext : IDatabaseContext
    {
        private const string LiteDbFileName = "Database.db";

        public LiteDatabaseContext(string? folderPath, bool isReadOnly)
        {
            var fileName = ":memory:";
            if (folderPath != null)
            {
                fileName = Path.Combine(folderPath, LiteDbFileName);
            }

            Database = new LiteDatabase(new ConnectionString
            {
                Filename = fileName,
                Connection = ConnectionType.Direct,
                ReadOnly = isReadOnly
            });
            RegisterTypes(Database);
            RegisterMapping(Database);
        }

        /// <summary>
        /// Wrapped and configured instance of LiteDB <see cref="LiteDatabase"/>
        /// </summary>
        public ILiteDatabase Database { get; }

        /// <inheritdoc/>
        public void Dispose()
        {
            Database.Dispose();
        }

        /// <inheritdoc/>
        public ValueTask DropAsync()
        {
            foreach (var collectionName in Database.GetCollectionNames())
            {
                Database.DropCollection(collectionName);
            }
            return new ValueTask();
        }

        /// <inheritdoc/>
        public ValueTask CheckpointAsync()
        {
            Database.Checkpoint();
            return new ValueTask();
        }

        private static void RegisterTypes(ILiteDatabase liteDatabase)
        {
            liteDatabase.Mapper.RegisterType(
                serialize: dateTimeOffset => new BsonValue(dateTimeOffset.UtcDateTime),
                deserialize: bson => bson.IsDateTime ? bson.AsDateTime : DateTimeOffset.FromUnixTimeSeconds(bson.AsInt64).ToLocalTime());

            liteDatabase.Mapper.RegisterType(
                serialize: uri => uri.ToString(),
                deserialize: value => new Uri(value.AsString, UriKind.RelativeOrAbsolute));

            liteDatabase.Mapper.RegisterType(
                serialize: site => site.Value,
                deserialize: value => Site.Parse(value));

            liteDatabase.Mapper.RegisterType(
                serialize: section => section.Modifier.ToString(),
                deserialize: value => Enum.TryParse<SectionModifiers>(value.AsString, out var modifier)
                    ? Section.CreateDefault(modifier) : Section.Any);

            liteDatabase.Mapper.RegisterType(
                serialize: image => image[ImageSize.Preview]?.AbsoluteUri,
                deserialize: value => new WebImage { [ImageSize.Preview] = new Uri(value.AsString) });

            liteDatabase.Mapper.RegisterType(
                serialize: range => range.ToFormattedString(),
                deserialize: value => RangeExtensions.TryParse(value, out var range) ? range : new Range());
        }

        private static void RegisterMapping(ILiteDatabase liteDatabase)
        {
            liteDatabase.Mapper.Entity<ItemInfo>()
                .Ignore(i => i.Details)
                .Id(i => i.Key, false);

            liteDatabase.Mapper.Entity<HistoryItem>()
                .Ignore(e => e.AutoStart)
                .DbRef(e => e.ItemInfo)
                .Id(e => e.Key, false);

            liteDatabase.Mapper.Entity<FavoriteItem>()
                .DbRef(e => e.ItemInfo)
                .Id(e => e.Key, false);

            liteDatabase.Mapper.Entity<DownloadEntity>()
                .Id(i => i.OperationId, false);

            liteDatabase.Mapper.Entity<TorrServerEntity>()
                .Id(i => i.TorrServerHash, false);

            var historyItemCollection = liteDatabase.GetCollection<HistoryItem>();
            historyItemCollection.EnsureIndex(h => h.AddTime);
            historyItemCollection.EnsureIndex(BsonExpression.Create($"$.{nameof(HistoryItem.ItemInfo)}.$id"));
            historyItemCollection.EnsureIndex(h => h.Season);
            historyItemCollection.EnsureIndex(h => h.Episode);

            var favoriteItemCollection = liteDatabase.GetCollection<FavoriteItem>();
            favoriteItemCollection.EnsureIndex(h => h.ListKind);
            favoriteItemCollection.EnsureIndex(BsonExpression.Create($"$.{nameof(FavoriteItem.ItemInfo)}.$id"));
        }
    }
}
