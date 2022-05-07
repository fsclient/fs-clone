namespace FSClient.Shared.Repositories
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    using FSClient.Shared.Models;

    public interface IHistoryRepository : IRepository<string, HistoryItem>
    {
        ValueTask<float> GetNodePositionById(string id);

        IAsyncEnumerable<HistoryItem> GetOrderedHistory(string? itemInfoKey = null, int? season = null, Range? episode = null);
    }
}
