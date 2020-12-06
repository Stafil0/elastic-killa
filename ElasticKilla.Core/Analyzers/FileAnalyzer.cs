using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ElasticKilla.Core.Collections;
using ElasticKilla.Core.Extensions;
using ElasticKilla.Core.Indexers;
using ElasticKilla.Core.Indexes;
using ElasticKilla.Core.Lockers;
using ElasticKilla.Core.Searchers;
using ElasticKilla.Core.Tokenizer;

namespace ElasticKilla.Core.Analyzers
{
    public class FileAnalyzer : BaseAnalyzer<string, string>
    {
        private const string DefaultFilePattern = "*";

        private readonly ReaderWriterLockSlim _queueLock = new ReaderWriterLockSlim(LockRecursionPolicy.SupportsRecursion);

        private readonly ReaderWriterLockSlim _subscriptionLock = new ReaderWriterLockSlim(LockRecursionPolicy.SupportsRecursion);

        private readonly Dictionary<string, FileSystemWatcher> _watchers;

        private readonly ITokenizer<string> _tokenizer;

        private readonly BackgroundTaskQueue<string> _backgroundTaskQueue;

        private bool _disposed;

        public ICollection<string> Subscriptions
        {
            get
            {
                using (new ReadLockCookie(_subscriptionLock))
                {
                    var result = _watchers.Values.Select(x => Path.Join(x.Path, string.Join('|', x.Filters))).ToList(); 
                    
                    Debug.WriteLine($"Subscribed to {result.Count} folders. Thread = {Thread.CurrentThread.ManagedThreadId}");
                    
                    return result;
                }
            }
        }

        public bool IsIndexing => !_backgroundTaskQueue.IsEmpty;

        public Task<IEnumerable<string>> DelayedSearch(string query)
        {
            return Task.Run(() =>
            {
                using (_backgroundTaskQueue.Pause())
                {
                    return base.Search(query);
                }
            });
        }

