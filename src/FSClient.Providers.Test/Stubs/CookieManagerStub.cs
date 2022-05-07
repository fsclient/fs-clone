namespace FSClient.Providers.Test.Stubs
{
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;

    using FSClient.Shared.Managers;
    using FSClient.Shared.Models;

    public class CookieManagerStub : ICookieManager
    {
        private readonly ConcurrentDictionary<Site, IEnumerable<Cookie>> cookiesStore;

        public CookieManagerStub(IDictionary<Site, IEnumerable<Cookie>>? cookiesStore = null)
        {
            this.cookiesStore = cookiesStore != null ? new ConcurrentDictionary<Site, IEnumerable<Cookie>>()
                : new ConcurrentDictionary<Site, IEnumerable<Cookie>>();
        }

        public IReadOnlyList<string> DeleteAll(Site site)
        {
            if (cookiesStore.TryRemove(site, out var cooks))
            {
                return cooks.Select(c => c.Name).ToList();
            }
            else
            {
                return new List<string>();
            }
        }

        public IReadOnlyList<Cookie> Load(Site site)
        {
            if (cookiesStore.TryGetValue(site, out var cooks))
            {
                return cooks.ToList();
            }
            else
            {
                return new List<Cookie>();
            }
        }

        public void Save(Site site, IEnumerable<Cookie> cookies)
        {
            cookiesStore.AddOrUpdate(site, cookies, (_, oldCooks) => oldCooks.Concat(cookies));
        }
    }
}
