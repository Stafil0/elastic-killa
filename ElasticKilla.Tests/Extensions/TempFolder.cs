using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace ElasticKilla.Tests.TestExtensions
{
    public class TempFolder : IDisposable
    {
        private readonly Random _rng = new Random();

        public List<string> Files { get; }

        public string FolderPath { get; }

        public string CreateFile(string extension = null) => CreateFile(() => string.Empty, extension);
        
        public string CreateFile(Func<string> generator, string extension = null)
        {
            var path = Path.Join(FolderPath, Path.GetRandomFileName());

            if (!string.IsNullOrWhiteSpace(extension))
                path = Path.ChangeExtension(path, extension);

            using var stream = File.Create(path);

            stream.Seek(0, SeekOrigin.Begin);
            stream.Write(Encoding.UTF8.GetBytes(generator()));

            Files.Add(path);
            return path;
        }

        public void CreateFiles(int count, Func<string> generator, string extension = null)
        {
            for (var i = 0; i < count; i++)
                CreateFile(generator, extension);
        }

        public void CreateFiles(int count, string extension = null) => CreateFiles(count, () => string.Empty, extension);

        public string RenameFile(string newName)
        {
            if (!Files.Any())
                CreateFile(() => string.Empty);

            var index = _rng.Next(Files.Count);
            var file = Files[index];
            var directory = Path.GetDirectoryName(file);
            var newFile = Path.Join(directory, newName);

            File.Move(file, newFile);
            Files.Remove(file);
            Files.Add(newFile);
            return file;
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(FolderPath))
                    Directory.Delete(FolderPath, true);
            }
            catch
            {
                // Suppress any errors.
            }

            Files.Clear();
        }

        private TempFolder()
        {
            Files = new List<string>();

            FolderPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(FolderPath);
        }

        public TempFolder(int filesCount) : this(filesCount, () => string.Empty)
        {
        }

        public TempFolder(int filesCount, Func<string> generator) : this()
        {
            CreateFiles(filesCount, generator);
        }
        
        public TempFolder(IDictionary<string, int> filesCount) : this(filesCount, () => string.Empty)
        {
        }

        public TempFolder(IDictionary<string, int> filesCount, Func<string> generator) : this()
        {
            foreach (var (extension, count) in filesCount)
                CreateFiles(count, generator, extension);
        }
    }
}