        private ISet<string> ParseTokens(string path)
        {
            Debug.WriteLine($"Parsing tokens from \"{path}\". Thread = {Thread.CurrentThread.ManagedThreadId}");

            var set = new HashSet<string>();
            try
            {
                using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (var reader = new StreamReader(stream))
                {
                    string text;
                    while ((text = reader.ReadLine()) != null)
                    {
                        var tokens = _tokenizer.Tokenize(text);
                        set.UnionWith(tokens);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Can't access to \"{path}\": {ex.Message}. Thread = {Thread.CurrentThread.ManagedThreadId}");
            }

            Debug.WriteLine($"Parsed {set.Count} tokens from \"{path}\". Thread = {Thread.CurrentThread.ManagedThreadId}");
            return set;
        }

        #region FileSystemWatcher

        public async Task Subscribe(string path, string pattern = null)
        {
            var normalizedFolder = PathExtensions.NormalizePath(path);
            if (!Directory.Exists(normalizedFolder))
            {
                Debug.WriteLine($"Directory \"{path}\" not exists. Thread = {Thread.CurrentThread.ManagedThreadId}");
                return;
            }

            var filter = string.IsNullOrWhiteSpace(pattern) ? DefaultFilePattern : pattern.ToLowerInvariant();
            using (new UpgradeableReadLockCookie(_subscriptionLock))
            {
                if (!_watchers.TryGetValue(normalizedFolder, out var watcher))
                {
                    using (new WriteLockCookie(_subscriptionLock))
                    {
                        if (TryGetAndSubscribeWatcher(normalizedFolder, filter, out watcher))
                            _watchers[normalizedFolder] = watcher;
                        else return;
                    }
                }
                else
                {
                    if (watcher.Filters.Contains(filter, StringComparer.InvariantCultureIgnoreCase))
                        return;

                    using (new WriteLockCookie(_subscriptionLock))
                        watcher.Filters.Add(filter);
                }
            }

            var tasks = new List<Task>();
            using (new WriteLockCookie(_queueLock))
            {
                tasks.AddRange(DirectoryExtensions
                    .GetFilesSafe(normalizedFolder, filter)
                    .Select(x =>
                    {
                        var normalizedFile = PathExtensions.NormalizePath(x);
                        return _backgroundTaskQueue.QueueTask(normalizedFolder, () =>
                        {
                            var tokens = ParseTokens(normalizedFile);

                            Debug.WriteLine($"Start indexing \"{x}\". Thread = {Thread.CurrentThread.ManagedThreadId}");
                            Indexer.Add(normalizedFile, tokens);
                        }, normalizedFile);
                    }));
            }

            await Task.WhenAll(tasks).IgnoreExceptions();
            await Task.Yield();
        }

        private bool TryGetAndSubscribeWatcher(string path, string filter, out FileSystemWatcher watcher)
        {
            watcher = default;
            bool subscribed;
            try
            {
                watcher = new FileSystemWatcher(path, filter);
                watcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName;
                watcher.IncludeSubdirectories = false;
                watcher.EnableRaisingEvents = true;
                watcher.Changed += OnFileChanged;
                watcher.Created += OnFileCreated;
                watcher.Deleted += OnFileDeleted;
                watcher.Renamed += OnFileRenamed;
                subscribed = true;
                Debug.WriteLine($"Subscribed to \"{Path.Join(watcher.Path, watcher.Filter)}\". Thread = {Thread.CurrentThread.ManagedThreadId}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Cant subscribe to \"{Path.Join(path, filter)}\": {ex.Message}. Thread = {Thread.CurrentThread.ManagedThreadId}");
                subscribed = false;
            }

            return subscribed;
        }

        public async Task Unsubscribe(string path, string pattern = null)
        {
            string filter;
            var normalizedFolder = PathExtensions.NormalizePath(path);
            using (new UpgradeableReadLockCookie(_subscriptionLock))
            {
                if (!_watchers.TryGetValue(normalizedFolder, out var watcher))
                {
                    Debug.WriteLine($"No subscribed watchers for \"{path}\". Thread = {Thread.CurrentThread.ManagedThreadId}");
                    return;
                }

                filter = string.IsNullOrWhiteSpace(pattern)
                    ? DefaultFilePattern
                    : watcher.Filters.FirstOrDefault(x => x.Equals(pattern, StringComparison.InvariantCultureIgnoreCase));

                using (new WriteLockCookie(_subscriptionLock))
                {
                    if (filter != null)
                        watcher.Filters.Remove(filter);

                    if (string.IsNullOrWhiteSpace(pattern) || !watcher.Filters.Any())
                    {
                        _watchers.Remove(normalizedFolder);
                        UnsubscribeWatcher(watcher);
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(filter))
            {
                var tasks = new List<Task>();
                using (new WriteLockCookie(_queueLock))
                {
                    tasks.AddRange(DirectoryExtensions
                        .GetFilesSafe(normalizedFolder, filter)
                        .Select(x =>
                        {
                            var normalizedFile = PathExtensions.NormalizePath(x);
                            _backgroundTaskQueue.CancelTasks(normalizedFile);
                            return _backgroundTaskQueue.QueueTask(normalizedFolder, () =>
                            {
                                Debug.WriteLine($"Removing index for \"{x}\". Thread = {Thread.CurrentThread.ManagedThreadId}");
                                Indexer.Remove(normalizedFile);
                            });
                        }));
                }

                await Task.WhenAll(tasks).IgnoreExceptions();
                await Task.Yield();
            }
        }

        private void UnsubscribeWatcher(FileSystemWatcher watcher)
        {
            Debug.WriteLine($"Unsubscribing \"{watcher.Path}\" watcher. Thread = {Thread.CurrentThread.ManagedThreadId}");

            watcher.EnableRaisingEvents = false;
            watcher.Changed -= OnFileChanged;
            watcher.Created -= OnFileCreated;
            watcher.Deleted -= OnFileDeleted;
            watcher.Renamed -= OnFileRenamed;
            watcher.Dispose();
        }

        private void OnFileChanged(object source, FileSystemEventArgs e)
        {
            using (new WriteLockCookie(_queueLock))
            {
                var normalized = PathExtensions.NormalizePath(e.FullPath);
                var directory = Path.GetDirectoryName(normalized);
                _backgroundTaskQueue.QueueTask(directory, () =>
                {
                    var tokens = ParseTokens(normalized);

                    Debug.WriteLine($"\"{e.FullPath}\" changed, reindexing. Thread = {Thread.CurrentThread.ManagedThreadId}");
                    Indexer.Update(normalized, tokens);
                }, normalized);
            }
        }

        private void OnFileCreated(object source, FileSystemEventArgs e)
        {
            using (new WriteLockCookie(_queueLock))
            {
                var normalized = PathExtensions.NormalizePath(e.FullPath);
                var directory = Path.GetDirectoryName(normalized);
                _backgroundTaskQueue.QueueTask(directory, () =>
                {
                    var tokens = ParseTokens(normalized);

                    Debug.WriteLine($"\"{e.FullPath}\" created, indexing. Thread = {Thread.CurrentThread.ManagedThreadId}");
                    Indexer.Add(normalized, tokens);
                }, normalized);
            }
        }

        private void OnFileDeleted(object source, FileSystemEventArgs e)
        {
            using (new WriteLockCookie(_queueLock))
            {
                var normalized = PathExtensions.NormalizePath(e.FullPath);
                var directory = Path.GetDirectoryName(normalized);
                _backgroundTaskQueue.CancelTasks(normalized);
                _backgroundTaskQueue.QueueTask(directory, () =>
                {
                    Debug.WriteLine($"\"{e.FullPath}\" removed, clearing index. Thread = {Thread.CurrentThread.ManagedThreadId}");
                    Indexer.Remove(normalized);
                });
            }

            // Если подписались на конкретный файл.
            using (new UpgradeableReadLockCookie(_subscriptionLock))
            {
                if (source is FileSystemWatcher watcher)
                {
                    var file = watcher.Filters.FirstOrDefault(x => string.Equals(x, e.Name, StringComparison.InvariantCultureIgnoreCase));
                    if (file == null) 
                        return;

                    using (new WriteLockCookie(_subscriptionLock))
                    {
                        watcher.Filters.Remove(file);
                    }
                }
            }
        }

        private void OnFileRenamed(object source, RenamedEventArgs e)
        {
            using (new WriteLockCookie(_queueLock))
            {
                var normalizedOld = PathExtensions.NormalizePath(e.OldFullPath);
                var normalizedNew = PathExtensions.NormalizePath(e.FullPath);
                var directory = PathExtensions.NormalizePath(normalizedNew);
                _backgroundTaskQueue.QueueTask(directory, () =>
                {
                    Debug.WriteLine($"\"{e.OldFullPath}\" renamed to \"{e.FullPath}\", reindexing. Thread = {Thread.CurrentThread.ManagedThreadId}");
                    Indexer.Switch(normalizedOld, normalizedNew);
                });
            }

            // Если подписались на конкретный файл.
            using (new UpgradeableReadLockCookie(_subscriptionLock))
            {
                if (source is FileSystemWatcher watcher)
                {
                    var file = watcher.Filters.FirstOrDefault(x => string.Equals(x, e.OldName, StringComparison.InvariantCultureIgnoreCase));
                    if (file == null) 
                        return;

                    using (new WriteLockCookie(_subscriptionLock))
                    {
                        watcher.Filters.Remove(file);
                        watcher.Filters.Add(e.Name.ToLowerInvariant());
                    }
                }
            }
        }

        #endregion

        #region IDisposable

        protected override void Dispose(bool disposing)
        {
            if (_disposed)
                return;

            if (disposing)
            {
                foreach (var (_, watcher) in _watchers)
                    UnsubscribeWatcher(watcher);

                _backgroundTaskQueue?.Dispose();
                _queueLock?.Dispose();
                _subscriptionLock?.Dispose();
            }

            _disposed = true;

            base.Dispose();
        }

        #endregion

        public FileAnalyzer(ITokenizer<string> tokenizer)
            : this(
                tokenizer,
                new StringIndex<string>(),
                new StringIndex<string>())
        {
        }

        public FileAnalyzer(
            ITokenizer<string> tokenizer,
            IIndex<string, string> forwardIndex,
            IIndex<string, string> invertedIndex)
            : this(
                tokenizer,
                new Searcher<string, string>(invertedIndex),
                new Indexer<string, string>(forwardIndex, invertedIndex))
        {
        }

        internal FileAnalyzer(
            ITokenizer<string> tokenizer,
            ISearcher<string, string> searcher,
            IIndexer<string, string> indexer)
            : base(searcher, indexer)
        {
            _tokenizer = tokenizer;
            _watchers = new Dictionary<string, FileSystemWatcher>();
            _backgroundTaskQueue = new BackgroundTaskQueue<string>();
        }
    }
}