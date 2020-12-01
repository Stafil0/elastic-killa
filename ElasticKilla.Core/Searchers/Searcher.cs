using System;
using System.Collections.Generic;
using ElasticKilla.Core.Indexes;

namespace ElasticKilla.Core.Searchers
{
    internal class Searcher<TKey, TValue> : ISearcher<TKey, TValue>
    {
        private bool _disposed = false;
        
        protected readonly IIndex<TKey, TValue> Index;

        public IEnumerable<TValue> Search(TKey key) => Index.Get(key);

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
                Index?.Dispose();
            }
            
            _disposed = true;
        }

        #endregion
    }
}