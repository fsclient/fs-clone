namespace FSClient.Shared.Helpers
{
    using System;
    using System.Diagnostics.CodeAnalysis;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Reflection;

    using FSClient.Shared.Services;

    public static class ObjectHelper
    {
        [return: NotNullIfNotNull("otherwise")]
        public static T SafeCast<T>(object? value, T? otherwise = default)
        {
            if (TrySafeCast<T>(value, out var casted))
            {
                return casted;
            }
            else
            {
                return otherwise!;
            }
        }

        public static bool TrySafeCast<T>(object? value, [MaybeNullWhen(false)] out T casted)
        {
            if (value is T exactType)
            {
                casted = exactType;
                return true;
            }

            if (value == null)
            {
                casted = default;
                return false;
            }

            try
            {
                var type = typeof(T);
                var underlyingType = Nullable.GetUnderlyingType(type);

                type = underlyingType ?? type;
                if (type.GetTypeInfo().IsEnum)
                {
                    var enumUnderlyingType = Enum.GetUnderlyingType(type);
                    var objectValue = value;
                    if (enumUnderlyingType != value.GetType())
                    {
                        objectValue = Convert.ChangeType(value, enumUnderlyingType, CultureInfo.InvariantCulture);
                    }
                    if (Enum.IsDefined(type, objectValue))
                    {
                        casted = (T)Enum.ToObject(type, objectValue);
                        return true;
                    }
                }

                casted = (T)Convert.ChangeType(value, type, CultureInfo.InvariantCulture);
                return true;
            }
            catch (InvalidCastException)
            {
                casted = default;
                return false;
            }
            catch (Exception ex)
            {
                Logger.Instance.LogError(ex);
                casted = default;
                return false;
            }
        }

        public static string ReadResourceFromTypeAssembly(this Type type, string resourceName)
        {
            var assembly = type.GetTypeInfo().Assembly;
            var fullResourceName = assembly.GetManifestResourceNames().FirstOrDefault(n => n.EndsWith(resourceName, StringComparison.Ordinal));
            if (fullResourceName == null)
            {
                throw new InvalidOperationException($"Cannot read {resourceName} resource file.");
            }

            using var stream = assembly.GetManifestResourceStream(fullResourceName);
            using var streamReader = new StreamReader(stream);
            return streamReader.ReadToEnd();
        }
    }
}
