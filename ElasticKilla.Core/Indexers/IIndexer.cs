using System;
using System.Collections.Generic;

namespace ElasticKilla.Core.Indexers
{
    public interface IIndexer<TKey, TValue> : IDisposable
    {
        void Add(TKey resource, TValue item);

        void Add(TKey resource, IEnumerable<TValue> items);

        void Switch(TKey who, TKey with);

        void Remove(TKey resource);

        void Remove(TKey resource, IEnumerable<TValue> items);

        void Update(TKey resource, IEnumerable<TValue> items);
    }
}