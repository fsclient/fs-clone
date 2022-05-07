#if !NET5_0 && !NETSTANDARD2_1
namespace System.Diagnostics.CodeAnalysis
{
    using System;

    [AttributeUsage(
        AttributeTargets.Field | AttributeTargets.Parameter | AttributeTargets.Property,
        Inherited = false
    )]
    public sealed class AllowNullAttribute : Attribute
    {
        public AllowNullAttribute() { }
    }

    [AttributeUsage(
        AttributeTargets.Field | AttributeTargets.Parameter | AttributeTargets.Property,
        Inherited = false
    )]
    public sealed class DisallowNullAttribute : Attribute
    {
        public DisallowNullAttribute() { }
    }

    [AttributeUsage(AttributeTargets.Method, Inherited = false)]
    public sealed class DoesNotReturnAttribute : Attribute
    {
        public DoesNotReturnAttribute() { }
    }

    [AttributeUsage(AttributeTargets.Parameter, Inherited = false)]
    public sealed class DoesNotReturnIfAttribute : Attribute
    {
        public bool ParameterValue { get; }
        public DoesNotReturnIfAttribute(bool parameterValue)
        {
            ParameterValue = parameterValue;
        }
    }

    [AttributeUsage(
        AttributeTargets.Field | AttributeTargets.Parameter |
        AttributeTargets.Property | AttributeTargets.ReturnValue,
        Inherited = false
    )]
    public sealed class MaybeNullAttribute : Attribute
    {
        public MaybeNullAttribute() { }
    }

    [AttributeUsage(AttributeTargets.Parameter, Inherited = false)]
    public sealed class MaybeNullWhenAttribute : Attribute
    {
        public bool ReturnValue { get; }

        public MaybeNullWhenAttribute(bool returnValue)
        {
            ReturnValue = returnValue;
        }
    }

    [AttributeUsage(
        AttributeTargets.Field | AttributeTargets.Parameter |
        AttributeTargets.Property | AttributeTargets.ReturnValue,
        Inherited = false
    )]
    public sealed class NotNullAttribute : Attribute
    {
        public NotNullAttribute() { }
    }

    [AttributeUsage(
        AttributeTargets.Parameter | AttributeTargets.Property | AttributeTargets.ReturnValue,
        AllowMultiple = true,
        Inherited = false
    )]
    public sealed class NotNullIfNotNullAttribute : Attribute
    {
        public string ParameterName { get; }

        public NotNullIfNotNullAttribute(string parameterName)
        {
            ParameterName = parameterName;
        }
    }

    [AttributeUsage(AttributeTargets.Parameter, Inherited = false)]
    public sealed class NotNullWhenAttribute : Attribute
    {
        public bool ReturnValue { get; }

        public NotNullWhenAttribute(bool returnValue)
        {
            ReturnValue = returnValue;
        }
    }
}
#endif
