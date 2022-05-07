namespace FSClient.Providers
{
    using FSClient.Shared.Helpers;

    internal class YohohoSearchResult
    {
        protected YohohoSearchResult()
        {

        }

        public YohohoSearchResult(
            string iframe,
            string translate,
            string quality)
        {
            IFrame = iframe?.NotEmptyOrNull();
            Translate = translate?.NotEmptyOrNull();
            Quality = quality?.NotEmptyOrNull();
        }

        public string? IFrame { get; }

        public string? Translate { get; }

        public string? Quality { get; }
    }
}
