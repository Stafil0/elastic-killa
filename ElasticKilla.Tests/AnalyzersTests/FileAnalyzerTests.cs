using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ElasticKilla.Core.Analyzers;
using ElasticKilla.Core.Extensions;
using ElasticKilla.Core.Indexers;
using ElasticKilla.Core.Searchers;
using ElasticKilla.Core.Tokenizer;
using ElasticKilla.Tests.TestExtensions;
using Moq;
using Xunit;

namespace ElasticKilla.Tests.AnalyzersTests
{
    public class OnSubscribeFileAnalyzerTests
    {
        [Fact]
        public async Task OnSubscribe_OneFile_AddToIndex()
        {
            var searcher = new Mock<ISearcher<string, string>>();
            var indexer = new Mock<IIndexer<string, string>>();
            var tokenizer = new Mock<ITokenizer<string>>();
            var analyzer = new FileAnalyzer(tokenizer.Object, searcher.Object, indexer.Object);

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
            var analyzer = new FileAnalyzer(tokenizer.Object, searcher.Object, indexer.Object);

            using var tmp = new TempFolder(filesCount);
            await analyzer.Subscribe(tmp.FolderPath);
            indexer.Verify(x => x.Add(It.IsAny<string>(), It.IsAny<IEnumerable<string>>()), Times.Exactly(filesCount));
        }

