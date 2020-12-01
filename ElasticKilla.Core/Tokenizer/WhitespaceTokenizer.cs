using System.Collections.Generic;

namespace ElasticKilla.Core.Tokenizer
{
    public class WhitespaceTokenizer : ITokenizer<string>
    {
        public IEnumerable<string> Tokenize(string input) => input.Split(" ");
    }
}