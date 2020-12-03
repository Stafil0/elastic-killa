using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ElasticKilla.Core.Analyzers;
using ElasticKilla.Core.Indexers;
using ElasticKilla.Core.Searchers;
using ElasticKilla.Core.Tokenizer;
using ElasticKilla.Tests.TestExtensions;
using Moq;
using Xunit;

namespace ElasticKilla.Tests.Analyzers
{
    public class FileAnalyzerTests
    {
        [Fact]
        public async Task OnSubscribe_OneFile_AddToIndex()
        {
            var searcher = new Mock<ISearcher<string, string>>();
            var indexer = new Mock<IIndexer<string, string>>();
            var tokenizer = new Mock<ITokenizer<string>>();
            using var analyzer = new FileAnalyzer(tokenizer.Object, searcher.Object, indexer.Object);

            using var tmp = new TempFile();
            await analyzer.Subscribe(tmp.FolderName, tmp.FileName);
            indexer.Verify(x => x.Add(It.IsAny<string>(), It.IsAny<IEnumerable<string>>()), Times.Once);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(10)]
        [InlineData(100)]
        [InlineData(1000)]
        public async Task OnSubscribe_MultipleFiles_AddToIndexMultiple(int filesCount)
        {
            var searcher = new Mock<ISearcher<string, string>>();
            var indexer = new Mock<IIndexer<string, string>>();
            var tokenizer = new Mock<ITokenizer<string>>();
            using var analyzer = new FileAnalyzer(tokenizer.Object, searcher.Object, indexer.Object);

            using var tmp = new TempFolder(filesCount);
            await analyzer.Subscribe(tmp.FolderPath);
            indexer.Verify(x => x.Add(It.IsAny<string>(), It.IsAny<IEnumerable<string>>()), Times.Exactly(filesCount));
        }

        [Theory]
        [InlineData(".tmp", 0, ".vjuh", 20)]
        [InlineData(".tmp", 10, ".vjuh", 20)]
        [InlineData(".tmp", 100, ".vjuh", 200)]
        [InlineData(".tmp", 1000, ".vjuh", 2000)]
        public async Task OnSubscribe_MultipleFilesWithPattern_AddToIndexMultiple(
            string firstPattern, int firstFilesGroupCount,
            string secondPattern, int secondFilesGroupCount)
        {
            var searcher = new Mock<ISearcher<string, string>>();
            var indexer = new Mock<IIndexer<string, string>>();
            var tokenizer = new Mock<ITokenizer<string>>();
            using var analyzer = new FileAnalyzer(tokenizer.Object, searcher.Object, indexer.Object);
            
            var masks = new Dictionary<string, int>
            {
                { firstPattern, firstFilesGroupCount },
                { secondPattern, secondFilesGroupCount }
            };

            using var tmp = new TempFolder(masks);

            foreach (var pattern in masks.Keys.Select(extension => $"*{extension}"))
                await analyzer.Subscribe(tmp.FolderPath, pattern);

            foreach (var (extension, count) in masks)
                indexer.Verify(x => x.Add(
                        It.IsRegex($".*({extension})$"),
                        It.IsAny<IEnumerable<string>>()),
                    Times.Exactly(count));
        }

        [Fact]
        public async Task OnSubscribe_EmptyFolder_DoNothing()
        {
            var searcher = new Mock<ISearcher<string, string>>();
            var indexer = new Mock<IIndexer<string, string>>();
            var tokenizer = new Mock<ITokenizer<string>>();
            using var analyzer = new FileAnalyzer(tokenizer.Object, searcher.Object, indexer.Object);

            using var tmp = new TempFolder(0);
            await analyzer.Subscribe(tmp.FolderPath);
            indexer.Verify(x => x.Add(It.IsAny<string>(), It.IsAny<IEnumerable<string>>()), Times.Never);
        }

