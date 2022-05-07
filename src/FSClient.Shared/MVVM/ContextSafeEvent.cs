namespace FSClient.Shared.Mvvm
{
    using System;
    using System.Collections.Concurrent;
    using System.Threading;
    using System.Threading.Tasks;

    public static class ContextSafeEvent
    {
        public static Func<bool> HasAccess = () => false;
    }

    public class ContextSafeEvent<THandler>
        where THandler : Delegate
    {
        private static readonly SynchronizationContext keyNullContext = new SynchronizationContext();

        private readonly ConcurrentDictionary<SynchronizationContext, THandler> handlersPerContext;

        public ContextSafeEvent()
        {
            handlersPerContext = new ConcurrentDictionary<SynchronizationContext, THandler>();
        }

        public void Register(THandler value)
        {
            handlersPerContext.AddOrUpdate(
                SynchronizationContext.Current ?? keyNullContext,
                value,
                (_, h) => (THandler)Delegate.Combine(h, value));
        }

        public void Unregister(THandler value)
        {
            var key = SynchronizationContext.Current ?? keyNullContext;
            if (handlersPerContext.TryGetValue(key, out var handler))
            {
                var newHandler = (THandler)Delegate.RemoveAll(handler, value)!;
                if (newHandler == null)
                {
                    handlersPerContext.TryRemove(key, out _);
                }
                else if (newHandler != handler)
                {
                    handlersPerContext.TryUpdate(key, newHandler, handler);
                }
            }
        }

        public void Invoke(Action<THandler> action)
        {
            foreach (var handlersPair in handlersPerContext)
            {
                var context = handlersPair.Key;
                var handler = handlersPair.Value;

                if (context == keyNullContext
                    || (context == SynchronizationContext.Current
                    && ContextSafeEvent.HasAccess())
                    )
                {
                    action(handler);
                }
                else
                {
                    context.Post(
                        state => ((PostState)state!).Execute(),
                        new PostState(action, handler));
                }
            }
        }

        public async ValueTask InvokeAsync(Action<THandler> action, CancellationToken cancellationToken)
        {
            if (handlersPerContext.IsEmpty)
            {
                return;
            }

            using var semaphore = new SemaphoreSlim(0);

            foreach (var handlersPair in handlersPerContext)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var context = handlersPair.Key;
                var handler = handlersPair.Value;

                if (context == keyNullContext
                    || (context == SynchronizationContext.Current
                    && ContextSafeEvent.HasAccess())
                    )
                {
                    action(handler);
                }
                else
                {
                    context.Post(
                        state => ((PostState)state!).ExecuteWithSemaphoreRelease(),
                        new PostState(action, handler, semaphore));

                    await semaphore.WaitAsync(cancellationToken).ConfigureAwait(true);
                }
            }
        }

        private struct PostState
        {
            private readonly Action<THandler> action;
            private readonly THandler handler;
            private readonly SemaphoreSlim? semaphore;

            public PostState(Action<THandler> action, THandler handler, SemaphoreSlim? semaphore = null)
            {
                this.action = action;
                this.handler = handler;
                this.semaphore = semaphore;
            }

            public void Execute()
            {
                action(handler);
            }

            public void ExecuteWithSemaphoreRelease()
            {
                try
                {
                    action(handler);
                }
                finally
                {
                    try
                    {
                        semaphore!.Release();
                    }
                    catch (ObjectDisposedException)
                    {

                    }
                }
            }
        }
    }
}
