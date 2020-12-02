using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using ElasticKilla.Core.Collections;

namespace ElasticKilla.Core.Indexes
{
    public class StringIndex<T> : BaseIndex<T, string>, IDisposable
    {
        private bool _disposed;

        private readonly ConcurrentDictionary<T, ISet<string>> _data;

        private readonly object _lock = new object();

        public override bool Contains(T query) => _data.ContainsKey(query);

        public override ISet<string> Get(T query) => _data.TryGetValue(query, out var value)
            ? new HashSet<string>(value)
            : new HashSet<string>();

        protected override void AddIndex(T query, string value)
        {
            _data.AddOrUpdate(
                query,
                new ConcurrentSet<string> { value },
                (k, v) =>
                {
                    v.Add(value);
                    return v;
                });
        }

        protected override void AddIndex(T query, IEnumerable<string> values)
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
        }

        protected override bool RemoveIndex(T query, string value) => RemoveFlush(query, set => set.Remove(value));

        protected override bool RemoveIndex(T query, IEnumerable<string> values) => RemoveFlush(query, set =>
        {
            set.ExceptWith(values);
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
                        _data.TryRemove(key, out value);
                }

                return result;
            }

            return false;
        }

        protected override bool RemoveAllIndex(T query, out IEnumerable<string> old)
        {
            var isRemoved = _data.TryRemove(query, out var removed);
            old = removed;

            return isRemoved;
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