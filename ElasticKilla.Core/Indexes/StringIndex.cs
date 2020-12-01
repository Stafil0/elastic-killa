using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using ElasticKilla.Core.Collections;

namespace ElasticKilla.Core.Indexes
{
    public class StringIndex<T> : IIndex<T, string>
    {
        private bool _disposed = false;

        private readonly ConcurrentDictionary<T, ISet<string>> _data;

        private readonly object _lock = new object();

        // TODO: перенести вызов событий в базовый клас.
        public event IIndex<T, string>.IndexedHandler Added;

        public event IIndex<T, string>.IndexedHandler Removed;

        public bool Contains(T query) => _data.ContainsKey(query);

        public ISet<string> Get(T query) => _data.TryGetValue(query, out var value)
            ? new HashSet<string>(value)
            : new HashSet<string>();

        public void Add(T query, string value)
        {
            _data.AddOrUpdate(
                query,
                new ConcurrentSet<string> { value },
                (k, v) =>
                {
                    v.Add(value);
                    return v;
                });

            Added?.Invoke(query, new [] { value });
        }

        public void Add(T query, IEnumerable<string> values)
        {
            var inserting = values.ToList();
            _data.AddOrUpdate(
                query,
                new ConcurrentSet<string>(inserting),
                (k, v) =>
                {
                    v.UnionWith(inserting);
                    return v;
                });

            Added?.Invoke(query, inserting);
        }

        public bool Remove(T query, string value) => RemoveFlush(query, set =>
        {
            set.Remove(value);

            Removed?.Invoke(query, new [] { value });

            return true;
        });

        public bool Remove(T query, IEnumerable<string> values) => RemoveFlush(query, set =>
        {
            var removed = values.ToList();
            set.ExceptWith(removed);

            Removed?.Invoke(query, removed);

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

        public bool RemoveAll(T query)
        {
            var removed = _data.TryRemove(query, out var old);

            Removed?.Invoke(query, old);

            return removed;
        }

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