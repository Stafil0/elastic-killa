using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;

namespace ElasticKilla.Core.Indexes
{
    public abstract class BaseIndex<TKey, TValue> : IIndex<TKey, TValue>
    {
        public event IIndex<TKey, TValue>.IndexedHandler Added;

        public event IIndex<TKey, TValue>.IndexedHandler Removed;

        public abstract bool Contains(TKey query);

        public abstract ISet<TValue> Get(TKey query);

        public void Add(TKey query, TValue value)
        {
            Debug.WriteLine($"Adding \"{value}\" for \"{query}\" query to index. Thread = {Thread.CurrentThread.ManagedThreadId}");

            AddIndex(query, value);
            Added?.Invoke(query, new [] { value });
        }
        protected abstract void AddIndex(TKey query, TValue value);

        public void Add(TKey query, IEnumerable<TValue> values)
        {
            var added = values.ToArray();
            if (!added.Any())
            {
                Debug.WriteLine($"Empty add request for \"{query}\" query, skipping. Thread = {Thread.CurrentThread.ManagedThreadId}");
                return;
            }

            Debug.WriteLine($"Adding bunch of words for \"{query}\" query to index. Thread = {Thread.CurrentThread.ManagedThreadId}");

            AddIndex(query, added);
            Added?.Invoke(query, added);
        }
        protected abstract void AddIndex(TKey query, IEnumerable<TValue> values);

        public bool Remove(TKey query, TValue value)
        {
            Debug.WriteLine($"Removing \"{value}\" for \"{query}\" query to index. Thread = {Thread.CurrentThread.ManagedThreadId}");

            var isRemoved = RemoveIndex(query, value);
            if (isRemoved)
                Removed?.Invoke(query, new [] { value });

            return isRemoved;
        }
        protected abstract bool RemoveIndex(TKey query, TValue value);
        
        public bool Remove(TKey query, IEnumerable<TValue> values)
        {
            var removed = values.ToArray();
            if (!removed.Any())
            {
                Debug.WriteLine($"Empty remove request for \"{query}\" query, skipping. Thread = {Thread.CurrentThread.ManagedThreadId}");
                return false;
            }

            Debug.WriteLine($"Removing bunch of words for \"{query}\" query to index. Thread = {Thread.CurrentThread.ManagedThreadId}");

            var isRemoved = RemoveIndex(query, removed);
            if (isRemoved)
                Removed?.Invoke(query, removed);

            return isRemoved;
        }
        protected abstract bool RemoveIndex(TKey query, IEnumerable<TValue> value);

        public bool RemoveAll(TKey query, out IEnumerable<TValue> old)
        {
            Debug.WriteLine($"Removing whole index for \"{query}\". Thread = {Thread.CurrentThread.ManagedThreadId}");

            var isRemoved = RemoveAllIndex(query, out old);
            if (isRemoved)
                Removed?.Invoke(query, old);

            return isRemoved;
        }
        protected abstract bool RemoveAllIndex(TKey query, out IEnumerable<TValue> old);
    }
}