        [Fact]
        public async Task OnSubscribe_BadPath_DoNothing()
        {
            var searcher = new Mock<ISearcher<string, string>>();
            var indexer = new Mock<IIndexer<string, string>>();
            var tokenizer = new Mock<ITokenizer<string>>();
            using var analyzer = new FileAnalyzer(tokenizer.Object, searcher.Object, indexer.Object);

            var tmp = Path.GetRandomFileName();
            await analyzer.Subscribe(tmp);
            indexer.Verify(x => x.Add(It.IsAny<string>(), It.IsAny<IEnumerable<string>>()), Times.Never);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(10)]
        [InlineData(100)]
        [InlineData(1000)]
        public async Task OnSubscribe_StartingDeleteFiles_AddThenDelete(int filesCount)
        {
            var searcher = new Mock<ISearcher<string, string>>();
            var tokenizer = new Mock<ITokenizer<string>>();
            var indexer = new Mock<IIndexer<string, string>>(MockBehavior.Strict);

            var analyzer = new FileAnalyzer(tokenizer.Object, searcher.Object, indexer.Object);

            Task task;
            string folder;
            var files = new List<string>();
            using (var tmp = new TempFolder(filesCount))
            {
                folder = tmp.FolderPath;
                files.AddRange(tmp.Files);
                foreach (var file in files)
                {
                    var sequence = new MockSequence();
                    indexer.InSequence(sequence).Setup(x => x.Add(file, It.IsAny<IEnumerable<string>>()));
                    indexer.InSequence(sequence).Setup(x => x.Remove(file));
                }

                task = Task.Run(async () => await analyzer.Subscribe(folder));
            }

            await task;

            foreach (var file in files)
            {
                indexer.Verify(x => x.Add(file, It.IsAny<IEnumerable<string>>()), Times.AtMostOnce);
                indexer.Verify(x => x.Remove(file), Times.AtMostOnce);
            }
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(10)]
        [InlineData(100)]
        [InlineData(1000)]
        public async Task OnSubscribe_Unsubscribe_AddThenDelete(int filesCount)
        {
            var searcher = new Mock<ISearcher<string, string>>();
            var tokenizer = new Mock<ITokenizer<string>>();
            var indexer = new Mock<IIndexer<string, string>>(MockBehavior.Strict);

            var analyzer = new FileAnalyzer(tokenizer.Object, searcher.Object, indexer.Object);

            using var tmp = new TempFolder(filesCount);
            var folder = tmp.FolderPath;

            foreach (var file in tmp.Files)
            {
                var sequence = new MockSequence();
                indexer.InSequence(sequence).Setup(x => x.Add(file, It.IsAny<IEnumerable<string>>()));
                indexer.InSequence(sequence).Setup(x => x.Remove(file));
            }

            var subscribe = Task.Run(() => analyzer.Subscribe(folder));

            // Дадим время подписаться раньше, чем отписаться.
            await Task.Delay(10);

            var unsubscribe = Task.Run(() => analyzer.Unsubscribe(folder));

            await Task.WhenAll(subscribe, unsubscribe);

            foreach (var file in tmp.Files)
            {
                indexer.Verify(x => x.Add(file, It.IsAny<IEnumerable<string>>()), Times.AtMostOnce);
                indexer.Verify(x => x.Remove(file), Times.AtMostOnce);
            }
        }

        [Fact]
        public async Task OnSubscribe_CheckProgress_MustBeIndexing()
        {
            var guids = Generators.Generate(1000, () => Guid.NewGuid().ToString()).ToArray();
            var text = string.Join(' ', guids);
            
            var searcher = new Mock<ISearcher<string, string>>();
            var indexer = new Mock<IIndexer<string, string>>();
            var tokenizer = new Mock<ITokenizer<string>>();
            tokenizer.Setup(x => x.Tokenize(text)).Returns(guids);

            using var analyzer = new FileAnalyzer(tokenizer.Object, searcher.Object, indexer.Object);
            using var tmp = new TempFolder(1, () => text);

            var task = analyzer.Subscribe(tmp.FolderPath);

            Assert.True(analyzer.IsIndexing);

            await task;

            Assert.False(analyzer.IsIndexing);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(10)]
        [InlineData(100)]
        [InlineData(1000)]
        public async Task OnSubscribe_GetSubscriptions_MustBeAtLeastSomething(int subscriptionsCount)
        {
            var searcher = new Mock<ISearcher<string, string>>();
            var indexer = new Mock<IIndexer<string, string>>();
            var tokenizer = new Mock<ITokenizer<string>>();

            var analyzer = new FileAnalyzer(tokenizer.Object, searcher.Object, indexer.Object);

            var tasks = new List<Task>();
            for (var i = 0; i < subscriptionsCount; i++)
            {
                tasks.Add(Task.Run(() =>
                {
                    using var tmp = new TempFolder(1);
                    analyzer.Subscribe(tmp.FolderPath);
                }));
            }

            // Ждем хотя бы одной подписки.
            await Task.WhenAny(tasks);
            Assert.InRange(analyzer.Subscriptions.Count, 1, subscriptionsCount);

            await Task.WhenAll(tasks);
            Assert.Equal(subscriptionsCount, analyzer.Subscriptions.Count);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(10)]
        [InlineData(100)]
        [InlineData(1000)]
        public async Task OnSubscribe_StartDelayedSearching_GetResultAfterIndexing(int filesCount)
        {
            var random = new Random();
            var guids = Generators.Generate(5, () => Guid.NewGuid().ToString()).ToArray();
            var text = string.Join(' ', guids);

            var searcher = new Mock<ISearcher<string, string>>();
            var indexer = new Mock<IIndexer<string, string>>(MockBehavior.Strict);

            var tokenizer = new Mock<ITokenizer<string>>();
            tokenizer.Setup(x => x.Tokenize(text)).Returns(guids);

            using var tmp = new TempFolder(filesCount, () => text);
            var path = tmp.FolderPath;
            var analyzer = new FileAnalyzer(tokenizer.Object, searcher.Object, indexer.Object);

            var guid = guids[random.Next(guids.Length)];

            var sequence = new MockSequence();
            foreach (var _ in tmp.Files)
                indexer.InSequence(sequence).Setup(x => x.Add(It.IsAny<string>(), It.IsAny<IEnumerable<string>>()));
            searcher.InSequence(sequence).Setup(x => x.Search(guid));

            var subscribe = Task.Run(async () => await analyzer.Subscribe(path));

            await Task.Delay(1000);

            var search = Task.Run(async () => await analyzer.DelayedSearch(guid));

            await search;
            await subscribe;

            indexer.Verify(x => x.Add(It.IsAny<string>(), It.IsAny<IEnumerable<string>>()), Times.Exactly(tmp.Files.Count));
            searcher.Verify(x => x.Search(guid), Times.Once);
        }

