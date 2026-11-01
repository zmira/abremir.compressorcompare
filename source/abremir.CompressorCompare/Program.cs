using System.CommandLine;
using System.Data;
using System.Diagnostics;
using System.IO.Compression;
using System.Reflection;
using System.Text;
using abremir.CompressorCompare;
using abremir.CompressorCompare.Compressors;
using Humanizer;
using Kurukuru;
using Pastel;
using WaffleGenerator;

if (!Debugger.IsAttached)
{
    Console.OutputEncoding = Encoding.UTF8;
}

Console.Title = "🗜️ Compressor Compare";

List<Type> compressorTypes = [];

Spinner.Start(
    "Loading compressor types...",
    () => compressorTypes = [.. Assembly.GetExecutingAssembly().GetTypes()
        .Where(type => typeof(ICompressor)
        .IsAssignableFrom(type) && !type.IsAbstract)
        .Where(type => type is not null)],
    Patterns.GrowVertical);

Console.Clear();
Console.WriteLine(CompressorCompareFont.Computer.Render("Compressor Compare"));

var rootCommand = new RootCommand("🗜️ Compare data compressors head-to-head");

var compressionMethodOption = new Option<string>("--compression-method", ["-m"])
{
    Description = "Compression method to be used",
    DefaultValueFactory = result => "All"
};
compressionMethodOption.AcceptOnlyFromAmong([.. compressorTypes.Select(compressor => compressor.Name), "All"]);

var compressionLevelOption = new Option<CompressionLevel>("--compression-level", ["-c"])
{
    Description = "Compression level to be used",
    DefaultValueFactory = result => CompressionLevel.SmallestSize
};
compressionLevelOption.AcceptOnlyFromAmong([.. Enum.GetValues<CompressionLevel>().Where(value => value is not CompressionLevel.NoCompression).Select(value => value.ToString())]);

var numberOfRunsOption = new Option<int>("--number-of-runs", ["-n"])
{
    Description = "Number of runs for multiple-run tests (>= 0)",
    DefaultValueFactory = result => 5
};
numberOfRunsOption.Validators.Add(result =>
{
    if (result.Tokens.Any() && (!int.TryParse(result.Tokens[0].Value, out int runs) || runs < 0))
    {
        result.AddError("The specified number of runs is not valid");
    }
});

var runStressTestOption = new Option<bool>("--stress-test", ["-t"])
{
    Description = "Run stress test",
    DefaultValueFactory = result => true
};

Command randomCommand = new("random", "Test compression with randomly generated data");

var sizeOption = new Option<int>("--size", ["-s"])
{
    Description = "Size of random dataset in MB (> 0)",
    DefaultValueFactory = result => 10
};
sizeOption.Validators.Add(result =>
{
    if (result.Tokens.Any() && (!int.TryParse(result.Tokens[0].Value, out int size) || size <= 0))
    {
        result.AddError("The specified size is not valid.");
    }
});

var randomizationTypeOption = new Option<RandomizationType>("--randomization-type", ["-r"])
{
    Description = "Randomization type for the random data",
    DefaultValueFactory = result => RandomizationType.LowCompressibility
};

randomCommand.Options.Add(sizeOption);
randomCommand.Options.Add(randomizationTypeOption);
randomCommand.Options.Add(compressionLevelOption);
randomCommand.Options.Add(numberOfRunsOption);
randomCommand.Options.Add(compressionMethodOption);
randomCommand.Options.Add(runStressTestOption);

rootCommand.Subcommands.Add(randomCommand);

Command fileCommand = new("file", "Test compression with a file");

Argument<string> filePathArgument = new("file-path")
{
    Description = "Path to the file to use"
};
filePathArgument.AcceptLegalFilePathsOnly();
filePathArgument.Validators.Add(result =>
{
    if (result.Tokens.Any() && !File.Exists(result.Tokens[0].Value))
    {
        result.AddError("The specified file does not exist");
    }
});

fileCommand.Arguments.Add(filePathArgument);

fileCommand.Options.Add(compressionLevelOption);
fileCommand.Options.Add(numberOfRunsOption);
fileCommand.Options.Add(compressionMethodOption);
fileCommand.Options.Add(runStressTestOption);

rootCommand.Subcommands.Add(fileCommand);

randomCommand.SetAction(async parseResult =>
{
    int sizeInMB = parseResult.GetValue(sizeOption);
    RandomizationType randomizationType = parseResult.GetValue(randomizationTypeOption);
    string compressionMethod = parseResult.GetValue(compressionMethodOption) ?? "All";
    CompressionLevel compressionLevel = parseResult.GetValue(compressionLevelOption);
    int numberOfRuns = parseResult.GetValue(numberOfRunsOption);
    bool runStressTest = parseResult.GetValue(runStressTestOption);

    var sizeInBytes = sizeInMB * 1024 * 1024;

    PrintSettings("Randomized data", sizeInBytes, compressionMethod, compressionLevel, numberOfRuns, runStressTest, randomizationType);

    byte[] dataset = [];

    switch (randomizationType)
    {
        case RandomizationType.HighCompressibility:
        case RandomizationType.LowCompressibility:
            Spinner.Start(
                "Generating random data...",
                () => dataset = GenerateDataset(sizeInBytes, randomizationType));
            break;
        case RandomizationType.GeneratedText:
            Spinner.Start(
                "Generating random text...",
                () => dataset = GenerateTextDataset(sizeInBytes));
            break;
    }

    RunCompressors(dataset, compressionLevel, numberOfRuns, compressionMethod, runStressTest);

    EndRun();

});

