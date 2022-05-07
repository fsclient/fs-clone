namespace FSClient.Shared.Mvvm
{
    using System;
    using System.Collections.Concurrent;
    using System.ComponentModel;
    using System.Diagnostics;
    using System.Diagnostics.CodeAnalysis;
    using System.Runtime.CompilerServices;

    [DebuggerStepThrough]
    public abstract class BindableBase : INotifyPropertyChanged
    {
        private readonly ConcurrentDictionary<string, object> objects;
        private readonly ContextSafeEvent<PropertyChangedEventHandler> propertyChangedEvent;

        protected BindableBase()
        {
            objects = new ConcurrentDictionary<string, object>();
            propertyChangedEvent = new ContextSafeEvent<PropertyChangedEventHandler>();
        }

        public event PropertyChangedEventHandler PropertyChanged
        {
            add => propertyChangedEvent.Register(value);
            remove => propertyChangedEvent.Unregister(value);
        }

        [return: NotNullIfNotNull("def")]
        protected T Get<T>(T? def = default, [CallerMemberName] string propertyName = null!)
        {
            return (T)objects.GetOrAdd(propertyName, def!);
        }

        protected T Get<T>(Func<T> def, [CallerMemberName] string propertyName = null!)
        {
            return (T)objects.GetOrAdd(propertyName, _ => def()!);
        }

        protected void OnPropertyChanged(params string[] propertyNames)
        {
            propertyChangedEvent.Invoke(handler =>
            {
                foreach (var name in propertyNames)
                {
                    handler.Invoke(this, new PropertyChangedEventArgs(name));
                }
            });
        }

        protected bool Set<T>(T value, [CallerMemberName] string propertyName = null!)
        {
            var hasOldValue = objects.TryGetValue(propertyName, out var oldObject);

            if ((objects.AddOrUpdate(propertyName, value!, (_, __) => value!) is T newValue
                && hasOldValue
                && oldObject is T oldValue && newValue.Equals(oldValue))
                || (oldObject == null && value == null))
            {
                return false;
            }

            OnPropertyChanged(propertyName);
            return true;
        }
    }
}
