using System;
using System.IO;

namespace ElasticKilla.Tests.TestExtensions
{
    public class TempFile : IDisposable
    {
        private readonly string _path;

        public string FileName => Path.GetFileName(_path);

        public string FolderName => Path.GetDirectoryName(_path);

        public void Dispose()
        {
            try
            {
                if (File.Exists(_path))
                    File.Delete(_path);
            }
            catch 
            {
                // Suppress any errors.
            }
        }
        
        public TempFile()
        {
            _path = Path.GetTempFileName();
        }
    }
}