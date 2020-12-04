using System.Collections;
using System.Collections.Generic;
using ElasticKilla.Core.Tokenizer;
using Xunit;

namespace ElasticKilla.Tests.TokenizersTests
{
    public class WhitespaceTokenizerData : IEnumerable<object[]>
    {
        public IEnumerator<object[]> GetEnumerator()
        {
            yield return new object[] {null, new string[0]};
            yield return new object[] {"", new string[0]};
            yield return new object[] {"aaa", new [] {"aaa"}};
            yield return new object[] {"aaa.", new [] {"aaa."}};
            yield return new object[] {"a. b", new [] {"a.", "b"}};
            yield return new object[] {"a . b", new [] {"a", ".", "b"}};
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    public class WhitespaceTokenizerTests
    {
        [Theory]
        [ClassData(typeof(WhitespaceTokenizerData))]
        public void GivenString_Split_MustTokenizeCorrectly(string input, IEnumerable<string> expected)
        {
            var tokenizer = new WhitespaceTokenizer();
            var output = tokenizer.Tokenize(input);

            Assert.Equal(expected, output);
        }
    }
}