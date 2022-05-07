namespace FSClient.UWP.Shared.Converters
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;

    using Windows.UI.Xaml.Data;

    using FSClient.Localization.Resources;
    using FSClient.Shared.Services;
    using FSClient.Shared.Helpers;

    public class LocalizationConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, string language)
        {
            if (parameter is not string resourceKey)
            {
                return null;
            }

            var localized = GetLocalizedString(resourceKey);
            return value switch
            {
                null => localized,
                Range range => string.Format(localized, range.ToFormattedString()),
                _ => string.Format(localized, value)
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }

        private static string GetLocalizedString(string resourceId)
        {
            try
            {
                var value = Strings.ResourceManager.GetString(resourceId, CultureInfo.CurrentUICulture);

                if (string.IsNullOrEmpty(value))
                {
                    throw new KeyNotFoundException($"x:Bind resources:Strings.with Key=\"{resourceId}\" was not found.");
                }

                return value;
            }
            catch (Exception ex)
            {
                Logger.Instance.LogError(ex);
            }
            return "[" + resourceId + "]";
        }
    }
}
