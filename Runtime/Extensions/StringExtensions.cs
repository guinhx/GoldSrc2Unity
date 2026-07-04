using System;
using System.IO;

namespace Source2Unity.Extensions
{
    public static class StringExtensions
    {
        public static string WithoutExtension(this string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
                return filePath;
            return Path.ChangeExtension(filePath, null);
        }

        public static string GetDirectoryPath(this string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
                return filePath;
            return Path.GetDirectoryName(filePath) ?? string.Empty;
        }

        public static string GetFileNameWithoutExtension(this string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
                return filePath;
            return Path.GetFileNameWithoutExtension(filePath);
        }
    }
}
