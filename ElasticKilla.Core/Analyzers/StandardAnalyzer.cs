using System;
using System.Collections.Generic;
using ElasticKilla.Core.Indexers;
using ElasticKilla.Core.Searchers;

namespace ElasticKilla.Core.Analyzers
{
    public abstract class StandardAnalyzer<TKey, TValue> : IDisposable
    {
        private bool _disposed = false;

        protected readonly ISearcher<TKey, TValue> Searcher;

        protected readonly IIndexer<TValue, TKey> Indexer;

        public virtual IEnumerable<TValue> Search(TKey key) => Searcher.Search(key);

        #region IDisposable

        public void Dispose() => Dispose(true);

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
                return;

            if (disposing)
            {
                Indexer?.Dispose();
                Searcher?.Dispose();
            }

            _disposed = true;
        }

        #endregion

        protected StandardAnalyzer(
            ISearcher<TKey, TValue> searcher, 
            IIndexer<TValue, TKey> indexer)
        {
            Indexer = indexer;
            Searcher = searcher;
        }
    }
}