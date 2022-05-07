namespace FSClient.Shared.Helpers
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.Diagnostics.CodeAnalysis;
    using System.Reflection;

    public static class EnumHelper
    {
        [return: NotNullIfNotNull("enumValue")]
        public static string? GetDisplayName(this Enum enumValue)
        {
            return enumValue?.GetType().GetTypeInfo()
                .GetDeclaredField(enumValue.ToString())?
                .GetCustomAttribute<DisplayAttribute>()?
                .GetName()
                ?? enumValue?.ToString();
        }

        public static string? GetDisplayDescription(this Enum enumValue)
        {
            return enumValue.GetType().GetTypeInfo()
                .GetDeclaredField(enumValue.ToString())?
                .GetCustomAttribute<DisplayAttribute>()?
                .GetDescription();
        }

        public static IEnumerable<T> GetFlags<T>(this T flags)
            where T : Enum
        {
            foreach (Enum? value in Enum.GetValues(flags.GetType()))
            {
                if (flags.HasFlag(value!))
                {
                    yield return (T)value!;
                }
            }
        }
    }
}