        [Theory]
        [InlineData(".TMP", 0, ".VJUH", 20)]
        [InlineData(".TMP", 10, ".VJUH", 20)]
        [InlineData(".TMP", 100, ".VJUH", 200)]
        [InlineData(".TMP", 1000, ".VJUH", 2000)]
        public async Task OnSubscribe_MultipleFilesWithPattern_AddToIndexMultiple(
            string firstPattern, int firstFilesGroupCount,
            string secondPattern, int secondFilesGroupCount)
        {
            var searcher = new Mock<ISearcher<string, string>>();
            var indexer = new Mock<IIndexer<string, string>>();
            var tokenizer = new Mock<ITokenizer<string>>();
            var analyzer = new FileAnalyzer(tokenizer.Object, searcher.Object, indexer.Object);

            var masks = new Dictionary<string, int>
            {
                {firstPattern, firstFilesGroupCount},
                {secondPattern, secondFilesGroupCount}
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
            var analyzer = new FileAnalyzer(tokenizer.Object, searcher.Object, indexer.Object);

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
            var analyzer = new FileAnalyzer(tokenizer.Object, searcher.Object, indexer.Object);

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
        [InlineData(10)]
        [InlineData(100)]
        [InlineData(1000)]
        public async Task OnSubscribe_StartingDeleteFolders_AddSomething(int subscriptionsCount)
        {
            var searcher = new Mock<ISearcher<string, string>>();
            var indexer = new Mock<IIndexer<string, string>>();
            var tokenizer = new Mock<ITokenizer<string>>();

            var analyzer = new FileAnalyzer(tokenizer.Object, searcher.Object, indexer.Object);

            var tasks = new List<Task>();
            for (var i = 0; i < subscriptionsCount; i++)
            {
                using var tmp = new TempFolder(1);
                var folder = tmp.FolderPath;
                tasks.Add(Task.Run(async () => await analyzer.Subscribe(folder)));
            }

            await Task.WhenAll(tasks);
            await Task.Delay(5 * subscriptionsCount);
            
            Assert.InRange(analyzer.Subscriptions.Count, subscriptionsCount > 0 ? 1 : 0, subscriptionsCount);
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

        [Theory]
        [InlineData(1)]
        [InlineData(10)]
        [InlineData(100)]
        [InlineData(1000)]
        public async Task OnSubscribe_CheckProgress_MustBeIndexing(int filesCount)
        {
            var guids = Generators.Generate(1000, () => Guid.NewGuid().ToString()).ToArray();
            var text = string.Join(' ', guids);

            var searcher = new Mock<ISearcher<string, string>>();
            var indexer = new Mock<IIndexer<string, string>>();
            var tokenizer = new Mock<ITokenizer<string>>();
            tokenizer.Setup(x => x.Tokenize(text)).Returns(guids);

            var analyzer = new FileAnalyzer(tokenizer.Object, searcher.Object, indexer.Object);
            using var tmp = new TempFolder(filesCount, () => text);

            var task = analyzer.Subscribe(tmp.FolderPath);

            Assert.True(analyzer.IsIndexing);

            await task;
            await Task.Delay(5 * filesCount);

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

            using var analyzer = new FileAnalyzer(tokenizer.Object, searcher.Object, indexer.Object);

            var tasks = new List<Task>();
            for (var i = 0; i < subscriptionsCount; i++)
            {
                tasks.Add(Task.Run(async () =>
                {
                    using var tmp = new TempFolder(1);
                    await analyzer.Subscribe(tmp.FolderPath);
                }));
            }

            // Ждем хотя бы одной подписки.
            await Task.WhenAny(tasks);
            Assert.InRange(analyzer.Subscriptions.Count, 1, subscriptionsCount);

            await Task.WhenAll(tasks);
            Assert.Equal(subscriptionsCount, analyzer.Subscriptions.Count);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(10)]
        [InlineData(100)]
        [InlineData(1000)]
        [InlineData(5000)]
        public async Task OnSubscribe_Search_GetResultImmediately(int filesCount)
        {
            var random = new Random();
            var guids = Generators.Generate(5, () => Guid.NewGuid().ToString()).ToArray();
            var text = string.Join(' ', guids);

            var tokenizer = new Mock<ITokenizer<string>>();
            tokenizer.Setup(x => x.Tokenize(text)).Returns(guids);

            using var tmp = new TempFolder(filesCount, () => text);
            var path = tmp.FolderPath;
            var analyzer = new FileAnalyzer(tokenizer.Object);

            var guid = guids[random.Next(guids.Length)];

            var subscribe = Task.Run(async () => await analyzer.Subscribe(path));

            // Дадим попдисаться.
            await Task.Delay(1000);

            var search = analyzer.Search(guid).ToList();
            Assert.InRange(search.Count, filesCount > 0 ? 1 : 0, filesCount);

            await subscribe;
            await Task.Delay(1000);

            search = analyzer.Search(guid).ToList();
            Assert.Equal(filesCount, search.Count);
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

            var searcher = new Mock<ISearcher<string, string>>(MockBehavior.Strict);
            var indexer = new Mock<IIndexer<string, string>>(MockBehavior.Strict);

            var tokenizer = new Mock<ITokenizer<string>>();
            tokenizer.Setup(x => x.Tokenize(text)).Returns(guids);

            using var tmp = new TempFolder(filesCount, () => text);
            var path = tmp.FolderPath;
            var analyzer = new FileAnalyzer(tokenizer.Object, searcher.Object, indexer.Object);

            var guid = guids[random.Next(guids.Length)];

            var sequence = new MockSequence();
            indexer.InSequence(sequence).Setup(x => x.Add(It.IsAny<string>(), It.IsAny<IEnumerable<string>>()));
            searcher.InSequence(sequence).Setup(x => x.Search(guid)).Returns(tmp.Files);

            var subscribe = Task.Run(async () => await analyzer.Subscribe(path));

            await Task.Delay(100);

            var search = Task.Run(async () => await analyzer.DelayedSearch(guid));

            await search;
            await subscribe;

            indexer.Verify(x => x.Add(It.IsAny<string>(), It.IsAny<IEnumerable<string>>()),
                Times.Exactly(tmp.Files.Count));
            searcher.Verify(x => x.Search(guid), Times.Once);
        }
    }

    public class AfterSubscribeFileAnalyzerTests
    {
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

            var analyzer = new FileAnalyzer(tokenizer.Object, searcher.Object, indexer.Object);

            using var tmp = new TempFolder(initCount);
            foreach (var file in tmp.Files)
            {
                indexer.Setup(x => x.Add(file, It.IsAny<IEnumerable<string>>()));
            }

            await analyzer.Subscribe(tmp.FolderPath);

            tmp.CreateFiles(count);
            await Task.Delay(10 * count);

            indexer.Verify(x => x.Add(It.IsAny<string>(), It.IsAny<IEnumerable<string>>()),
                Times.Exactly(initCount + count));

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

            var analyzer = new FileAnalyzer(tokenizer.Object, searcher.Object, indexer.Object);

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
                var newPath = PathExtensions.NormalizePath(Path.Join(tmp.FolderPath, newFile));
                renamed.Add((oldPath, newPath));
            }

            // Даем время сработать событиям переименования.
            await Task.Delay(10 * renameCount);

            indexer.Verify(x => x.Switch(It.IsAny<string>(), It.IsAny<string>()), Times.Exactly(renameCount));
            foreach (var (oldPath, newPath) in renamed)
            {
                indexer.Verify(x => x.Switch(oldPath, newPath), Times.Once);
            }
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(10)]
        [InlineData(100)]
        [InlineData(1000)]
        public async Task AfterSubscribe_DeleteFile_ReIndex(int filesCount)
        {
            var searcher = new Mock<ISearcher<string, string>>();
            var tokenizer = new Mock<ITokenizer<string>>();
            var indexer = new Mock<IIndexer<string, string>>(MockBehavior.Strict);

            var analyzer = new FileAnalyzer(tokenizer.Object, searcher.Object, indexer.Object);

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

                await Task.Run(async () => await analyzer.Subscribe(folder));
            }

            // Дадим всем событиям на удаление сработать.
            await Task.Delay(5 * filesCount);

            foreach (var file in files)
            {
                indexer.Verify(x => x.Add(file, It.IsAny<IEnumerable<string>>()), Times.Once);
                indexer.Verify(x => x.Remove(file), Times.Once);
            }
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(10)]
        [InlineData(100)]
        [InlineData(1000)]
        [InlineData(5000)]
        public async Task AfterSubscribe_Search_FindAll(int filesCount)
        {
            var random = new Random();
            var guids = Generators.Generate(5, () => Guid.NewGuid().ToString()).ToArray();
            var text = string.Join(' ', guids);

            var tokenizer = new Mock<ITokenizer<string>>();
            tokenizer.Setup(x => x.Tokenize(text)).Returns(guids);

            using var tmp = new TempFolder(filesCount, () => text);
            var path = tmp.FolderPath;
            var analyzer = new FileAnalyzer(tokenizer.Object);

            var guid = guids[random.Next(guids.Length)];

            await Task.Run(async () => await analyzer.Subscribe(path));
            await Task.Delay(filesCount);

            var search = analyzer.Search(guid).ToList();
            Assert.Equal(filesCount, search.Count);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(10)]
        [InlineData(100)]
        [InlineData(1000)]
        public async Task AfterSubscribe_GetSubscriptions_MustReturnAll(int subscriptionsCount)
        {
            var searcher = new Mock<ISearcher<string, string>>();
            var indexer = new Mock<IIndexer<string, string>>();
            var tokenizer = new Mock<ITokenizer<string>>();

            var analyzer = new FileAnalyzer(tokenizer.Object, searcher.Object, indexer.Object);

            var tasks = new List<Task>();
            for (var i = 0; i < subscriptionsCount; i++)
            {
                tasks.Add(Task.Run(async () =>
                {
                    using var tmp = new TempFolder(1);
                    await analyzer.Subscribe(tmp.FolderPath);
                }));
            }

            await Task.WhenAll(tasks);
            Assert.Equal(subscriptionsCount, analyzer.Subscriptions.Count);
        }

