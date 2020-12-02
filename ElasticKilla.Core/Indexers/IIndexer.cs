using System.Collections.Generic;

namespace ElasticKilla.Core.Indexers
{
    public interface IIndexer<TKey, TValue>
    {
        void Add(TKey query, TValue value);

        void Add(TKey query, IEnumerable<TValue> values);

        void Switch(TKey who, TKey with);

        void Remove(TKey query);

        void Remove(TKey query, IEnumerable<TValue> values);

        void Update(TKey query, IEnumerable<TValue> values);
    }
}