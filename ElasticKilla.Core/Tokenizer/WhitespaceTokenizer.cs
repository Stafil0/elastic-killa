using System;
using System.Collections.Generic;

namespace ElasticKilla.Core.Tokenizer
{
    public class WhitespaceTokenizer : ITokenizer<string>
    {
        public IEnumerable<string> Tokenize(string input) => string.IsNullOrEmpty(input) 
            ? new string[0]
            : input.Split(" ", StringSplitOptions.RemoveEmptyEntries);
    }
}