using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ElasticKilla.Core.Collections;
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

        private readonly ReaderWriterLockSlim _lock = new ReaderWriterLockSlim();

        private readonly Dictionary<string, FileSystemWatcher> _watchers;

        private readonly ITokenizer<string> _tokenizer;

        private readonly BackgroundQueue _backgroundQueue;

        private bool _disposed;

        public ICollection<string> Subscriptions
        {
            get
            {
                using (new ReadLockCookie(_lock))
                {
                    return _watchers.Values.Select(x => Path.Join(x.Path, string.Join('|', x.Filters))).ToList();
                }
            }
        }

        private ISet<string> ParseTokens(string path)
        {
            Debug.WriteLine($"Parsing tokens from \"{path}\". Thread = {Thread.CurrentThread.ManagedThreadId}");

            var set = new HashSet<string>();
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

            Debug.WriteLine($"Parsed {set.Count} tokens from \"{path}\". Thread = {Thread.CurrentThread.ManagedThreadId}");
            return set;
        }

        #region FileSystemWatcher

        public async Task Subscribe(string path, string pattern = null)
        {
            if (!File.Exists(path) && !Directory.Exists(path))
            {
                Debug.WriteLine($"Already subscribed to \"{path}\" or path not exists. Thread = {Thread.CurrentThread.ManagedThreadId}");
                return;
            }

            string filter;
            using (new UpgradeableReadLockCookie(_lock))
            {
                if (!_watchers.TryGetValue(path, out var watcher))
                {
                    using (new WriteLockCookie(_lock))
                    {
                        watcher = new FileSystemWatcher(path);
                        _watchers[path] = watcher;
                        SubscribeWatcher(watcher);
                    }
                }

                filter = string.IsNullOrWhiteSpace(pattern) ? DefaultFilePattern : pattern;
                if (watcher.Filters.Contains(filter))
                    return;

                using (new WriteLockCookie(_lock))
                    watcher.Filters.Add(filter);
            }

            var tasks = Directory
                .EnumerateFiles(path, filter)
                .Select(x => new {File = x, Tokens = ParseTokens(x)})
                .Select(x => _backgroundQueue.QueueTask(() =>
                {
                    Debug.WriteLine($"Start indexing \"{x.File}\". Thread = {Thread.CurrentThread.ManagedThreadId}");
                    Indexer.Add(x.File, x.Tokens);
                }));

            await Task.WhenAll(tasks);
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
            using (new UpgradeableReadLockCookie(_lock))
            {
                if (!_watchers.TryGetValue(path, out var watcher))
                {
                    Debug.WriteLine($"No subscribed watchers for \"{path}\". Thread = {Thread.CurrentThread.ManagedThreadId}");
                    return;
                }

                filter = string.IsNullOrWhiteSpace(pattern)
                    ? DefaultFilePattern
                    : watcher.Filters.FirstOrDefault(x => x.Equals(pattern));

                using (new WriteLockCookie(_lock))
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
                var tasks = Directory
                    .EnumerateFiles(path, filter)
                    .Select(x => _backgroundQueue.QueueTask(() =>
                    {
                        Debug.WriteLine($"Removing index for \"{x}\". Thread = {Thread.CurrentThread.ManagedThreadId}");
                        Indexer.Remove(x);
                    }));

                await Task.WhenAll(tasks);
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
            _backgroundQueue.QueueTask(() =>
            {
                Debug.WriteLine($"\"{e.FullPath}\" changed, reindexing. Thread = {Thread.CurrentThread.ManagedThreadId}");

                var tokens = ParseTokens(e.FullPath);
                Indexer.Update(e.FullPath, tokens);
            });
        }

        private void OnFileCreated(object source, FileSystemEventArgs e)
        {
            _backgroundQueue.QueueTask(() =>
            {
                Debug.WriteLine($"\"{e.FullPath}\" created, indexing. Thread = {Thread.CurrentThread.ManagedThreadId}");

                var tokens = ParseTokens(e.FullPath);
                Indexer.Add(e.FullPath, tokens);
            });
        }

        private void OnFileDeleted(object source, FileSystemEventArgs e)
        {
            _backgroundQueue.QueueTask(() =>
            {
                Debug.WriteLine($"\"{e.FullPath}\" removed, clearing index. Thread = {Thread.CurrentThread.ManagedThreadId}");
                Indexer.Remove(e.FullPath);
            });

            // Если подписались на конкретный файл.
            if (source is FileSystemWatcher watcher && watcher.Filters.Contains(e.Name))
            {
                using (new WriteLockCookie(_lock))
                {
                    watcher.Filters.Remove(e.Name);
                }
            }
        }

        private void OnFileRenamed(object source, RenamedEventArgs e)
        {
            _backgroundQueue.QueueTask(() =>
            {
                Debug.WriteLine($"\"{e.OldFullPath}\" renamed to \"{e.FullPath}\", reindexing. Thread = {Thread.CurrentThread.ManagedThreadId}");
                Indexer.Switch(e.OldFullPath, e.FullPath);
            });

            // Если подписались на конкретный файл.
            if (source is FileSystemWatcher watcher && watcher.Filters.Contains(e.OldName))
            {
                using (new WriteLockCookie(_lock))
                {
                    watcher.Filters.Remove(e.OldName);
                    watcher.Filters.Add(e.Name);
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
                    watcher.Dispose();
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