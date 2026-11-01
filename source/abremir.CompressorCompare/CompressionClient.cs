using System.IO.Compression;
using abremir.CompressorCompare.Compressors;
using Pastel;

namespace abremir.CompressorCompare;

internal class CompressionClient(ICompressor compressor)
{
    private readonly ICompressor _compressor = compressor;

    public bool Compress(byte[] inputData, out byte[] compressedData, CompressionLevel compressionLevel)
    {
        compressedData = [];
        if (inputData is null || inputData.Length is 0)
        {
            Console.WriteLine("No data to compress.".Pastel(ConsoleColor.DarkYellow));
            return false;
        }

        return _compressor.Compress(inputData, ref compressedData, compressionLevel);
    }

    public bool Decompress(byte[] inputData, out byte[] decompressedData)
    {
        decompressedData = [];

        if (inputData is null || inputData.Length is 0)
        {
            Console.WriteLine("No data to decompress.".Pastel(ConsoleColor.DarkYellow));
            return false;
        }

        return _compressor.Decompress(inputData, ref decompressedData);
    }
}
