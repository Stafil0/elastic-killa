using System;
using System.Threading.Tasks;

namespace ElasticKilla.Core.Collections
{
    internal class BackgroundQueue
    {
        private Task _last = Task.FromResult(true);
        private readonly object _lock = new object();

        public Task QueueTask(Action action)
        {
            lock (_lock)
            {
                _last = _last.ContinueWith(t => action(), TaskContinuationOptions.RunContinuationsAsynchronously);
                return _last;
            }
        }

        public Task<T> QueueTask<T>(Func<T> work)
        {
            lock (_lock)
            {
                var task = _last.ContinueWith(t => work(), TaskContinuationOptions.RunContinuationsAsynchronously);
                _last = task;
                return task;
            }
        }
    }
}