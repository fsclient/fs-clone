namespace FSClient.UWP.Shared.Converters
{
    using System;

#if WINUI3
    using Microsoft.UI.Xaml.Data;
#else
    using Windows.UI.Xaml.Data;
#endif

    using FSClient.Shared.Helpers;

    public class ObjectToDescriptionConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, string? language)
        {
            if (value is Enum en)
            {
                return en.GetDisplayDescription();
            }

            return null;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, string? language)
        {
            throw new NotSupportedException();
        }
    }
}
