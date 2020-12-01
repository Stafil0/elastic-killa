using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ElasticKilla.Core.Indexers;
using ElasticKilla.Core.Indexes;
using ElasticKilla.Core.Searchers;
using ElasticKilla.Core.Tokenizer;

namespace ElasticKilla.Core.Analyzers
{
    public class FileAnalyzer : BaseAnalyzer<string, string>
    {
        private const string DefaultFilter = "*.*";

        private bool _disposed = false;

        private readonly Dictionary<string, FileSystemWatcher> _watchers;

        private readonly ITokenizer<string> _tokenizer;

        private IEnumerable<string> ReadTokens(string path)
        {
            // TODO: добавить чтение батчами.
            string text;
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var reader = new StreamReader(stream))
            {
                text = reader.ReadToEnd();
            }

            return _tokenizer.Tokenize(text);
        }

        #region FileSystemWatcher

        public void Subscribe(string path, string filter = null)
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

            // TODO: добавить асинхронность.
            var tasks = Directory
                .EnumerateFiles(watcher.Path)
                .Select(x => Task.Run(() => Indexer.Remove(x)));

            await Task.WhenAll(tasks);
            await Task.Yield();

            watcher.Dispose();
            _watchers.Remove(path);
        }

        private void OnFileChanged(object source, FileSystemEventArgs e)
        {
            // TODO: добавить чтение батчами.
            var tokens = ReadTokens(e.FullPath);
            Indexer.Update(e.FullPath, tokens);
        }

        private void OnFileCreated(object source, FileSystemEventArgs e)
        {
            // TODO: добавить чтение батчами.
            var tokens = ReadTokens(e.FullPath);
            Indexer.Add(e.FullPath, tokens);
        }

        private void OnFileDeleted(object source, FileSystemEventArgs e)
        {
            Indexer.Remove(e.FullPath);
        }

        private void OnFileRenamed(object source, RenamedEventArgs e)
        {
            Indexer.Switch(e.OldFullPath, e.FullPath);
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
            _watchers = new Dictionary<string, FileSystemWatcher>();
            _tokenizer = tokenizer;
        }
    }
}