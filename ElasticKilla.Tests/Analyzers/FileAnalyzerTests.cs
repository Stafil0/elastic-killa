using System.IO.Abstractions;
using ElasticKilla.Core.Analyzers;
using ElasticKilla.Core.Indexers;
using ElasticKilla.Core.Searchers;
using ElasticKilla.Core.Tokenizer;
using Moq;
using Xunit;

namespace ElasticKilla.Tests.Analyzers
{
    public class FileAnalyzerTests
    {
        [Fact]
        public void OnSubscribe_OneFile_AddToIndex()
        {
            var searcher = new Mock<ISearcher<string, string>>();
            var indexer = new Mock<IIndexer<string, string>>();
            var tokenizer = new Mock<ITokenizer<string>>();

            

            var analyzer = new FileAnalyzer(tokenizer.Object, searcher.Object, indexer.Object);
        }

        [Fact]
        public void OnSubscribe_MultipleFiles_AddToIndexInQueue()
        {
        }

        [Fact]
        public void OnSubscribe_MultipleFilesWithPattern_AddToIndexInQueue()
        {
        }

        [Fact]
        public void OnSubscribe_EmptyFolder_DoNothing()
        {
        }
        
        [Fact]
        public void OnSubscribe_BadPath_DoNothing()
        {
        }

        [Fact]
        public void OnSubscribe_StartingDeleteFiles_AddThenDeleteInQueue()
        {
            
        }

        [Fact]
        public void AfterSubscribe_AddFile_AddToIndex()
        {
        }

        [Fact]
        public void AfterSubscribe_AddFiles_AddToIndexInQueue()
        {
        }

        [Fact]
        public void AfterSubscribe_RenameFile_Reindex()
        {
            
        }

        [Fact]
        public void AfterSubscribe_RenameFiles_ReindexInQueue()
        {
            
        }

        [Fact]
        public void AfterSubscribe_DeleteFile_Reindex()
        {
            
        }

        [Fact]
        public void AfterSubscribe_DeleteFiles_ReindexInQueue()
        {
            
        }

        [Fact]
        public void OnUnsubscribe_WatchedPath_RemoveIndex()
        {
            
        }

        [Fact]
        public void OnUnsubscribe_NotExistingWatch_DoNothing()
        {
            
        }

        [Fact]
        public void OnUnsubscribe_SubscribeAgain_RemoveThenAddToIndexInQueue()
        {
            
        }

        [Fact]
        public void OnUnsubscribe_StartChangingFiles_ChangeIndexInQueue()
        {
            
        }

        [Fact]
        public void AfterUnsubscribe_StartChangingFiles_DoNothing()
        {
            
        }
    }
}