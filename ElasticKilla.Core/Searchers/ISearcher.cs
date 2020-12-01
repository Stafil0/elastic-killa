using System;
using System.Collections.Generic;

namespace ElasticKilla.Core.Searchers
{
    public interface ISearcher<TKey, TValue> : IDisposable
    {
        IEnumerable<TValue> Search(TKey key);
    }
}