        [Theory]
        [InlineData(0, 1)]
        [InlineData(0, 5)]
        [InlineData(0, 100)]
        [InlineData(10, 1)]
        [InlineData(10, 5)]
        [InlineData(10, 100)]
        public async Task AfterSubscribe_AddFiles_AddToIndex(int initCount, int count)
        {
            var searcher = new Mock<ISearcher<string, string>>();
            var tokenizer = new Mock<ITokenizer<string>>();
            var indexer = new Mock<IIndexer<string, string>>();

            using var analyzer = new FileAnalyzer(tokenizer.Object, searcher.Object, indexer.Object);

            using var tmp = new TempFolder(initCount);
            foreach (var file in tmp.Files)
            {
                indexer.Setup(x => x.Add(file, It.IsAny<IEnumerable<string>>()));
            }

            await analyzer.Subscribe(tmp.FolderPath);

            tmp.CreateFiles(count);

            indexer.Verify(x => x.Add(It.IsAny<string>(), It.IsAny<IEnumerable<string>>()), Times.Exactly(initCount + count));

            foreach (var file in tmp.Files)
            {
                indexer.Verify(x => x.Add(file, It.IsAny<IEnumerable<string>>()), Times.Once);
            }
        }

        [Theory]
        [InlineData(10, 1)]
        [InlineData(10, 5)]
        [InlineData(10, 10)]
        public async Task AfterSubscribe_RenameFile_ReIndex(int initCount, int renameCount)
        {
            var searcher = new Mock<ISearcher<string, string>>();
            var tokenizer = new Mock<ITokenizer<string>>();
            var indexer = new Mock<IIndexer<string, string>>();

            using var analyzer = new FileAnalyzer(tokenizer.Object, searcher.Object, indexer.Object);

            using var tmp = new TempFolder(initCount);
            foreach (var file in tmp.Files)
            {
                indexer.Setup(x => x.Add(file, It.IsAny<IEnumerable<string>>()));
            }

            await analyzer.Subscribe(tmp.FolderPath);

            indexer.Verify(x => x.Add(It.IsAny<string>(), It.IsAny<IEnumerable<string>>()), Times.Exactly(initCount));
            foreach (var file in tmp.Files)
            {
                indexer.Verify(x => x.Add(file, It.IsAny<IEnumerable<string>>()), Times.Once);
            }

            var renamed = new List<(string, string)>();

            for (var i = 0; i < renameCount; i++)
            {
                var newFile = Path.GetRandomFileName();
                var oldPath = tmp.RenameFile(newFile);
                var newPath = Path.Join(tmp.FolderPath, newFile);
                renamed.Add((oldPath, newPath));
            }

            // Даем время сработать событиям переименования.
            await Task.Delay(1000);

            indexer.Verify(x => x.Switch(It.IsAny<string>(), It.IsAny<string>()), Times.Exactly(renameCount));
            foreach (var (oldPath, newPath) in renamed)
            {
                indexer.Verify(x => x.Switch(oldPath, newPath), Times.Once);
            }
        }


        [Fact]
        public void AfterSubscribe_DeleteFile_Reindex()
        {
            
        }
        
        [Fact]
        public void AfterSubscribe_Search_GetFiles()
        {
            
        }
        
        [Fact]
        public void AfterSubscribe_GetSubscriptions_MustBeNotEmpty()
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

        [Fact]
        public void AfterUnsubscribe_GetSubscriptions_MustBeEmpty()
        {
            
        }
    }
}