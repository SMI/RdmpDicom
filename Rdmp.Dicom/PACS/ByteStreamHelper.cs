using System.IO;

namespace Rdmp.Dicom.PACS;

public static class ByteStreamHelper
{
    /// <summary>
    /// Reads data from a stream until the end is reached. The
    /// data is returned as a byte array. An IOException is
    /// thrown if any of the underlying IO calls fail.
    /// </summary>
    /// <param name="stream">The stream to read data from</param>
    /// <exception cref="IOException"></exception>
    public static byte[] ReadFully(Stream stream)
    {
        var buffer = new byte[32768];
        long length;
        try
        {
            length = stream.Length;
        }
        catch
        {
            // Use default size of 32k
            length = 32768;
        }
        using var ms = new MemoryStream((int)length);
        while (true)
        {;
            var read = stream.Read(buffer, 0, buffer.Length);
            if (read <= 0)
                return ms.ToArray();
            ms.Write(buffer, 0, read);
        }
    }
}