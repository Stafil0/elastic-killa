using System.Collections.Generic;
using ElasticKilla.Core.Indexers;
using ElasticKilla.Core.Indexes;
using Moq;
using Xunit;

namespace ElasticKilla.Tests.IndexerTests
{
    public class IndexerTests
    {
        [Fact]
        public void AddNull_DoNothing()
        {
            var forward = new Mock<IIndex<string, string>>();
            forward.Setup(x => x.Add(It.IsAny<string>(), It.IsAny<IEnumerable<string>>()));
            
            var inverted = new Mock<IIndex<string, string>>();
            inverted.Setup(x => x.Add(It.IsAny<string>(), It.IsAny<string>()));
            
            var indexer = new Indexer<string, string>(forward.Object, inverted.Object);
            indexer.Add(null, new string[0]);
            
            forward.Verify(x => x.Add(It.IsAny<string>(), It.IsAny<IEnumerable<string>>()), Times.Never);
            inverted.Verify(x => x.Add(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public void AddIndex_AddedToForward_UpdatedInverted()
        {
            
        }
        
        [Fact]
        public void AddIndexMultiple_AddedToForward_UpdatedInverted()
        {
            
        }

        [Fact]
        public void SwitchNull_DoNothing()
        {
            
        }

        [Fact]
        public void SwitchIndexes_RemovedFromIndexes_AddedAgain()
        {
            
        }

        [Fact]
        public void UpdateNull_DoNothing()
        {
            
        }

        [Fact]
        public void UpdateIndex_RemovedOnlyOld_AddedOnlyNew()
        {
            
        }

        [Fact]
        public void RemoveNull_DoNothing()
        {
            
        }

        [Fact]
        public void RemoveOnlyWithKey_RemoveWholeIndex()
        {
            
        }

        [Fact]
        public void RemoveByValues_RemoveOnlyValues()
        {
            
        }
    }
}