        [Theory]
        [InlineData(10, 5, 10, 5)]
        [InlineData(100, 50, 100, 50)]
        [InlineData(1000, 500, 1000, 500)]
        public async Task AfterSubscribeMultiple_ChangeFiles_ReIndex(
            int firstInitCount, int firstChangeCount,
            int secondInitCount, int secondChangeCount)
        {
            var searcher = new Mock<ISearcher<string, string>>();
            var tokenizer = new Mock<ITokenizer<string>>();
            var indexer = new Mock<IIndexer<string, string>>();

            var analyzer = new FileAnalyzer(tokenizer.Object, searcher.Object, indexer.Object);

            using var firstTemp = new TempFolder(firstInitCount);
            using var secondTemp = new TempFolder(secondInitCount);

            var firstFolder = firstTemp.FolderPath;
            var secondFolder = secondTemp.FolderPath;
            var first = Task.Run(async () => await analyzer.Subscribe(firstFolder));
            var second = Task.Run(async () => await analyzer.Subscribe(secondFolder));
            await Task.WhenAll(first, second);
            await Task.Delay(1000);

            indexer.Verify(x => x.Add(It.IsAny<string>(), It.IsAny<IEnumerable<string>>()),
                Times.Exactly(firstInitCount + secondInitCount));

            var renamed = new List<(string, string)>();

            static (string, string) Rename(TempFolder folder)
            {
                var newFile = Path.GetRandomFileName();
                var oldPath = folder.RenameFile(newFile);
                var newPath = PathExtensions.NormalizePath(Path.Join(folder.FolderPath, newFile));
                return (oldPath, newPath);
            }

            for (var i = 0; i < firstChangeCount; i++)
                renamed.Add(Rename(firstTemp));

            for (var i = 0; i < secondChangeCount; i++)
                renamed.Add(Rename(secondTemp));

            // Даем время сработать событиям переименования.
            await Task.Delay(5 * (firstChangeCount + secondChangeCount));

            indexer.Verify(x => x.Switch(It.IsAny<string>(), It.IsAny<string>()),
                Times.Exactly(firstChangeCount + secondChangeCount));
            foreach (var (oldPath, newPath) in renamed)
            {
                indexer.Verify(x => x.Switch(oldPath, newPath), Times.Once);
            }
        }
    }

