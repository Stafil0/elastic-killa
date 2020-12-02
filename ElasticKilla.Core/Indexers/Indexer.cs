using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using ElasticKilla.Core.Indexes;

namespace ElasticKilla.Core.Indexers
{
    internal class Indexer<TKey, TValue> : IIndexer<TKey, TValue>, IDisposable
    {
        private bool _disposed;

        private readonly IIndex<TKey, TValue> _forward;

        private readonly IIndex<TValue, TKey> _inverted;

        public void Add(TKey query, TValue value)
        {
            Debug.WriteLine($"Adding \"{value}\" for \"{query}\" query to forward index. Thread = {Thread.CurrentThread.ManagedThreadId}");
            _forward.Add(query, value);
        }

        public void Add(TKey query, IEnumerable<TValue> values)
        {
            Debug.WriteLine($"Adding bunch of words for \"{query}\" query to forward index. Thread = {Thread.CurrentThread.ManagedThreadId}");
            _forward.Add(query, values);
        }

        public void Switch(TKey who, TKey with)
        {
            Debug.WriteLine($"Switching forward indexes for \"{who}\" and \"{with}\". Thread = {Thread.CurrentThread.ManagedThreadId}");
            
            var whoIndex = _forward.Get(who);
            var withIndex = _forward.Get(with);

            Remove(who);
            Remove(with);

            Add(who, withIndex);
            Add(with, whoIndex);
            
            Debug.WriteLine($"Switched \"{who}\" and \"{with}\" forward indexes. Thread = {Thread.CurrentThread.ManagedThreadId}");
        }

        public void Remove(TKey query)
        {
            Debug.WriteLine($"Removing whole forward index for \"{query}\". Thread = {Thread.CurrentThread.ManagedThreadId}");
            _forward.RemoveAll(query, out _);
        }

        public void Remove(TKey query, IEnumerable<TValue> values)
        {
            Debug.WriteLine($"Removing bunch of words from forward index for \"{query}\" query. Thread = {Thread.CurrentThread.ManagedThreadId}");
            _forward.Remove(query, values);
        }

        public void Update(TKey query, IEnumerable<TValue> values)
        {
            Debug.WriteLine($"Updating forward index for \"{query}\". Thread = {Thread.CurrentThread.ManagedThreadId}");
            var tokens = values.ToList();
            var before = _forward.Get(query);
            var after = new HashSet<TValue>(tokens);

            after.ExceptWith(before);
            before.ExceptWith(tokens);

            Remove(query, before);
            Add(query, after);
            Debug.WriteLine($"Updated forward index for \"{query}\". Thread = {Thread.CurrentThread.ManagedThreadId}");
        }

        private void OnForwardAdded(TKey query, IEnumerable<TValue> values)
        {
            foreach (var item in values.AsParallel())
            {
                Debug.WriteLine($"Adding \"{item}\" for \"{query}\" query to inverted index. Thread = {Thread.CurrentThread.ManagedThreadId}");
                _inverted.Add(item, query);
            }
        }

        private void OnForwardRemoved(TKey query, IEnumerable<TValue> values)
        {
            foreach (var item in values.AsParallel())
            {
                Debug.WriteLine($"Removing \"{item}\" for \"{query}\" query from inverted index. Thread = {Thread.CurrentThread.ManagedThreadId}");
                _inverted.Remove(item, query);
            }
        }

        public Indexer(IIndex<TKey, TValue> forwardIndex, IIndex<TValue, TKey> invertedIndex)
        {
            _inverted = invertedIndex;
            _forward = forwardIndex;
            _forward.Added += OnForwardAdded;
            _forward.Removed += OnForwardRemoved;
        }

        #region IDisposable

        public void Dispose() => Dispose(true);
        
        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
                return;

            if (disposing)
            {
                _forward.Added -= OnForwardAdded;
                _forward.Removed -= OnForwardRemoved;

                if (_forward is IDisposable forward)
                    forward.Dispose();

                if (_inverted is IDisposable inverted)
                    inverted.Dispose();
            }

            _disposed = true;
        }

        #endregion
    }
}