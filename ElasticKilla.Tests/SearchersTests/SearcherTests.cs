using System;
using System.Collections.Generic;
using ElasticKilla.Core.Indexes;
using ElasticKilla.Core.Searchers;
using Moq;
using Xunit;

namespace ElasticKilla.Tests.SearchersTests
{
    public class SearcherTests
    {
        [Fact]
        public void Search_GetFromIndex_ReturnResult_NoOtherCalls()
        {
            var query = Guid.NewGuid().ToString();
            var response = new HashSet<string> { Guid.NewGuid().ToString(), Guid.NewGuid().ToString() };

            var index = new Mock<IIndex<string, string>>();
            index.Setup(x => x.Get(query)).Returns(response);

            var searcher = new Searcher<string, string>(index.Object);

            var result = searcher.Search(query);
            Assert.Equal(result, response);

            index.Verify(x => x.Get(query));
            index.VerifyNoOtherCalls();
        } 
    }
}