using System;
using System.Collections.Generic;
using System.Linq;
using ElasticKilla.Core.Indexes;

namespace ElasticKilla.Core.Indexers
{
    internal class Indexer<TKey, TValue> : IIndexer<TKey, TValue>
    {
        private bool _disposed = false;

        private readonly IIndex<TKey, TValue> _forward;

        private readonly IIndex<TValue, TKey> _inverted;

        public void Add(TKey resource, TValue item) => _forward.Add(resource, item);

        public void Add(TKey resource, IEnumerable<TValue> items) => _forward.Add(resource, items);

        public void Switch(TKey who, TKey with)
        {
            var whoIndex = _forward.Get(who);
            var withIndex = _forward.Get(with);

            Remove(who);
            Remove(with);

            Add(who, withIndex);
            Add(with, whoIndex);
        }

        public void Remove(TKey resource) => _forward.RemoveAll(resource);

        public void Remove(TKey resource, IEnumerable<TValue> items) => _forward.Remove(resource, items);

        public void Update(TKey resource, IEnumerable<TValue> items)
        {
            var tokens = items.ToList();
            var before = _forward.Get(resource);
            var after = new HashSet<TValue>(tokens);

            after.ExceptWith(before);
            before.ExceptWith(tokens);

            Remove(resource, before);
            Add(resource, after);
        }

        private void OnForwardOnAdded(TKey key, IEnumerable<TValue> items)
        {
            foreach (var item in items) 
                _inverted.Add(item, key);
        }

        private void OnForwardOnRemoved(TKey key, IEnumerable<TValue> items)
        {
            foreach (var item in items)
                _inverted.Remove(item, key);
        }

        public Indexer(IIndex<TKey, TValue> forwardIndex, IIndex<TValue, TKey> invertedIndex)
        {
            _inverted = invertedIndex;
            _forward = forwardIndex;
            _forward.Added += OnForwardOnAdded;
            _forward.Removed += OnForwardOnRemoved;
        }

        #region IDisposable

        public void Dispose() => Dispose(true);
        
        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
                return;

            if (disposing)
            {
                _forward.Added -= OnForwardOnAdded;
                _forward.Removed -= OnForwardOnRemoved;
                _forward?.Dispose();

                _inverted?.Dispose();
            }

            _disposed = true;
        }

        #endregion
    }
}