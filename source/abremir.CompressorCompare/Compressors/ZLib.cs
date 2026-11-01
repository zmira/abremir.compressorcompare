using System.IO.Compression;
using Pastel;

namespace abremir.CompressorCompare.Compressors;

internal class ZLib : ICompressor
{
    public bool Compress(byte[] data, ref byte[] compressed, CompressionLevel compressionLevel)
    {
        try
        {
            using MemoryStream compressedStream = new();
            using (ZLibStream zlibStream = new(compressedStream, compressionLevel))
            {
                zlibStream.Write(data, 0, data.Length);
            }
            compressed = compressedStream.ToArray();

            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Compression failed: {ex.Message}".Pastel(ConsoleColor.Red));
            return false;
        }
    }

    public bool Decompress(byte[] data, ref byte[] decompressed)
    {
        try
        {
            using MemoryStream compressedStream = new(data);
            using ZLibStream zlibStream = new(compressedStream, CompressionMode.Decompress);
            using MemoryStream decompressedStream = new();

            zlibStream.CopyTo(decompressedStream);
            decompressed = decompressedStream.ToArray();

            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Decompression failed: {ex.Message}".Pastel(ConsoleColor.Red));
            return false;
        }
    }
}
