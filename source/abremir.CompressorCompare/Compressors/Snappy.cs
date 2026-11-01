using System.IO.Compression;
using Pastel;
using Snappier;

namespace abremir.CompressorCompare.Compressors;

internal class Snappy : ICompressor
{
    public bool Compress(byte[] data, ref byte[] compressed, CompressionLevel compressionLevel)
    {
        try
        {
            using MemoryStream compressedStream = new();
            using (SnappyStream snappyStream = new(compressedStream, CompressionMode.Compress))
            {
                snappyStream.Write(data, 0, data.Length);
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
            using SnappyStream snappyStream = new(compressedStream, CompressionMode.Decompress);
            using MemoryStream decompressedStream = new();

            snappyStream.CopyTo(decompressedStream);
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
