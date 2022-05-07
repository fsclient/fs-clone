namespace FSClient.UWP.Shared.Converters
{
    using System;

#if WINUI3
    using Microsoft.UI.Xaml.Data;
#else
    using Windows.UI.Xaml.Data;
#endif

    public class TypeConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, string? language)
        {
            return value == null ? null : System.Convert.ChangeType(value, targetType);
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, string? language)
        {
            return value == null ? null : System.Convert.ChangeType(value, targetType);
        }
    }
}
