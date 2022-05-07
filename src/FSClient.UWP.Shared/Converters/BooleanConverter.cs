namespace FSClient.UWP.Shared.Converters
{
    using System;
    using System.Collections;
    using System.Globalization;
    using System.Linq;
    using System.Reflection;

#if WINUI3
    using Microsoft.UI.Xaml;
    using Microsoft.UI.Xaml.Data;
#else
    using Windows.UI.Xaml;
    using Windows.UI.Xaml.Data;
#endif

    using FSClient.Shared.Mvvm;
    using FSClient.Shared.Services;
    public class BooleanConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, string? language)
        {
            var par = (string?)parameter;
            bool ret, inv;
            inv = false;

            if (par != null)
            {
                inv = par.StartsWith("!", StringComparison.Ordinal);
                if (inv)
                {
                    par = par[1..];
                }
            }

            switch (value)
            {
                case bool b:
                    ret = CompareWithParameter(b, par) ?? b;
                    break;
                case int i:
                    ret = CompareWithParameter(i, par) ?? i != 0;
                    break;
                case string s:
                    ret = CompareWithParameter(s, par) ?? !string.IsNullOrEmpty(s);
                    break;
                case Array a:
                    ret = CompareWithParameter(a.Length, par) ?? a.Length > 0;
                    break;
                case IIncrementalCollection incremental:
                    ret = CompareWithParameter(incremental.Count, par) ?? incremental.Count > 0;
                    break;
                case ICollection col:
                    ret = CompareWithParameter(col.Count, par) ?? col.Count > 0;
                    break;
                case ICollectionView colView:
                    ret = CompareWithParameter(colView.Count, par) ?? colView.Count > 0;
                    break;
                case IEnumerable enumerable:
                    var count = enumerable.Cast<object>().Count();
                    ret = CompareWithParameter(count, par) ?? count > 0;
                    break;
                default:
                    ret = CompareWithParameter(value, par) ?? value != null;
                    break;
            }

            ret ^= inv;

            if (targetType == typeof(Visibility))
            {
                return ret ? Visibility.Visible : Visibility.Collapsed;
            }

            return ret;
        }

        private bool? CompareWithParameter<T>(T value, string? par)
        {
            try
            {
                if (string.IsNullOrEmpty(par))
                {
                    return null;
                }

                if (value is IComparable comparable)
                {
                    if (par!.Contains(".."))
                    {
                        var parts = par.Split(new[] {".."}, StringSplitOptions.None);
                        var big = !string.IsNullOrWhiteSpace(parts[0]);
                        var low = !string.IsNullOrWhiteSpace(parts[1]);
                        var ret = true;
                        if (big)
                        {
                            var left = System.Convert.ChangeType(parts[0], value.GetType(),
                                CultureInfo.InvariantCulture);
                            var leftComp = comparable.CompareTo(left);
                            ret &= (leftComp >= 0);
                        }

                        if (low && ret)
                        {
                            var right = System.Convert.ChangeType(parts[1], value.GetType(),
                                CultureInfo.InvariantCulture);
                            var rightComp = comparable.CompareTo(right);
                            ret &= (rightComp <= 0);
                        }

                        return ret;
                    }

                    if (!value.GetType().GetTypeInfo().IsEnum)
                    {
                        var converted = System.Convert.ChangeType(par, value.GetType(), CultureInfo.InvariantCulture);
                        var result = comparable.CompareTo(converted);

                        return result == 0;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.LogWarning(ex);
            }

            return value?.ToString() == par;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, string? language)
        {
            if (targetType != typeof(bool))
            {
                return value;
            }

            var inv = parameter?.ToString() == "!";
            bool retValue;

            switch (value)
            {
                case bool b:
                    retValue = b;
                    break;
                case Visibility v:
                    retValue = v == Visibility.Visible;
                    break;
                default:
                    return value;
            }

            return retValue ^ inv;
        }
    }
}
