namespace FSClient.UWP.Shared.Services
{
    using System;
    using System.Collections.Generic;

    using Windows.System;

#if WINUI3
    using Microsoft.UI.Xaml.Controls;
#else
    using Windows.UI.Xaml.Controls;
#endif

    using FSClient.Shared.Helpers;
    using FSClient.Shared.Services;

    public class NavigationItem : ILogState
    {
        public NavigationItem(string title, Type type)
        {
            Title = title.NotEmptyOrNull() ?? throw new ArgumentException("Title is null or empty", nameof(title));
            Type = type ?? throw new ArgumentException("Type is null", nameof(type));
        }

        public object? Parameter { get; set; }

        public string Title { get; }

        public string? Glyph { get; set; }

        public Type Type { get; }

        public VirtualKey AcceleratorKey { get; set; }

        public virtual string ToShortString()
        {
            return Title ?? Type?.Name ?? base.ToString()!;
        }

        public override string ToString()
        {
            return Title
                   ?? (Parameter == null
                       ? Type?.Name
                       : Type?.Name + " : " + Parameter)
                   ?? base.ToString()!;
        }

        public IDictionary<string, string> GetLogProperties(bool verbose)
        {
            IDictionary<string, string> props;

            if (Parameter is ILogState logState)
            {
                props = logState.GetLogProperties(verbose);
            }
            else
            {
                props = new Dictionary<string, string>();
                if (verbose)
                {
                    props.Add(nameof(Parameter), Parameter?.ToString() ?? "Null");
                }
            }

            if (Type != null)
            {
                props.Add(nameof(Type), Type.Name);
            }

            return props;
        }
    }

    public class NavigationItem<TPage> : NavigationItem
        where TPage : Page
    {
        public NavigationItem(string title) : base(title, typeof(TPage))
        {
        }
    }
}
