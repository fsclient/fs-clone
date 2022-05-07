namespace FSClient.Shared.Services
{
    using System.Collections.Generic;
    using System.Threading.Tasks;

    /// <summary>
    /// Application language service.
    /// </summary>
    public interface IAppLanguageService
    {
        /// <summary>
        /// Get's two-letter language codes available for application.
        /// </summary>
        /// <returns></returns>
        IEnumerable<string> GetAvailableLanguages();

        /// <summary>
        /// Get's two-letter language.
        /// </summary>
        string GetCurrentLanguage();

        /// <summary>
        /// Applies language.
        /// </summary>
        /// <param name="name">Two-letter language code. If null, default language will be used.</param>
        ValueTask ApplyLanguageAsync(string? name);
    }
}
