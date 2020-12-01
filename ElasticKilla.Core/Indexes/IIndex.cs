using System;
using System.Collections.Generic;

namespace ElasticKilla.Core.Indexes
{
    public interface IIndex<TKey, TValue> : IDisposable
    {
        delegate void IndexedHandler(TKey key, IEnumerable<TValue> items);

        event IndexedHandler Added; 

        event IndexedHandler Removed;

        bool Contains(TKey key);
        
        ISet<TValue> Get(TKey key);

        void Add(TKey key, TValue data);

        void Add(TKey key, IEnumerable<TValue> items);

        
        public bool Remove(TKey key, TValue data);

        public bool Remove(TKey key, IEnumerable<TValue> items);

        public bool RemoveAll(TKey key);
    }
}