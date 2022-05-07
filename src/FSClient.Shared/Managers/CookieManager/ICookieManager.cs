namespace FSClient.Shared.Managers
{
    using System.Collections.Generic;
    using System.Net;

    using FSClient.Shared.Models;

    /// <summary>
    /// Security cookies manager.
    /// </summary>
    public interface ICookieManager
    {
        /// <summary>
        /// Save cookies with relation to concrete site.
        /// </summary>
        /// <param name="site">Site which cookies will be saved.</param>
        /// <param name="cookies">Cookies to save.</param>
        void Save(Site site, IEnumerable<Cookie> cookies);

        /// <summary>
        /// Delete all cookies for concrete site and return their names.
        /// </summary>
        /// <param name="site">Site which cookies should be deleted.</param>
        /// <returns>List of deleted <see cref="Cookie.Name"/>.</returns>
        IReadOnlyList<string> DeleteAll(Site site);

        /// <summary>
        /// Load and return all cookies for concrete site.
        /// </summary>
        /// <param name="site">Site which cookies should be loaded.</param>
        /// <returns>List of <see cref="Cookie"/>.</returns>
        IReadOnlyList<Cookie> Load(Site site);
    }
}
