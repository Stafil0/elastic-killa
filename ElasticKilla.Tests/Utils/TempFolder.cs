using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using ElasticKilla.Core.Extensions;

namespace ElasticKilla.Tests.Utils
{
    public class TempFolder : IDisposable
    {
        private readonly Random _rng = new Random();

        public List<string> Files { get; }

        public readonly string FolderPath;

        public string GetRandomFile() => PathExtensions.NormalizePath(Files[_rng.Next(Files.Count)]);
        
        public string CreateFile(string extension = null) => CreateFile(() => string.Empty, extension);
        
        public string CreateFile(Func<string> generator, string extension = null)
        {
            var path = Path.Join(FolderPath, Path.GetRandomFileName());

            if (!string.IsNullOrWhiteSpace(extension))
                path = Path.ChangeExtension(path, extension);

            path = PathExtensions.NormalizePath(path);

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

        public bool ChangeFile(string name, Func<string> generator)
        {
            var normalized = PathExtensions.NormalizePath(name);
            var file = Files.FirstOrDefault(x => PathExtensions.NormalizePath(x).Equals(normalized, StringComparison.InvariantCultureIgnoreCase));
            if (string.IsNullOrEmpty(file))
                return false;

            using var stream = File.OpenWrite(file);
            
            stream.Seek(0, SeekOrigin.Begin);
            stream.Write(Encoding.UTF8.GetBytes(generator()));

            return true;
        }

        public bool RenameFile(string oldFile, string newName)
        {
            if (Files.FirstOrDefault(x => x.Equals(oldFile, StringComparison.InvariantCultureIgnoreCase)) == null)
                return false;
            
            var directory = Path.GetDirectoryName(oldFile);
            var newFile = Path.Join(directory, newName);

            newFile = PathExtensions.NormalizePath(newFile);

            File.Move(oldFile, newFile);
            Files.Remove(oldFile);
            Files.Add(newFile);
            return true;
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

            FolderPath = PathExtensions.NormalizePath(Path.Combine(Path.GetTempPath(), Path.GetRandomFileName()));
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