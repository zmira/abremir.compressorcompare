using System.IO.Compression;

namespace abremir.CompressorCompare.Compressors;

internal interface ICompressor
{
    bool Compress(byte[] data, ref byte[] compressed, CompressionLevel compressionLevel);
    bool Decompress(byte[] data, ref byte[] decompressed);
}
