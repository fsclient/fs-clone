namespace FSClient.UWP.Shared.Converters
{
    using System;
    using System.Reflection;

#if WINUI3
    using Microsoft.UI.Xaml.Data;
#else
    using Windows.UI.Xaml.Data;
#endif

    public class BindToObjectConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, string? language)
        {
            return value;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, string? language)
        {
            if (value == null
                && targetType.GetTypeInfo().IsValueType)
            {
                return Activator.CreateInstance(targetType);
            }

            return value;
        }
    }
}
