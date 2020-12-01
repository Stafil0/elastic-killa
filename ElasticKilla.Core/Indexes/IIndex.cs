using System;
using System.Collections.Generic;

namespace ElasticKilla.Core.Indexes
{
    // TODO: добавить асинхронные методы.
    public interface IIndex<TKey, TValue> : IDisposable
    {
        delegate void IndexedHandler(TKey query, IEnumerable<TValue> values);

        event IndexedHandler Added;

        event IndexedHandler Removed;

        bool Contains(TKey query);
        
        ISet<TValue> Get(TKey query);

        void Add(TKey query, TValue value);

        void Add(TKey query, IEnumerable<TValue> values);
        
        public bool Remove(TKey query, TValue value);

        public bool Remove(TKey query, IEnumerable<TValue> values);

        public bool RemoveAll(TKey query);
    }
}