fileCommand.SetAction(async parseResult =>
{
    string filePath = parseResult.GetValue(filePathArgument) ?? throw new ArgumentException("file path");
    string compressionMethod = parseResult.GetValue(compressionMethodOption) ?? "All";
    CompressionLevel compressionLevel = parseResult.GetValue(compressionLevelOption);
    int numberOfRuns = parseResult.GetValue(numberOfRunsOption);
    bool runStressTest = parseResult.GetValue(runStressTestOption);

    PrintSettings("File", new FileInfo(filePath).Length, compressionMethod, compressionLevel, numberOfRuns, runStressTest);

    byte[] dataset = [];

    Spinner.Start(
        "Reading file...",
        async () => dataset = await File.ReadAllBytesAsync(filePath),
        Patterns.GrowVertical);

    RunCompressors(dataset, compressionLevel, numberOfRuns, compressionMethod, runStressTest);

    EndRun();
});

await rootCommand.Parse(args).InvokeAsync();

static byte[] GenerateDataset(int sizeInBytes, RandomizationType randomizationStyle)
{
    if (sizeInBytes <= 0)
    {
        throw new ArgumentException("Size must be greater than zero.", nameof(sizeInBytes));
    }

    byte[] data = new byte[sizeInBytes];

    if (randomizationStyle == RandomizationType.HighCompressibility)
    {
        // Create a repeating pattern (e.g., "ABC123") to make it highly compressible
        byte[] pattern = Encoding.UTF8.GetBytes("ABC123");
        for (int i = 0; i < sizeInBytes; i++)
        {
            data[i] = pattern[i % pattern.Length];
        }
    }
    else
    {
        // Fill with random bytes (low compressibility)
        Random.Shared.NextBytes(data);
    }

    return data;
}

static byte[] GenerateTextDataset(int sizeInBytes)
{
    if (sizeInBytes <= 0)
    {
        throw new ArgumentException("Size must be greater than zero.", nameof(sizeInBytes));
    }

    byte[] data = new byte[sizeInBytes];
    StringBuilder sb = new();
    while (sb.Length < sizeInBytes)
    {
        sb.Append(WaffleEngine.Text(Random.Shared.Next(20) + 1, true));
    }

    byte[] textBytes = Encoding.UTF8.GetBytes(sb.ToString());
    Array.Copy(textBytes, data, sizeInBytes);

    return data;
}

static void PrintSettings(string dataSource, long dataSizeInBytes, string compressionMethod, CompressionLevel compressionLevel, int numberOfRuns, bool runStressTest, RandomizationType? randomizationType = null)
{
    TestCompressor.PrintResult("Data Source", dataSource, ConsoleColor.DarkYellow);
    if (randomizationType is not null)
    {
        TestCompressor.PrintResult("Randomization Type", randomizationType.Value.Humanize(), ConsoleColor.DarkYellow);
    }
    TestCompressor.PrintResult("Data Size", dataSizeInBytes.Bytes().Humanize(), ConsoleColor.DarkYellow);
    TestCompressor.PrintResult("Compression Method", compressionMethod, ConsoleColor.DarkYellow);
    TestCompressor.PrintResult("Compression Level", compressionLevel.Humanize(), ConsoleColor.DarkYellow);
    TestCompressor.PrintResult("Number of Runs", numberOfRuns.ToString(), ConsoleColor.DarkYellow);
    TestCompressor.PrintResult("Run Stress Test", runStressTest.ToString(), ConsoleColor.DarkYellow);
}

void RunCompressors(byte[] dataset, CompressionLevel compressionLevel, int numberOfRuns, string compressionMethod, bool runStressTests)
{
    foreach (var type in compressorTypes)
    {
        if (!compressionMethod.Equals("All", StringComparison.OrdinalIgnoreCase) && !type.Name.Equals(compressionMethod, StringComparison.OrdinalIgnoreCase))
        {
            continue;
        }

        var compressorInstance = (ICompressor)Activator.CreateInstance(type)!;

        new TestCompressor(compressorInstance, dataset, type.Name, compressionLevel, numberOfRuns, runStressTests).RunTests();
    }
}

static void EndRun()
{
    Console.Write("Press any key to exit...".Pastel(ConsoleColor.White));
    Console.ReadKey();
}