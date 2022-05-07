namespace FSClient.Providers.Test.Stubs
{
    using System.Collections.Generic;
    using System.Threading.Tasks;

    using FSClient.Shared.Services;

    public class AppLanguageServiceStub : IAppLanguageService
    {
        public ValueTask ApplyLanguageAsync(string? name)
        {
            return new ValueTask();
        }

        public IEnumerable<string> GetAvailableLanguages()
        {
            yield return "ru-RU";
        }

        public string GetCurrentLanguage()
        {
            return "ru-RU";
        }
    }
}
