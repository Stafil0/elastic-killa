using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ElasticKilla.Core.Collections;
using ElasticKilla.Core.Indexers;
using ElasticKilla.Core.Indexes;
using ElasticKilla.Core.Searchers;
using ElasticKilla.Core.Tokenizer;

namespace ElasticKilla.Core.Analyzers
{
    public class FileAnalyzer : BaseAnalyzer<string, string>
    {
        private const string DefaultFilter = "*.*";

        private readonly Dictionary<string, FileSystemWatcher> _watchers;

        private readonly ITokenizer<string> _tokenizer;

        private readonly BackgroundQueue _backgroundQueue;

        private bool _disposed;

        private ISet<string> ReadTokens(string path)
        {
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

            return set;
        }

        #region FileSystemWatcher

        public async Task Subscribe(string path, string filter = null)
        {
            if (_watchers.ContainsKey(path) || !File.Exists(path) && !Directory.Exists(path))
                return;

            var fileFilter = string.IsNullOrWhiteSpace(filter) ? DefaultFilter : filter;
            var watcher = new FileSystemWatcher(path, fileFilter)
            {
                NotifyFilter = NotifyFilters.LastWrite |
                               NotifyFilters.FileName |
                               NotifyFilters.DirectoryName,
            };

            watcher.Changed += OnFileChanged;
            watcher.Created += OnFileCreated;
            watcher.Deleted += OnFileDeleted;
            watcher.Renamed += OnFileRenamed;
            watcher.IncludeSubdirectories = false;
            watcher.EnableRaisingEvents = true;

            _watchers[path] = watcher;

            var tasks = Directory
                .EnumerateFiles(path, filter)
                .Select(x => new {File = x, Tokens = ReadTokens(x)})
                .Select(x => _backgroundQueue.QueueTask(() => Indexer.Add(x.File, x.Tokens)));

            await Task.WhenAll(tasks);
            await Task.Yield();
        }

        public async Task Unsubscribe(string path)
        {
            if (!_watchers.TryGetValue(path, out var watcher))
                return;

            watcher.EnableRaisingEvents = false;
            watcher.Changed -= OnFileChanged;
            watcher.Created -= OnFileCreated;
            watcher.Deleted -= OnFileDeleted;
            watcher.Renamed -= OnFileRenamed;

            var tasks = Directory
                .EnumerateFiles(watcher.Path, watcher.Filter)
                .Select(x => _backgroundQueue.QueueTask(() => Indexer.Remove(x)));

            await Task.WhenAll(tasks);
            await Task.Yield();

            watcher.Dispose();
            _watchers.Remove(path);
        }

        private void OnFileChanged(object source, FileSystemEventArgs e)
        {
            _backgroundQueue.QueueTask(() =>
            {
                var tokens = ReadTokens(e.FullPath);
                Indexer.Update(e.FullPath, tokens);
            });
        }

        private void OnFileCreated(object source, FileSystemEventArgs e)
        {
            _backgroundQueue.QueueTask(() =>
            {
                var tokens = ReadTokens(e.FullPath);
                Indexer.Add(e.FullPath, tokens);
            });
        }

        private void OnFileDeleted(object source, FileSystemEventArgs e)
        {
            _backgroundQueue.QueueTask(() => Indexer.Remove(e.FullPath));
        }

        private void OnFileRenamed(object source, RenamedEventArgs e)
        {
            _backgroundQueue.QueueTask(() => Indexer.Switch(e.OldFullPath, e.FullPath));
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
            : base(
                new Searcher<string, string>(invertedIndex),
                new Indexer<string, string>(forwardIndex, invertedIndex))
        {
            _tokenizer = tokenizer;
            _watchers = new Dictionary<string, FileSystemWatcher>();
            _backgroundQueue = new BackgroundQueue();
        }
    }
}