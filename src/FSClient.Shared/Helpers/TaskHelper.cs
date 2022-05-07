namespace FSClient.Shared.Helpers
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    public static class TaskHelper
    {
        [return: NotNullIfNotNull("otherwise")]
        public static async Task<T> WhenAny<T>(
            this IEnumerable<Func<CancellationToken, Task<T>>> taskFactories,
            Func<T, bool> condition,
            T? otherwise = default,
            CancellationToken token = default)
        {
            var tasks = taskFactories.ToArray();
            if (tasks.Length == 0)
            {
                return otherwise!;
            }

            var completedCount = 0;

            var tcs = new TaskCompletionSource<T>();
            using (var ctSource = CancellationTokenSource.CreateLinkedTokenSource(token))
            {
                foreach (var taskFactory in tasks)
                {
                    RunTask();

                    async void RunTask()
                    {
                        var resultSetted = false;
                        try
                        {
                            var result = await taskFactory(ctSource.Token);
                            if (condition(result)
                                && !ctSource.IsCancellationRequested)
                            {
                                ctSource.Cancel();
                                resultSetted = true;
                                _ = tcs.TrySetResult(result);
                            }
                        }
                        catch (OperationCanceledException)
                        {

                        }
                        catch (Exception ex)
                        {
                            _ = tcs.TrySetException(ex);
                        }
                        finally
                        {
                            if (Interlocked.Increment(ref completedCount) == tasks.Length
                                && !resultSetted)
                            {
                                _ = tcs.TrySetResult(otherwise!);
                            }
                        }
                    }
                }
                return await tcs.Task.ConfigureAwait(false);
            }
        }
    }
}
