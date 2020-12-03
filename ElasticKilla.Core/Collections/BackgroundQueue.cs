using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using ElasticKilla.Core.Lockers;

namespace ElasticKilla.Core.Collections
{
    internal class BackgroundQueue : IDisposable
    {
        private Task _last = Task.FromResult(true);

        private readonly ReaderWriterLockSlim _lock = new ReaderWriterLockSlim(LockRecursionPolicy.SupportsRecursion);

        private readonly ConcurrentDictionary<string, CancellationTokenSource> _cancellations = new ConcurrentDictionary<string, CancellationTokenSource>();

        public bool IsEmpty
        {
            get
            {
                using (new ReadLockCookie(_lock))
                {
                    return _last.IsCompleted;
                }
            }
        }

        public IDisposable Pause() => new ReadLockCookie(_lock);

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

        public Task QueueTask(Action action, string key = null)
        {
            using (new WriteLockCookie(_lock))
            {
                _last = _last.ContinueWith(t => action(),
                    GetCancellationToken(key),
                    TaskContinuationOptions.RunContinuationsAsynchronously,
                    TaskScheduler.Current);

                return _last;
            }
        }

        public Task<T> QueueTask<T>(Func<T> func, string key = null)
        {
            using (new WriteLockCookie(_lock))
            {
                var task = _last.ContinueWith(t => func(),
                    GetCancellationToken(key),
                    TaskContinuationOptions.RunContinuationsAsynchronously,
                    TaskScheduler.Current);
                _last = task;
                return task;
            }
        }

        public void CancelTasks(string key)
        {
            using (new WriteLockCookie(_lock))
            {
                if (string.IsNullOrEmpty(key) || !_cancellations.TryRemove(key, out var cts))
                    return;

                cts.Cancel();
            }
        }

        public void Dispose()
        {
            _lock?.Dispose();
        }
    }
}