using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using ElasticKilla.Core.Collections;

namespace ElasticKilla.Core.Indexes
{
    internal class StringIndex<T> : IIndex<T, string>
    {
        private bool _disposed = false;

        private readonly ConcurrentDictionary<T, ISet<string>> _data;

        private readonly object _lock = new object();

        public event IIndex<T, string>.IndexedHandler Added;

        public event IIndex<T, string>.IndexedHandler Removed;

        public bool Contains(T key) => _data.ContainsKey(key);

        public ISet<string> Get(T key) => _data.TryGetValue(key, out var value)
            ? new HashSet<string>(value)
            : new HashSet<string>();

        public void Add(T key, string data)
        {
            _data.AddOrUpdate(
                key,
                new ConcurrentSet<string> { data },
                (k, v) =>
                {
                    v.Add(data);
                    return v;
                });

            Added?.Invoke(key, new [] { data });
        }

        public void Add(T key, IEnumerable<string> items)
        {
            var inserting = items.ToList();
            _data.AddOrUpdate(
                key,
                new ConcurrentSet<string>(inserting),
                (k, v) =>
                {
                    v.UnionWith(inserting);
                    return v;
                });

            Added?.Invoke(key, inserting);
        }

        public bool Remove(T key, string data) => RemoveFlush(key, set =>
        {
            set.Remove(data);

            Removed?.Invoke(key, new [] { data });

            return true;
        });

        public bool Remove(T key, IEnumerable<string> items) => RemoveFlush(key, set =>
        {
            var removed = items.ToList();
            set.ExceptWith(removed);

            Removed?.Invoke(key, removed);

            return true;
        });

        private bool RemoveFlush(T key, Func<ISet<string>, bool> remover)
        {
            if (_data.TryGetValue(key, out var value))
            {
                var result = remover(value);
                lock (_lock)
                {
                    if (!value.Any())
                        RemoveAll(key);
                }

                return result;
            }

            return false;
        }

        public bool RemoveAll(T key) => _data.TryRemove(key, out _);

        public void Flush()
        {
            foreach (var (_, value) in _data)
                value.Clear();
            _data.Clear();
        }
        
        #region IDisposable

        public void Dispose() => Dispose(true);

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
                return;

            if (disposing)
            {
                Flush();
            }

            _disposed = true;
        }

        #endregion

        public StringIndex()
        {
            _data = new ConcurrentDictionary<T, ISet<string>>();
        }
    }
}