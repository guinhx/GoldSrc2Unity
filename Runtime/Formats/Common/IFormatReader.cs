using System.IO;

namespace Source2Unity.Formats.Common
{
    public interface IFormatReader<out T>
    {
        T Read(Stream stream);
        T Read(string filePath);
    }
}