    public class OnUnsubscribeFileAnalyzerTests
    {
        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(10)]
        [InlineData(100)]
        [InlineData(1000)]
        public async Task OnUnsubscribe_WatchedPath_RemoveIndex(int filesCount)
        {
            var searcher = new Mock<ISearcher<string, string>>();
            var indexer = new Mock<IIndexer<string, string>>(MockBehavior.Strict);
            var tokenizer = new Mock<ITokenizer<string>>();
            var analyzer = new FileAnalyzer(tokenizer.Object, searcher.Object, indexer.Object);

            using var tmp = new TempFolder(filesCount);
            foreach (var file in tmp.Files)
            {
                var sequence = new MockSequence();
                indexer.InSequence(sequence).Setup(x => x.Add(file, It.IsAny<IEnumerable<string>>()));
                indexer.InSequence(sequence).Setup(x => x.Remove(file));
            }

            await Task.Run(async () => await analyzer.Subscribe(tmp.FolderPath));

            // Дадим попдисаться.
            await Task.Delay(1000);

            await Task.Run(async () => await analyzer.Unsubscribe(tmp.FolderPath));

            // Дадим отписаться.
            await Task.Delay(1000);

            indexer.Verify(x => x.Add(It.IsAny<string>(), It.IsAny<IEnumerable<string>>()), Times.Exactly(filesCount));
            foreach (var file in tmp.Files)
            {
                indexer.Verify(x => x.Add(file, It.IsAny<IEnumerable<string>>()), Times.Once);
                indexer.Verify(x => x.Remove(file), Times.Once);
            }
        }

        [Theory]
        [InlineData(100)]
        public async Task OnUnsubscribe_NotExistingWatch_DoNothing(int filesCount)
        {
            var searcher = new Mock<ISearcher<string, string>>();
            var indexer = new Mock<IIndexer<string, string>>(MockBehavior.Strict);
            var tokenizer = new Mock<ITokenizer<string>>();
            var analyzer = new FileAnalyzer(tokenizer.Object, searcher.Object, indexer.Object);

            using var tmp = new TempFolder(filesCount);
            foreach (var file in tmp.Files)
                indexer.Setup(x => x.Add(file, It.IsAny<IEnumerable<string>>()));

            await Task.Run(async () => await analyzer.Subscribe(tmp.FolderPath));

            // Дадим попдисаться.
            await Task.Delay(150);

            await Task.Run(async () => await analyzer.Unsubscribe(Path.GetRandomFileName()));

            // Дадим "отписаться".
            await Task.Delay(150);

            indexer.Verify(x => x.Add(It.IsAny<string>(), It.IsAny<IEnumerable<string>>()), Times.Exactly(filesCount));
            foreach (var file in tmp.Files)
                indexer.Verify(x => x.Add(file, It.IsAny<IEnumerable<string>>()), Times.Once);
            indexer.VerifyNoOtherCalls();
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(10)]
        [InlineData(100)]
        [InlineData(1000)]
        public async Task OnUnsubscribe_SubscribeAgain_RemoveThenAddToIndexInQueue(int filesCount)
        {
            var searcher = new Mock<ISearcher<string, string>>();
            var indexer = new Mock<IIndexer<string, string>>(MockBehavior.Strict);
            var tokenizer = new Mock<ITokenizer<string>>();
            var analyzer = new FileAnalyzer(tokenizer.Object, searcher.Object, indexer.Object);

            using var tmp = new TempFolder(filesCount);
            var folder = tmp.FolderPath;

            foreach (var file in tmp.Files)
            {
                var sequence = new MockSequence();
                indexer.InSequence(sequence).Setup(x => x.Add(file, It.IsAny<IEnumerable<string>>()));
                indexer.InSequence(sequence).Setup(x => x.Remove(file));
                indexer.InSequence(sequence).Setup(x => x.Add(file, It.IsAny<IEnumerable<string>>()));
            }

            await Task.Run(async () => await analyzer.Subscribe(folder));

            // Дадим попдисаться.
            await Task.Delay(1000);

            var unsub = Task.Run(async () => await analyzer.Unsubscribe(folder));

            // Дадим отписаться.
            await Task.Delay(1000);

            var sub = Task.Run(async () => await analyzer.Subscribe(folder));

            await Task.WhenAll(unsub, sub);

            indexer.Verify(x => x.Add(It.IsAny<string>(), It.IsAny<IEnumerable<string>>()), Times.Exactly(filesCount * 2));
            foreach (var file in tmp.Files)
            {
                indexer.Verify(x => x.Add(file, It.IsAny<IEnumerable<string>>()), Times.Exactly(2));
                indexer.Verify(x => x.Remove(file), Times.Once);
            }
        }

