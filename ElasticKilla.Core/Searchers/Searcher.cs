using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using ElasticKilla.Core.Indexes;

namespace ElasticKilla.Core.Searchers
{
    internal class Searcher<TKey, TValue> : ISearcher<TKey, TValue>, IDisposable
    {
        private bool _disposed;
        
        protected readonly IIndex<TKey, TValue> Index;

        public IEnumerable<TValue> Search(TKey query)
        {
            if (query == null)
                return Enumerable.Empty<TValue>();

            Debug.WriteLine($"Searching \"{query}\" in index. Thread = {Thread.CurrentThread.ManagedThreadId}");
            return Index.Get(query);
        }

        public Searcher(IIndex<TKey, TValue> index)
        {
            Index = index;
        }

        #region IDisposable

        public void Dispose() => Dispose(true);

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
                return;

            if (disposing)
            {
                if (Index is IDisposable index)
                    index.Dispose();
            }
            
            _disposed = true;
        }

        #endregion
    }
}