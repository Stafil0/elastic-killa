using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ElasticKilla.Core.Lockers;

namespace ElasticKilla.Core.Collections
{
    internal class BackgroundTaskQueue<TKey> : IDisposable
    {
        private readonly ReaderWriterLockSlim _lock = new ReaderWriterLockSlim(LockRecursionPolicy.SupportsRecursion);

        private readonly ConcurrentDictionary<string, CancellationTokenSource> _cancellations = new ConcurrentDictionary<string, CancellationTokenSource>();

        private readonly ConcurrentDictionary<TKey, Task> _tasks = new ConcurrentDictionary<TKey, Task>();

        public bool IsEmpty
        {
            get
            {
                using (new ReadLockCookie(_lock))
                {
                    return _tasks.Values.All(x => x.IsCompleted);
                }
            }
        }

        public IDisposable Pause()
        {
            var cookie = new ReadLockCookie(_lock);
            SpinWait.SpinUntil(() => IsEmpty);

            return cookie;
        }

        public Task QueueTask(TKey key, Action action, string cancellationKey = null)
        {
            using (new WriteLockCookie(_lock))
            {
                ClearCompletedTasks();

                var lastTask = _tasks.GetOrAdd(key, Task.FromResult(true));
                var newTask = lastTask.ContinueWith(t => action(),
                    GetCancellationToken(cancellationKey),
                    TaskContinuationOptions.RunContinuationsAsynchronously,
                    TaskScheduler.Current);

                _tasks.TryUpdate(key, newTask, lastTask);
                return newTask;
            }
        }

        public Task<TValue> QueueTask<TValue>(TKey key, Func<TValue> func, string cancellationKey = null)
        {
            using (new WriteLockCookie(_lock))
            {
                ClearCompletedTasks();

                var lastTask = _tasks.GetOrAdd(key, Task.FromResult(true));
                var newTask = lastTask.ContinueWith(t => func(),
                    GetCancellationToken(cancellationKey),
                    TaskContinuationOptions.RunContinuationsAsynchronously,
                    TaskScheduler.Current);
                
                _tasks.TryUpdate(key, newTask, lastTask);
                return newTask;
            }
        }

        public void CancelTasks(string cancellationKey)
        {
            using (new WriteLockCookie(_lock))
            {
                if (string.IsNullOrEmpty(cancellationKey) || !_cancellations.TryRemove(cancellationKey, out var cts))
                    return;

                cts.Cancel();
                ClearCompletedTasks();
            }
        }

        private void ClearCompletedTasks()
        {
            var keys = _tasks.Where(x => x.Value.IsCompleted).Select(x => x.Key);
            foreach (var key in keys)
            {
                _tasks.TryRemove(key, out _);
            }
        }

        private CancellationToken GetCancellationToken(string key)
        {
            CancellationToken token;
            if (!string.IsNullOrEmpty(key))
            {
                var cts = _cancellations.GetOrAdd(key, new CancellationTokenSource());
                token = cts.Token;
            }
            else token = CancellationToken.None;

            return token;
        }

        public void Dispose()
        {
            _lock?.Dispose();
        }
    }
}