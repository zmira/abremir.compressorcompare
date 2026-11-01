using System.IO.Compression;
using K4os.Compression.LZ4;
using K4os.Compression.LZ4.Streams;
using Pastel;

namespace abremir.CompressorCompare.Compressors;

internal class LZ4 : ICompressor
{
    public bool Compress(byte[] data, ref byte[] compressed, CompressionLevel compressionLevel)
    {
        LZ4Level level = compressionLevel switch
        {
            CompressionLevel.Fastest => LZ4Level.L00_FAST,
            CompressionLevel.SmallestSize => LZ4Level.L12_MAX,
            _ => LZ4Level.L10_OPT
        };

        try
        {
            using MemoryStream compressedStream = new();
            using (LZ4EncoderStream lz4Stream = LZ4Stream.Encode(compressedStream, level))
            {
                lz4Stream.Write(data, 0, data.Length);
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
            using LZ4DecoderStream lz4Stream = LZ4Stream.Decode(compressedStream);
            using MemoryStream decompressedStream = new();

            lz4Stream.CopyTo(decompressedStream);
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
