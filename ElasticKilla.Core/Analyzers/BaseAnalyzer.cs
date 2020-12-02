using System;
using System.Collections.Generic;
using ElasticKilla.Core.Indexers;
using ElasticKilla.Core.Searchers;

namespace ElasticKilla.Core.Analyzers
{
    public abstract class BaseAnalyzer<TKey, TValue> : IDisposable
    {
        private bool _disposed;

        protected readonly ISearcher<TKey, TValue> Searcher;

        protected readonly IIndexer<TValue, TKey> Indexer;

        public virtual IEnumerable<TValue> Search(TKey query) => Searcher.Search(query);

        #region IDisposable

        public void Dispose() => Dispose(true);

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
                return;

            if (disposing)
            {
                if (Indexer is IDisposable indexer)
                    indexer.Dispose();

                if (Searcher is IDisposable searcher)
                    searcher.Dispose();
            }

            _disposed = true;
        }

        #endregion

        protected BaseAnalyzer(
            ISearcher<TKey, TValue> searcher, 
            IIndexer<TValue, TKey> indexer)
        {
            Searcher = searcher;
            Indexer = indexer;
        }
    }
}