        [Theory]
        [InlineData(0, 5)]
        [InlineData(1, 5)]
        [InlineData(10, 5)]
        [InlineData(100, 50)]
        [InlineData(1000, 500)]
        public async Task OnUnsubscribe_StartAddingFiles_AddOrCancelThenRemoveIndex(int initFilesCount, int newFilesCount)
        {
            var searcher = new Mock<ISearcher<string, string>>();
            var indexer = new Mock<IIndexer<string, string>>(MockBehavior.Strict);
            var tokenizer = new Mock<ITokenizer<string>>();
            var analyzer = new FileAnalyzer(tokenizer.Object, searcher.Object, indexer.Object);

            using var tmp = new TempFolder(initFilesCount);
            var folder = tmp.FolderPath;

            await Task.Run(async () => await analyzer.Subscribe(folder));
            await Task.Delay(150);

            var unsubscribe = Task.Run(async () => await analyzer.Unsubscribe(folder));
            await Task.Delay(10);

            var newFiles = new List<string>();
            for (var i = 0; i < newFilesCount; i++)
            {
                var sequence = new MockSequence();
                var file = tmp.CreateFile();
                indexer.InSequence(sequence).Setup(x => x.Add(file, It.IsAny<IEnumerable<string>>()));
                indexer.InSequence(sequence).Setup(x => x.Remove(file));
                newFiles.Add(file);
            }

            await unsubscribe;

            foreach (var file in newFiles)
            {
                indexer.Verify(x => x.Add(file, It.IsAny<IEnumerable<string>>()), Times.AtMostOnce);
                indexer.Verify(x => x.Remove(file, It.IsAny<IEnumerable<string>>()), Times.AtMostOnce);
            }
        }

