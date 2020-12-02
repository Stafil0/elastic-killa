using System;
using System.Collections.Generic;

namespace ElasticKilla.Core.Searchers
{
    public interface ISearcher<TKey, TValue>
    {
        IEnumerable<TValue> Search(TKey query);
    }
}