using System;
using System.Collections.Concurrent;
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

        private readonly BackgroundQueue _backgroundQueue;

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

        public bool IsIndexing => !_backgroundQueue.IsEmpty;

        public Task<IEnumerable<string>> DelayedSearch(string query)
        {
            return Task.Run(() =>
            {
                using (_backgroundQueue.Pause())
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
            if (!Directory.Exists(path))
            {
                Debug.WriteLine($"Directory \"{path}\" not exists. Thread = {Thread.CurrentThread.ManagedThreadId}");
                return;
            }

            var filter = string.IsNullOrWhiteSpace(pattern) ? DefaultFilePattern : pattern;
            using (new UpgradeableReadLockCookie(_subscriptionLock))
            {
                if (!_watchers.TryGetValue(path, out var watcher))
                {
                    using (new WriteLockCookie(_subscriptionLock))
                    {
                        watcher = new FileSystemWatcher(path, filter);
                        _watchers[path] = watcher;
                        SubscribeWatcher(watcher);
                    }
                }
                else
                {
                    if (watcher.Filters.Contains(filter))
                        return;

                    using (new WriteLockCookie(_subscriptionLock))
                        watcher.Filters.Add(filter);
                }
            }

            var tasks = new List<Task>();
            using (new WriteLockCookie(_queueLock))
            {
                tasks.AddRange(DirectoryExtensions
                    .GetFilesSafe(path, filter)
                    .Select(x => _backgroundQueue.QueueTask(() =>
                    {
                        var tokens = ParseTokens(x);

                        Debug.WriteLine($"Start indexing \"{x}\". Thread = {Thread.CurrentThread.ManagedThreadId}");
                        Indexer.Add(x, tokens);
                    }, x)));
            }

            await Task.WhenAll(tasks).IgnoreExceptions();
            await Task.Yield();
        }

        private void SubscribeWatcher(FileSystemWatcher watcher)
        {
            watcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName;
            watcher.IncludeSubdirectories = false;
            watcher.Changed += OnFileChanged;
            watcher.Created += OnFileCreated;
            watcher.Deleted += OnFileDeleted;
            watcher.Renamed += OnFileRenamed;
            watcher.EnableRaisingEvents = true;

            Debug.WriteLine($"Subscribed to \"{Path.Join(watcher.Path, watcher.Filter)}\". Thread = {Thread.CurrentThread.ManagedThreadId}");
        }

        public async Task Unsubscribe(string path, string pattern = null)
        {
            string filter;
            using (new UpgradeableReadLockCookie(_subscriptionLock))
            {
                if (!_watchers.TryGetValue(path, out var watcher))
                {
                    Debug.WriteLine($"No subscribed watchers for \"{path}\". Thread = {Thread.CurrentThread.ManagedThreadId}");
                    return;
                }

                filter = string.IsNullOrWhiteSpace(pattern)
                    ? DefaultFilePattern
                    : watcher.Filters.FirstOrDefault(x => x.Equals(pattern));

                using (new WriteLockCookie(_subscriptionLock))
                {
                    watcher.Filters.Remove(pattern);
                    if (string.IsNullOrWhiteSpace(pattern) || !watcher.Filters.Any())
                    {
                        _watchers.Remove(path);
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
                        .GetFilesSafe(path, filter)
                        .Select(x =>
                        {
                            _backgroundQueue.CancelTasks(x);
                            return _backgroundQueue.QueueTask(() =>
                            {
                                Debug.WriteLine($"Removing index for \"{x}\". Thread = {Thread.CurrentThread.ManagedThreadId}");
                                Indexer.Remove(x);
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
                _backgroundQueue.QueueTask(() =>
                {
                    var tokens = ParseTokens(e.FullPath);

                    Debug.WriteLine($"\"{e.FullPath}\" changed, reindexing. Thread = {Thread.CurrentThread.ManagedThreadId}");
                    Indexer.Update(e.FullPath, tokens);
                }, e.FullPath);
            }
        }

        private void OnFileCreated(object source, FileSystemEventArgs e)
        {
            using (new WriteLockCookie(_queueLock))
            {
                _backgroundQueue.QueueTask(() =>
                {
                    var tokens = ParseTokens(e.FullPath);

                    Debug.WriteLine($"\"{e.FullPath}\" created, indexing. Thread = {Thread.CurrentThread.ManagedThreadId}");
                    Indexer.Add(e.FullPath, tokens);
                }, e.FullPath);
            }
        }

        private void OnFileDeleted(object source, FileSystemEventArgs e)
        {
            using (new WriteLockCookie(_queueLock))
            {
                _backgroundQueue.CancelTasks(e.FullPath);
                _backgroundQueue.QueueTask(() =>
                {
                    Debug.WriteLine($"\"{e.FullPath}\" removed, clearing index. Thread = {Thread.CurrentThread.ManagedThreadId}");
                    Indexer.Remove(e.FullPath);
                });
            }

            // Если подписались на конкретный файл.
            using (new UpgradeableReadLockCookie(_subscriptionLock))
            {
                if (source is FileSystemWatcher watcher && watcher.Filters.Contains(e.Name))
                {
                    using (new WriteLockCookie(_subscriptionLock))
                    {
                        watcher.Filters.Remove(e.Name);
                    }
                }
            }
        }

        private void OnFileRenamed(object source, RenamedEventArgs e)
        {
            using (new WriteLockCookie(_queueLock))
            {
                _backgroundQueue.QueueTask(() =>
                {
                    Debug.WriteLine($"\"{e.OldFullPath}\" renamed to \"{e.FullPath}\", reindexing. Thread = {Thread.CurrentThread.ManagedThreadId}");
                    Indexer.Switch(e.OldFullPath, e.FullPath);
                });
            }

            // Если подписались на конкретный файл.
            using (new UpgradeableReadLockCookie(_subscriptionLock))
            {
                if (source is FileSystemWatcher watcher && watcher.Filters.Contains(e.OldName))
                {
                    using (new WriteLockCookie(_subscriptionLock))
                    {
                        watcher.Filters.Remove(e.OldName);
                        watcher.Filters.Add(e.Name);
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
                _queueLock?.Dispose();
                _subscriptionLock?.Dispose();
                _backgroundQueue?.Dispose();
                foreach (var (_, watcher) in _watchers)
                    UnsubscribeWatcher(watcher);
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
            _backgroundQueue = new BackgroundQueue();
        }
    }
}