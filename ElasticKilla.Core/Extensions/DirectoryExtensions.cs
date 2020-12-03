using System;
using System.Diagnostics;
using System.IO;

namespace ElasticKilla.Core.Extensions
{
    public static class DirectoryExtensions
    {
        public static string[] GetFilesSafe(string path, string searchPattern)
        {
            string[] files;
            try
            {
                files = Directory.GetFiles(path, searchPattern);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Can't access to {path} with {searchPattern}: {ex.Message}");
                files = new string[0];
            }

            return files;
        }
    }
}