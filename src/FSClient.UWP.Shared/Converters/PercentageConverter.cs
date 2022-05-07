namespace FSClient.UWP.Shared.Converters
{
    using System;

#if WINUI3
    using Microsoft.UI.Xaml.Data;
#else
    using Windows.UI.Xaml.Data;
#endif

    public class PercentageConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, string? language)
        {
            if (!TryConvert(parameter, out var denominator))
            {
                denominator = 1;
            }

            if (!TryConvert(value, out var convertedValue))
            {
                convertedValue = 0;
            }

            var computed = (convertedValue / denominator) * 100;

            if (targetType.FullName == typeof(string).FullName)
            {
                return (int)computed + "%";
            }

            return computed;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, string? language)
        {
            if (!TryConvert(parameter, out var denominator))
            {
                denominator = 1;
            }

            if (value is string str
                && double.TryParse(str.TrimEnd('%'), out var output))
            {
                output = output * denominator / 100;
            }
            else if (TryConvert(value, out var convertedValue))
            {
                output = convertedValue * denominator / 100;
            }
            else
            {
                output = 0;
            }

            return System.Convert.ChangeType(output, targetType);
        }

        private static bool TryConvert(object? value, out double result)
        {
            try
            {
                if (value == null
                    || !(value is IConvertible))
                {
                    result = default;
                    return false;
                }

                result = System.Convert.ToDouble(value);
                return true;
            }
            catch (Exception ex)
                when (ex is FormatException || ex is InvalidCastException || ex is OverflowException)
            {
                result = default;
                return false;
            }
        }
    }
}
