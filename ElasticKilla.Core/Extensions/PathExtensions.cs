using System.IO;

namespace ElasticKilla.Core.Extensions
{
    public static class PathExtensions
    {
        public static string NormalizePath(string path)
        {
            return Path.GetFullPath(new DirectoryInfo(path).FullName)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .ToUpperInvariant();
        }
    }
}