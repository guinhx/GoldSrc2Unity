using System;

namespace GoldSrc2Unity.Source.Extension;

public static class StringExtension
{
    public static string WithoutFileExtension(this string filePath)
    {
        if (string.IsNullOrEmpty(filePath))
            return null;
            
        var fileExtPos = filePath.LastIndexOf(".", StringComparison.Ordinal);
        return fileExtPos >= 0 ? filePath.Substring(fileExtPos) : filePath;
    }
}