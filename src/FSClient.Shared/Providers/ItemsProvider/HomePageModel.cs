namespace FSClient.Shared.Providers
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    using FSClient.Shared.Models;

    public record HomePageModel(Site Site, string Caption)
    {
        public string? TopItemsCaption { get; set; }

        public IEnumerable<ItemInfo> TopItems { get; set; } = Array.Empty<ItemInfo>();

        public IEnumerable<IGrouping<string, ItemInfo>> HomeItems { get; set; } = Array.Empty<IGrouping<string, ItemInfo>>();
    }
}
