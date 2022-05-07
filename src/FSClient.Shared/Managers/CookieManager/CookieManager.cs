namespace FSClient.Shared.Managers
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;

    using FSClient.Shared.Helpers.Web;
    using FSClient.Shared.Models;
    using FSClient.Shared.Services;

    /// <inheritdoc/>
    public sealed class CookieManager : ICookieManager
    {
        private const string CookiesResourceName = "FS_Cookies";

        private readonly ISettingService settingService;

        public CookieManager(ISettingService settingService)
        {
            this.settingService = settingService;
        }

        /// <inheritdoc/>
        public void Save(Site site, IEnumerable<Cookie> cookies)
        {
            var cookieString = cookies.ToCookieString();

            settingService.SetSetting(
                CookiesResourceName,
                site.Value,
                cookieString,
                SettingStrategy.Secure);

            settingService.SetSetting(
                CookiesResourceName,
                site.Value,
                cookieString,
                SettingStrategy.Local);
        }

        /// <inheritdoc/>
        public IReadOnlyList<string> DeleteAll(Site site)
        {
            var credential = settingService.GetSetting(CookiesResourceName, site.Value, (string?)null, SettingStrategy.Secure)
                ?? settingService.GetSetting(CookiesResourceName, site.Value, string.Empty);

            var deletedNames = CookieHelper
                .FromCookieString(credential)
                .Select(c => c.Name)
                .ToList();

            settingService.DeleteSetting(
                CookiesResourceName,
                site.Value,
                SettingStrategy.Secure);
            settingService.DeleteSetting(
                CookiesResourceName,
                site.Value,
                SettingStrategy.Local);
            settingService.DeleteSetting(
                CookiesResourceName,
                site.Value,
                SettingStrategy.Roaming);

            return deletedNames;
        }

        /// <inheritdoc/>
        public IReadOnlyList<Cookie> Load(Site site)
        {
            var credential = settingService.GetSetting(CookiesResourceName, site.Value, (string?)null, SettingStrategy.Secure)
                ?? settingService.GetSetting(CookiesResourceName, site.Value, string.Empty, SettingStrategy.Local);

            return CookieHelper.FromCookieString(credential).ToList();
        }
    }
}