        [Theory]
        [InlineData(0, 5)]
        [InlineData(1, 5)]
        [InlineData(10, 5)]
        [InlineData(100, 50)]
        [InlineData(1000, 500)]
        public async Task OnUnsubscribe_StartChangingFiles_AddOrCancelThenRemoveIndex(int initFilesCount, int newFilesCount)
        {
            var searcher = new Mock<ISearcher<string, string>>();
            var indexer = new Mock<IIndexer<string, string>>(MockBehavior.Strict);
            var tokenizer = new Mock<ITokenizer<string>>();
            var analyzer = new FileAnalyzer(tokenizer.Object, searcher.Object, indexer.Object);

            using var tmp = new TempFolder(initFilesCount);
            var folder = tmp.FolderPath;

            await Task.Run(async () => await analyzer.Subscribe(folder));
            await Task.Delay(150);

            var unsubscribe = Task.Run(async () => await analyzer.Unsubscribe(folder));
            await Task.Delay(10);

            var newFiles = new List<(string oldFile, string newFile)>();
            for (var i = 0; i < newFilesCount; i++)
            {
                var sequence = new MockSequence();
                var newName = Path.GetRandomFileName();
                var newFile = Path.Join(tmp.FolderPath, newName);
                var oldFile = tmp.RenameFile(newName);

                indexer.InSequence(sequence).Setup(x => x.Switch(oldFile, newFile));
                indexer.InSequence(sequence).Setup(x => x.Remove(newFile));
                newFiles.Add((oldFile, newFile));
            }

            await unsubscribe;

            foreach (var (oldFile, newFile) in newFiles)
            {
                indexer.Verify(x => x.Switch(oldFile, newFile), Times.AtMostOnce);
                indexer.Verify(x => x.Remove(newFile), Times.AtMostOnce);
            }
        }
    }

    public class AfterUnsubscribeFileAnalyzerTests
    {
        [Theory]
        [InlineData(0, 5)]
        [InlineData(1, 5)]
        [InlineData(10, 5)]
        [InlineData(100, 50)]
        [InlineData(1000, 500)]
        public async Task AfterUnsubscribe_StartChangingFiles_DoNothing(int initFilesCount, int newFilesCount)
        {
            var searcher = new Mock<ISearcher<string, string>>();
            var indexer = new Mock<IIndexer<string, string>>();
            var tokenizer = new Mock<ITokenizer<string>>();
            var analyzer = new FileAnalyzer(tokenizer.Object, searcher.Object, indexer.Object);

            using var tmp = new TempFolder(initFilesCount);
            var folder = tmp.FolderPath;
            var files = tmp.Files.ToList();

            foreach (var file in files)
                indexer.Setup(x => x.Remove(file));

            await Task.Run(async () => await analyzer.Subscribe(folder));
            await Task.Delay(150);

            await Task.Run(async () => await analyzer.Unsubscribe(folder));
            await Task.Delay(150);

            var newFiles = new List<(string oldFile, string newFile)>();
            for (var i = 0; i < newFilesCount; i++)
            {
                var newName = Path.GetRandomFileName();
                var newFile = Path.Join(tmp.FolderPath, newName);
                var oldFile = tmp.RenameFile(newName);
                newFiles.Add((oldFile, newFile));
            }

            foreach (var file in files)
                indexer.Verify(x => x.Remove(file), Times.Once);

            foreach (var (oldFile, newFile) in newFiles)
            {
                indexer.Verify(x => x.Remove(newFile), Times.Never);
                indexer.Verify(x => x.Switch(oldFile, newFile), Times.Never);
            }
        }

        [Theory]
        [InlineData(0)]
        [InlineData(10)]
        [InlineData(100)]
        [InlineData(1000)]
        public async Task AfterUnsubscribe_GetSubscriptions_MustBeEmpty(int subscriptionsCount)
        {
            var searcher = new Mock<ISearcher<string, string>>();
            var indexer = new Mock<IIndexer<string, string>>();
            var tokenizer = new Mock<ITokenizer<string>>();

            var analyzer = new FileAnalyzer(tokenizer.Object, searcher.Object, indexer.Object);

            var tasks = new List<Task>();
            var folders = new List<TempFolder>();
            for (var i = 0; i < subscriptionsCount; i++)
            {
                var tmp = new TempFolder(1);
                var folder = tmp.FolderPath;

                folders.Add(tmp);
                tasks.Add(Task.Run(async () => await analyzer.Subscribe(folder)));
            }

            await Task.WhenAll(tasks);
            Assert.Equal(subscriptionsCount, analyzer.Subscriptions.Count);

            tasks.Clear();
            tasks.AddRange(folders.Select(tmp => Task.Run(async () => await analyzer.Unsubscribe(tmp.FolderPath))));

            await Task.WhenAll(tasks);
            Assert.Equal(0, analyzer.Subscriptions.Count);

            foreach (var tmp in folders)
                tmp.Dispose();
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(10)]
        [InlineData(100)]
        [InlineData(1000)]
        [InlineData(5000)]
        public async Task AfterUnsubscribe_Search_NoResult(int filesCount)
        {
            var random = new Random();
            var guids = Generators.Generate(5, () => Guid.NewGuid().ToString()).ToArray();
            var text = string.Join(' ', guids);

            var tokenizer = new Mock<ITokenizer<string>>();
            tokenizer.Setup(x => x.Tokenize(text)).Returns(guids);

            using var tmp = new TempFolder(filesCount, () => text);
            var path = tmp.FolderPath;
            var analyzer = new FileAnalyzer(tokenizer.Object);

            var guid = guids[random.Next(guids.Length)];

            await Task.Run(async () => await analyzer.Subscribe(path));
            await Task.Delay(1000);

            await Task.Run(async () => await analyzer.Unsubscribe(path));
            await Task.Delay(1000);

            var search = analyzer.Search(guid).ToList();
            Assert.Empty(search);
        }
    }
}