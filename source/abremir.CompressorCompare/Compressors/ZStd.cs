using System.IO.Compression;
using Pastel;
using ZstdSharp;

namespace abremir.CompressorCompare.Compressors;

internal class ZStd : ICompressor
{
    public bool Compress(byte[] data, ref byte[] compressed, CompressionLevel compressionLevel)
    {
        int level = compressionLevel switch
        {
            CompressionLevel.Fastest => Compressor.MinCompressionLevel,
            CompressionLevel.SmallestSize => Compressor.MaxCompressionLevel,
            _ => Compressor.DefaultCompressionLevel
        };

        try
        {
            using MemoryStream compressedStream = new();
            using (CompressionStream zstdStream = new(compressedStream, level))
            {
                zstdStream.Write(data, 0, data.Length);
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
            using DecompressionStream zstdStream = new(compressedStream);
            using MemoryStream decompressedStream = new();

            zstdStream.CopyTo(decompressedStream);
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
