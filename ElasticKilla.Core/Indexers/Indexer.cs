using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ElasticKilla.Core.Indexes;

namespace ElasticKilla.Core.Indexers
{
    internal class Indexer<TKey, TValue> : IIndexer<TKey, TValue>
    {
        private bool _disposed = false;

        private readonly IIndex<TKey, TValue> _forward;

        private readonly IIndex<TValue, TKey> _inverted;

        public void Add(TKey query, TValue value) => _forward.Add(query, value);

        public void Add(TKey query, IEnumerable<TValue> values) => _forward.Add(query, values);

        public void Switch(TKey who, TKey with)
        {
            var whoIndex = _forward.Get(who);
            var withIndex = _forward.Get(with);

            // TODO: добавить асинхронность.
            Remove(who);
            Remove(with);

            Add(who, withIndex);
            Add(with, whoIndex);
        }

        public void Remove(TKey query) => _forward.RemoveAll(query);

        public void Remove(TKey query, IEnumerable<TValue> values) => _forward.Remove(query, values);

        public void Update(TKey query, IEnumerable<TValue> values)
        {
            var tokens = values.ToList();
            var before = _forward.Get(query);
            var after = new HashSet<TValue>(tokens);

            after.ExceptWith(before);
            before.ExceptWith(tokens);

            // TODO: добавить асинхронность.
            Remove(query, before);
            Add(query, after);
        }

        private void OnForwardOnAdded(TKey query, IEnumerable<TValue> values)
        {
            // TODO: добавить асинхронность.
            foreach (var item in values) 
                _inverted.Add(item, query);
        }

        private void OnForwardOnRemoved(TKey query, IEnumerable<TValue> values)
        {
            // TODO: добавить асинхронность.
            foreach (var item in values)
                _inverted.Remove(item, query);
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