namespace FSClient.UWP.Shared.Helpers
{
    using System;
    using System.Threading.Tasks;

    using Windows.ApplicationModel.Core;
    using Windows.UI.Core;

    // https://github.com/Microsoft/Windows-task-snippets/blob/master/tasks/UI-thread-task-await-from-background-thread.md
    public static class DispatcherHelper
    {
        public static bool HasAccess()
        {
            try
            {
                return CoreWindow.GetForCurrentThread()?.Dispatcher?.HasThreadAccess == true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public static Task YieldIdle(this CoreDispatcher dispatcher)
        {
            var taskCompletionSource = new TaskCompletionSource<bool>();
            _ = dispatcher.RunIdleAsync(new IdleDispatchedHandler(_ => taskCompletionSource.TrySetResult(true)));
            return taskCompletionSource.Task;
        }

        public static CoreDispatcher GetForCurrentOrMainView()
        {
            var window = (CoreWindow.GetForCurrentThread() ?? CoreApplication.MainView.CoreWindow);
            if (window == null)
            {
                throw new InvalidOperationException("No window was found");
            }

            return window.Dispatcher;
        }

        public static ValueTask CheckBeginInvokeOnUI(this CoreDispatcher dispatcher, Action action,
            CoreDispatcherPriority priority = CoreDispatcherPriority.Normal)
        {
            if (dispatcher == null)
            {
                throw new ArgumentNullException(nameof(dispatcher));
            }

            if (dispatcher.HasThreadAccess)
            {
                action();
                return new ValueTask();
            }

            return priority == CoreDispatcherPriority.Idle
                ? new ValueTask(dispatcher.RunIdleAsync(_ => action()).AsTask())
                : new ValueTask(dispatcher.RunAsync(priority, new DispatchedHandler(action)).AsTask());
        }

        public static Task<T> CheckBeginInvokeOnUI<T>(this CoreDispatcher dispatcher, Func<Task<T>> action,
            CoreDispatcherPriority priority = CoreDispatcherPriority.Normal)
        {
            if (dispatcher == null)
            {
                throw new ArgumentNullException(nameof(dispatcher));
            }

            return dispatcher.HasThreadAccess
                ? action()
                : dispatcher.RunTaskAsync(action, priority);
        }

        public static Task CheckBeginInvokeOnUI(this CoreDispatcher dispatcher, Func<Task> action,
            CoreDispatcherPriority priority = CoreDispatcherPriority.Normal)
        {
            if (dispatcher == null)
            {
                throw new ArgumentNullException(nameof(dispatcher));
            }

            return dispatcher.HasThreadAccess
                ? action()
                : dispatcher.RunTaskAsync(action, priority);
        }

        public static async Task<T> RunTaskAsync<T>(this CoreDispatcher dispatcher,
            Func<Task<T>> func, CoreDispatcherPriority priority = CoreDispatcherPriority.Normal)
        {
            if (dispatcher == null)
            {
                throw new ArgumentNullException(nameof(dispatcher));
            }

            var taskCompletionSource = new TaskCompletionSource<T>();
            if (priority == CoreDispatcherPriority.Idle)
            {
                await dispatcher.RunIdleAsync(_ => TaskFunc());
            }
            else
            {
                await dispatcher.RunAsync(priority, TaskFunc);
            }

            return await taskCompletionSource.Task.ConfigureAwait(false);

            async void TaskFunc()
            {
                try
                {
                    taskCompletionSource.SetResult(await func().ConfigureAwait(true));
                }
                catch (Exception ex)
                {
                    taskCompletionSource.SetException(ex);
                }
            }
        }

        public static Task RunTaskAsync(this CoreDispatcher dispatcher,
            Func<Task> func, CoreDispatcherPriority priority = CoreDispatcherPriority.Normal)
        {
            return RunTaskAsync(dispatcher, async () =>
            {
                await func().ConfigureAwait(false);
                return false;
            }, priority);
        }
    }
}
