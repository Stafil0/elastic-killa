using System;
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
        public void Add_Null_DoNothing()
        {
            var dummy = new[] {Guid.NewGuid().ToString()};
            var forward = new Mock<IIndex<string, string>>();
            forward
                .Setup(x => x.Add(It.IsAny<string>(), It.IsAny<IEnumerable<string>>()))
                .Raises(x => x.Added += null, It.IsAny<string>(), dummy);
            
            var inverted = new Mock<IIndex<string, string>>();
            inverted.Setup(x => x.Add(It.IsAny<string>(), It.IsAny<string>()));
            
            using var indexer = new Indexer<string, string>(forward.Object, inverted.Object);
            indexer.Add(null, new string[0]);
            
            forward.Verify(x => x.Add(It.IsAny<string>(), It.IsAny<IEnumerable<string>>()), Times.Never);
            inverted.Verify(x => x.Add(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public void Add_Single_AddedToForward_UpdatedInverted()
        {
            var text = Guid.NewGuid().ToString();
            var query = Guid.NewGuid().ToString();
            
            var forward = new Mock<IIndex<string, string>>();
            forward
                .Setup(x => x.Add(query, text))
                .Raises(x => x.Added += null, query, new [] {text});

            var inverted = new Mock<IIndex<string, string>>();
            inverted.Setup(x => x.Add(text, query));

            using var indexer = new Indexer<string, string>(forward.Object, inverted.Object);
            indexer.Add(query, text);

            forward.Verify(x => x.Add(query, text), Times.Once);
            inverted.Verify(x => x.Add(text, query), Times.Once);
        }
        
        [Fact]
        public void Add_Multiple_AddedToForward_UpdatedInverted()
        {
            var query = Guid.NewGuid().ToString();
            var values = new[] {Guid.NewGuid().ToString(), Guid.NewGuid().ToString()};

            var forward = new Mock<IIndex<string, string>>();
            forward
                .Setup(x => x.Add(query, values))
                .Raises(x => x.Added += null, query, values);

            var inverted = new Mock<IIndex<string, string>>();
            foreach (var value in values)
                inverted.Setup(x => x.Add(value, query));

            using var indexer = new Indexer<string, string>(forward.Object, inverted.Object);
            indexer.Add(query, values);

            forward.Verify(x => x.Add(query, values), Times.Once);
            foreach (var value in values)
                inverted.Verify(x => x.Add(value, query), Times.Once);
        }

        [Fact]
        public void Switch_Null_DoNothing()
        {
            IEnumerable<string> removed;
            var dummy = new[] {Guid.NewGuid().ToString()};
            var forward = new Mock<IIndex<string, string>>();
            forward.Setup(x => x.Get(It.IsAny<string>()));

            forward
                .Setup(x => x.RemoveAll(It.IsAny<string>(), out removed))
                .Returns(true)
                .Raises(x => x.Removed += null, It.IsAny<string>(), dummy);
            
            forward
                .Setup(x => x.Add(It.IsAny<string>(), It.IsAny<IEnumerable<string>>()))
                .Raises(x => x.Added += null, It.IsAny<string>(), dummy);
            
            var inverted = new Mock<IIndex<string, string>>();
            inverted.Setup(x => x.Remove(It.IsAny<string>(), It.IsAny<string>()));
            inverted.Setup(x => x.Add(It.IsAny<string>(), It.IsAny<string>()));
            
            using var indexer = new Indexer<string, string>(forward.Object, inverted.Object);
            indexer.Switch(null, null);
            
            forward.Verify(x => x.Get(It.IsAny<string>()), Times.Never);
            forward.Verify(x => x.RemoveAll(It.IsAny<string>(), out removed), Times.Never);
            forward.Verify(x => x.Add(It.IsAny<string>(), It.IsAny<IEnumerable<string>>()), Times.Never);
            
            inverted.Verify(x => x.Remove(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
            inverted.Verify(x => x.Add(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public void Switch_Single_RemovedFromIndexes_AddedAgain()
        {
            var whoQuery = "who";
            var whoValues = new HashSet<string>(new[] {"who1", "who2", "who3"});

            var withQuery = "with";
            var withValues = new HashSet<string>(new[] {"with1", "with2", "with3"});

            var forward = new Mock<IIndex<string, string>>(MockBehavior.Strict);

            IEnumerable<string> removed;
            var sequence = new MockSequence();
            forward
                .InSequence(sequence)
                .Setup(x => x.Get(whoQuery))
                .Returns(whoValues);

            forward
                .InSequence(sequence)
                .Setup(x => x.Get(withQuery))
                .Returns(withValues);

            forward
                .InSequence(sequence)
                .Setup(x => x.RemoveAll(whoQuery, out removed))
                .Returns(true)
                .Raises(x => x.Removed += null, whoQuery, whoValues);

            forward
                .InSequence(sequence)
                .Setup(x => x.RemoveAll(withQuery, out removed))
                .Returns(true)
                .Raises(x => x.Removed += null, withQuery, withValues);
            
            forward
                .InSequence(sequence)
                .Setup(x => x.Add(whoQuery, withValues))
                .Raises(x => x.Added += null, whoQuery, withValues);

            forward
                .InSequence(sequence)
                .Setup(x => x.Add(withQuery, whoValues))
                .Raises(x => x.Added += null, withQuery, whoValues);

            var inverted = new Mock<IIndex<string, string>>(MockBehavior.Strict);

            foreach (var value in whoValues)
            {
                var invertedSequence = new MockSequence();
                inverted
                    .InSequence(invertedSequence)
                    .Setup(x => x.Remove(value, whoQuery))
                    .Returns(true);
                
                inverted
                    .InSequence(invertedSequence)
                    .Setup(x => x.Add(value, withQuery));
            }

            foreach (var value in withValues)
            {
                var invertedSequence = new MockSequence();
                inverted
                    .InSequence(invertedSequence)
                    .Setup(x => x.Remove(value, withQuery))
                    .Returns(true);

                inverted
                    .InSequence(invertedSequence)
                    .Setup(x => x.Add(value, whoQuery));
            }

            using var indexer = new Indexer<string, string>(forward.Object, inverted.Object);
            indexer.Switch(whoQuery, withQuery);

            forward.VerifyAll();
            inverted.VerifyAll();
        }

        [Fact]
        public void Update_Null_DoNothing()
        {
            var dummy = new[] {Guid.NewGuid().ToString()};
            var forward = new Mock<IIndex<string, string>>();
            forward.Setup(x => x.Get(It.IsAny<string>()));

            forward
                .Setup(x => x.Remove(It.IsAny<string>(), It.IsAny<IEnumerable<string>>()))
                .Returns(true)
                .Raises(x => x.Removed += null, It.IsAny<string>(), dummy);

            forward
                .Setup(x => x.Add(It.IsAny<string>(), It.IsAny<IEnumerable<string>>()))
                .Raises(x => x.Added += null, It.IsAny<string>(), dummy);
            
            var inverted = new Mock<IIndex<string, string>>();
            inverted.Setup(x => x.Add(It.IsAny<string>(), It.IsAny<string>()));
            inverted.Setup(x => x.Remove(It.IsAny<string>(), It.IsAny<string>()));
            
            using var indexer = new Indexer<string, string>(forward.Object, inverted.Object);
            indexer.Update(null, null);
            
            forward.Verify(x => x.Get(It.IsAny<string>()), Times.Never);
            forward.Verify(x => x.Remove(It.IsAny<string>(), It.IsAny<IEnumerable<string>>()), Times.Never);
            forward.Verify(x => x.Add(It.IsAny<string>(), It.IsAny<IEnumerable<string>>()), Times.Never);
            
            inverted.Verify(x => x.Add(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
            inverted.Verify(x => x.Remove(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public void Update_RemovedOnlyOld_AddedOnlyNew()
        {
            var query = "query";
            var before = new HashSet<string> {"q3", "q4", "q5", "q6", "q7"};
            var after = new[] {"q1", "q2", "q3", "q4"};

            var removed = new HashSet<string>(before);
            removed.ExceptWith(after);
            
            var added = new HashSet<string>(after);
            added.ExceptWith(before);
            
            var forward = new Mock<IIndex<string, string>>(MockBehavior.Strict);

            var sequence = new MockSequence();
            forward
                .InSequence(sequence)
                .Setup(x => x.Get(query))
                .Returns(before);

            forward
                .InSequence(sequence)
                .Setup(x => x.Remove(query, removed))
                .Returns(true)
                .Raises(x => x.Removed += null, query, removed);

            forward
                .Setup(x => x.Add(query, added))
                .Raises(x => x.Added += null, query, added);

            var inverted = new Mock<IIndex<string, string>>(MockBehavior.Strict);

            foreach (var remove in removed)
                inverted.Setup(x => x.Remove(remove, query)).Returns(true);
            
            foreach (var add in added)
                inverted.Setup(x => x.Add(add, query));

            using var indexer = new Indexer<string, string>(forward.Object, inverted.Object);
            indexer.Update(query, after);
            
            forward.VerifyAll();
            inverted.VerifyAll();
        }

        [Fact]
        public void Remove_Null_DoNothing()
        {
            IEnumerable<string> removed;
            var dummy = new[] {Guid.NewGuid().ToString()};
            var forward = new Mock<IIndex<string, string>>();
            forward
                .Setup(x => x.RemoveAll(It.IsAny<string>(), out removed))
                .Returns(true)
                .Raises(x => x.Removed += null, It.IsAny<string>(), dummy);

            forward
                .Setup(x => x.Remove(It.IsAny<string>(), It.IsAny<string>()))
                .Returns(true)
                .Raises(x => x.Removed += null, It.IsAny<string>(), dummy);

            forward
                .Setup(x => x.Remove(It.IsAny<string>(), It.IsAny<IEnumerable<string>>()))
                .Returns(true)
                .Raises(x => x.Removed += null, It.IsAny<string>(), dummy);

            var inverted = new Mock<IIndex<string, string>>();
            inverted.Setup(x => x.Remove(It.IsAny<string>(), It.IsAny<string>()));

            using var indexer = new Indexer<string, string>(forward.Object, inverted.Object);
            indexer.Remove(null, null);
            indexer.Remove(null);

            forward.Verify(x => x.RemoveAll(It.IsAny<string>(), out removed), Times.Never);
            forward.Verify(x => x.Remove(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
            forward.Verify(x => x.Remove(It.IsAny<string>(), It.IsAny<IEnumerable<string>>()), Times.Never);
            
            inverted.Verify(x => x.Remove(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public void Remove_OnlyWithKey_RemoveWholeIndex_RemovedFromInverted()
        {
            IEnumerable<string> removed;
            var query = "query";
            var index = new[] {Guid.NewGuid().ToString(), Guid.NewGuid().ToString(), Guid.NewGuid().ToString()};
            var forward = new Mock<IIndex<string, string>>(MockBehavior.Strict);
            forward
                .Setup(x => x.RemoveAll(query, out removed))
                .Returns(true)
                .Raises(x => x.Removed += null, query, index);

            var inverted = new Mock<IIndex<string, string>>(MockBehavior.Strict);
            foreach (var i in index)
                inverted.Setup(x => x.Remove(i, query)).Returns(true);

            using var indexer = new Indexer<string, string>(forward.Object, inverted.Object);
            indexer.Remove(query);

            forward.VerifyAll();
            inverted.VerifyAll();
        }

        [Fact]
        public void Remove_ByValues_RemovedFromForward_RemovedFromInverted()
        {
            var query = "query";
            var index = new[] {Guid.NewGuid().ToString(), Guid.NewGuid().ToString(), Guid.NewGuid().ToString()};
            var forward = new Mock<IIndex<string, string>>(MockBehavior.Strict);
            forward
                .Setup(x => x.Remove(query, index))
                .Returns(true)
                .Raises(x => x.Removed += null, query, index);

            var inverted = new Mock<IIndex<string, string>>(MockBehavior.Strict);
            foreach (var i in index)
                inverted.Setup(x => x.Remove(i, query)).Returns(true);

            using var indexer = new Indexer<string, string>(forward.Object, inverted.Object);
            indexer.Remove(query, index);

            forward.VerifyAll();
            inverted.VerifyAll();
        }
    }
}