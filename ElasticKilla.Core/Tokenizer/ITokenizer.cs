using System.Collections.Generic;

namespace ElasticKilla.Core.Tokenizer
{
    public interface ITokenizer<T>
    {
        IEnumerable<T> Tokenize(T input);
    }
}