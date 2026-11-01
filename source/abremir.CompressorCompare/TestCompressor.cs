using System.Diagnostics;
using System.IO.Compression;
using abremir.CompressorCompare.Compressors;
using Humanizer;
using Pastel;

namespace abremir.CompressorCompare;

internal class TestCompressor(ICompressor compressor, byte[] testData, string compressorName, CompressionLevel compressionLevel, int numberOfRuns, bool runStressTests)
{
    private readonly ICompressor _compressor = compressor;
    private readonly byte[] _testData = testData;
    private readonly string _compressorName = compressorName;
    private readonly CompressionLevel _compressionLevel = compressionLevel;
    private readonly int _numberOfRuns = numberOfRuns;
    private readonly bool _runStressTests = runStressTests;

    public void RunTests()
    {
        Console.WriteLine($"Testing {_compressorName}...".Pastel(ConsoleColor.Blue));

        RunBaseTests();

        if (_runStressTests)
        {
            TestStress();
        }

        if (_numberOfRuns > 0)
        {
            TestMultipleRuns();
        }

        Console.WriteLine($"Completed tests for {_compressorName}.\n".Pastel(ConsoleColor.Green));
    }

    private void RunBaseTests()
    {
        var process = Process.GetCurrentProcess();

        Stopwatch stopwatch = new();
        stopwatch.Start();

        long initialMemory = GC.GetTotalMemory(true);

        byte[] compressedData = [];
        bool compressed = _compressor.Compress(_testData, ref compressedData, _compressionLevel);

        double compressionRatio = (double)compressedData.Length / _testData.Length;
        long peakMemoryDuringCompression = GC.GetTotalMemory(true) - initialMemory;
        TimeSpan compressionCpuUsage = process.TotalProcessorTime;
        double reductionPercentage = 100 * (1 - compressionRatio);

        stopwatch.Stop();

        TimeSpan compressionTime = stopwatch.Elapsed;

        stopwatch.Restart();

        var memoryBeforeDecompress = GC.GetTotalMemory(true);

        byte[] decompressedData = [];
        bool decompressed = _compressor.Decompress(compressedData, ref decompressedData);

        long peakMemoryDuringDecompression = GC.GetTotalMemory(true) - memoryBeforeDecompress;
        TimeSpan totalCpuUsage = process.TotalProcessorTime;
        TimeSpan decompressionCpuUsage = totalCpuUsage - compressionCpuUsage;

        stopwatch.Stop();

        TimeSpan decompressionTime = stopwatch.Elapsed;
        long finalMemory = GC.GetTotalMemory(false);

        if (compressed && decompressed)
        {
            PrintResult("Compressed Size", compressedData.Length.Bytes().Humanize(), ConsoleColor.Red);
            PrintResult("Compression Time", compressionTime.Humanize(), ConsoleColor.Green);
            PrintResult("Decompression Time", decompressionTime.Humanize(), ConsoleColor.Green);
        }
        else
        {
            Console.WriteLine("Compression/Decompression failed.".Pastel(ConsoleColor.Red));
        }

        PrintResult("Compression Ratio", $"{compressionRatio:P2} (smaller is better)", ConsoleColor.Yellow);
        PrintResult("Memory Usage", (finalMemory - initialMemory).Bytes().Humanize(), ConsoleColor.Magenta);
        PrintResult("Peak Memory Usage during Compression", peakMemoryDuringCompression.Bytes().Humanize(), ConsoleColor.Blue);
        PrintResult("Peak Memory Usage during Decompression", peakMemoryDuringDecompression.Bytes().Humanize(), ConsoleColor.Blue);
        PrintResult("CPU Time for Compression", compressionCpuUsage.Humanize());
        PrintResult("CPU Time for Decompression", decompressionCpuUsage.Humanize());
        PrintResult("Data Size Reduction", $"{reductionPercentage:F2}% (higher is better)");
        PrintResult("Compression Throughput", $"{_testData.Length.Megabytes().Per(compressionTime).Humanize(TimeUnit.Second)} (higher is better)");
        PrintResult("Decompression Throughput", $"{compressedData.Length.Megabytes().Per(decompressionTime).Humanize(TimeUnit.Second)} (higher is better)");

        if (_testData.SequenceEqual(decompressedData))
        {
            PrintResult("Integrity Test", "Decompressed data matches the original", ConsoleColor.White);
        }
        else
        {
            PrintResult("Integrity Test", "Decompressed data does not match the original", ConsoleColor.Red);
        }
    }

    private void TestStress()
    {
        int length = _testData.Length * 10;
        Console.WriteLine($"Starting Stress Test with {length.Bytes().Humanize()}");

        Stopwatch stopwatchTotal = new();
        stopwatchTotal.Start();

        int largeDataSetSize = length;
        byte[] largeTestData = new byte[largeDataSetSize];
        new Random().NextBytes(largeTestData);

        Stopwatch stopwatch = new();
        stopwatch.Start();

        byte[] compressedData = [];

        _compressor.Compress(largeTestData, ref compressedData, _compressionLevel);

        stopwatch.Stop();
        PrintResult("Large Data Compression Time", stopwatch.Elapsed.Humanize());

        stopwatch.Restart();

        byte[] decompressedData = [];

        _compressor.Decompress(compressedData, ref decompressedData);

        stopwatch.Stop();
        PrintResult("Large Data Decompression Time", stopwatch.Elapsed.Humanize());

        stopwatchTotal.Stop();
        PrintResult("Total Stress Test Time", stopwatchTotal.Elapsed.Humanize());
    }

    private void TestMultipleRuns()
    {
        Console.WriteLine($"Starting Multiple-Run Tests");

        Stopwatch stopwatch = new();
        double totalCompressionTime = 0;
        double totalDecompressionTime = 0;
        long totalMemoryUsed = 0;

        for (int i = 0; i < _numberOfRuns; i++)
        {
            byte[] compressedData = [];
            byte[] decompressedData = [];

            long initialMemory = GC.GetTotalMemory(true);

            stopwatch.Restart();

            _compressor.Compress(_testData, ref compressedData, _compressionLevel);

            stopwatch.Stop();

            totalCompressionTime += stopwatch.ElapsedMilliseconds;

            stopwatch.Restart();

            _compressor.Decompress(compressedData, ref decompressedData);

            stopwatch.Stop();

            totalDecompressionTime += stopwatch.ElapsedMilliseconds;

            long finalMemory = GC.GetTotalMemory(false);
            totalMemoryUsed += finalMemory - initialMemory;

            if (!_testData.SequenceEqual(decompressedData))
            {
                Console.WriteLine($"Integrity failed on run {i + 1}".Pastel(ConsoleColor.Red));
                break;
            }
        }

        PrintResult("Average Compression Time", TimeSpan.FromMilliseconds(totalCompressionTime / _numberOfRuns).Humanize());
        PrintResult("Average Decompression Time", TimeSpan.FromMilliseconds(totalDecompressionTime / _numberOfRuns).Humanize());
        PrintResult("Average Memory Usage", (totalMemoryUsed / _numberOfRuns).Bytes().Humanize());
    }

    public static void PrintResult(string label, string value, ConsoleColor colour = ConsoleColor.Gray)
    {
        Console.Write($"{label.PadRight(60).PadLeft(15)} : ".Pastel(ConsoleColor.Cyan));
        Console.WriteLine(value.Pastel(colour));
    }
}
