using System.Diagnostics;
using System.Text.Json;
using System.Xml.Linq;

namespace KnuthPlass.Cli.Tests;

public sealed class CliProcessTests
{
    private static readonly string[] AllArtifactNames =
    [
        "comparison.html",
        "layout-comparison.svg",
        "breakpoint-graph.svg",
        "summary.json",
        "trace.txt",
    ];

    [Fact]
    public async Task HelpReturnsSuccessWithoutArtifacts()
    {
        using var temporary = new TemporaryDirectory();

        var result = await RunAsync(temporary.Path, "--help");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Usage:", result.Output, StringComparison.Ordinal);
        Assert.Empty(result.Error);
        Assert.Empty(Directory.EnumerateFiles(temporary.Path));
    }

    [Fact]
    public async Task BothAlgorithmsProduceDeterministicWellFormedArtifacts()
    {
        using var temporary = new TemporaryDirectory();
        var output = System.IO.Path.Combine(temporary.Path, "reports");
        string[] arguments =
        [
            "--text", "aa bb cc",
            "--width", "5",
            "--output", output,
            "--trace",
        ];

        var first = await RunAsync(temporary.Path, arguments);
        Assert.Equal(0, first.ExitCode);
        Assert.Empty(first.Error);
        Assert.Contains(
            "totals are equal within epsilon; no improvement is claimed",
            first.Output,
            StringComparison.Ordinal);
        Assert.Contains("Algorithm     Status", first.Output, StringComparison.Ordinal);
        Assert.Contains("Trace algorithm: Knuth-Plass", first.Output, StringComparison.Ordinal);
        Assert.Equal(
            AllArtifactNames,
            Directory.EnumerateFiles(output)
                .Select(System.IO.Path.GetFileName)
                .OrderBy(NameRank)
                .ToArray());

        var firstBytes = AllArtifactNames.ToDictionary(
            name => name,
            name => File.ReadAllBytes(System.IO.Path.Combine(output, name)),
            StringComparer.Ordinal);

        using var json = JsonDocument.Parse(
            File.ReadAllText(System.IO.Path.Combine(output, "summary.json")));
        Assert.Equal(
            2,
            json.RootElement.GetProperty("algorithms").GetArrayLength());
        _ = XDocument.Load(System.IO.Path.Combine(output, "comparison.html"));
        _ = XDocument.Load(System.IO.Path.Combine(output, "layout-comparison.svg"));
        _ = XDocument.Load(System.IO.Path.Combine(output, "breakpoint-graph.svg"));
        Assert.StartsWith(
            "Options targetWidth=5",
            File.ReadAllText(System.IO.Path.Combine(output, "trace.txt")),
            StringComparison.Ordinal);

        var second = await RunAsync(temporary.Path, arguments);
        Assert.Equal(0, second.ExitCode);
        foreach (var name in AllArtifactNames)
        {
            Assert.Equal(
                firstBytes[name],
                File.ReadAllBytes(System.IO.Path.Combine(output, name)));
        }
    }

    [Fact]
    public async Task ConflictingInputModesAndInvalidWidthReturnUsageError()
    {
        using var temporary = new TemporaryDirectory();

        var conflicting = await RunAsync(
            temporary.Path,
            "--text", "words",
            "--file", "input.txt",
            "--width", "5");
        var invalidWidth = await RunAsync(
            temporary.Path,
            "--text", "words",
            "--width", "NaN");

        Assert.Equal(2, conflicting.ExitCode);
        Assert.Contains("Exactly one", conflicting.Error, StringComparison.Ordinal);
        Assert.Equal(2, invalidWidth.ExitCode);
        Assert.Contains("finite", invalidWidth.Error, StringComparison.Ordinal);
        Assert.DoesNotContain(" at ", conflicting.Error, StringComparison.Ordinal);
        Assert.DoesNotContain(" at ", invalidWidth.Error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task MissingFileReturnsIoErrorAndInvalidUtf8ReturnsInputError()
    {
        using var temporary = new TemporaryDirectory();
        var invalidPath = System.IO.Path.Combine(temporary.Path, "invalid.txt");
        await File.WriteAllBytesAsync(invalidPath, [0xFF]);

        var missing = await RunAsync(
            temporary.Path,
            "--file", "missing.txt",
            "--width", "5");
        var invalid = await RunAsync(
            temporary.Path,
            "--file", invalidPath,
            "--width", "5");

        Assert.Equal(4, missing.ExitCode);
        Assert.Contains("Could not read input file", missing.Error, StringComparison.Ordinal);
        Assert.Equal(2, invalid.ExitCode);
        Assert.Contains("not valid UTF-8", invalid.Error, StringComparison.Ordinal);
        Assert.DoesNotContain(" at ", missing.Error, StringComparison.Ordinal);
        Assert.DoesNotContain(" at ", invalid.Error, StringComparison.Ordinal);

        var utf16LittleEndian = System.IO.Path.Combine(temporary.Path, "utf16-le.txt");
        var utf16BigEndian = System.IO.Path.Combine(temporary.Path, "utf16-be.txt");
        var utf8Bom = System.IO.Path.Combine(temporary.Path, "utf8-bom.txt");
        await File.WriteAllBytesAsync(utf16LittleEndian, [0xFF, 0xFE, 0x61, 0x00]);
        await File.WriteAllBytesAsync(utf16BigEndian, [0xFE, 0xFF, 0x00, 0x61]);
        await File.WriteAllBytesAsync(utf8Bom, [0xEF, 0xBB, 0xBF, 0x61]);

        var littleEndian = await RunAsync(
            temporary.Path,
            "--file", utf16LittleEndian,
            "--width", "5");
        var bigEndian = await RunAsync(
            temporary.Path,
            "--file", utf16BigEndian,
            "--width", "5");
        var validBom = await RunAsync(
            temporary.Path,
            "--file", utf8Bom,
            "--width", "1",
            "--max-input-length", "1",
            "--output", System.IO.Path.Combine(temporary.Path, "utf8-bom"));

        Assert.Equal(2, littleEndian.ExitCode);
        Assert.Equal(2, bigEndian.ExitCode);
        Assert.Equal(0, validBom.ExitCode);
        Assert.Contains("not valid UTF-8", littleEndian.Error, StringComparison.Ordinal);
        Assert.Contains("not valid UTF-8", bigEndian.Error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task MaximumInputLengthIsEnforcedForTextAndFiles()
    {
        using var temporary = new TemporaryDirectory();
        var exactFile = System.IO.Path.Combine(temporary.Path, "exact.txt");
        var longFile = System.IO.Path.Combine(temporary.Path, "long.txt");
        await File.WriteAllTextAsync(exactFile, "abcde");
        await File.WriteAllTextAsync(longFile, "abcdef");

        var exactText = await RunAsync(
            temporary.Path,
            "--text", "abcde",
            "--width", "5",
            "--max-input-length", "5",
            "--output", System.IO.Path.Combine(temporary.Path, "text-exact"));
        var exactFromFile = await RunAsync(
            temporary.Path,
            "--file", exactFile,
            "--width", "5",
            "--max-input-length", "5",
            "--output", System.IO.Path.Combine(temporary.Path, "file-exact"));
        var longText = await RunAsync(
            temporary.Path,
            "--text", "abcdef",
            "--width", "6",
            "--max-input-length", "5");
        var longFromFile = await RunAsync(
            temporary.Path,
            "--file", longFile,
            "--width", "6",
            "--max-input-length", "5");

        Assert.Equal(0, exactText.ExitCode);
        Assert.Equal(0, exactFromFile.ExitCode);
        Assert.Equal(2, longText.ExitCode);
        Assert.Equal(2, longFromFile.ExitCode);
        Assert.Contains("5 character limit", longText.Error, StringComparison.Ordinal);
        Assert.Contains("5 character limit", longFromFile.Error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task MaximumInputLengthOptionRangeIsValidated()
    {
        using var temporary = new TemporaryDirectory();

        var upper = await RunAsync(
            temporary.Path,
            "--text", "a",
            "--width", "1",
            "--max-input-length", "1000000",
            "--output", System.IO.Path.Combine(temporary.Path, "upper"));
        var zero = await RunAsync(
            temporary.Path,
            "--text", "a",
            "--width", "1",
            "--max-input-length", "0");
        var above = await RunAsync(
            temporary.Path,
            "--text", "a",
            "--width", "1",
            "--max-input-length", "1000001");

        Assert.Equal(0, upper.ExitCode);
        Assert.Equal(2, zero.ExitCode);
        Assert.Equal(2, above.ExitCode);
        Assert.Contains("1 through 1000000", zero.Error, StringComparison.Ordinal);
        Assert.Contains("1 through 1000000", above.Error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task StrictInfeasibleLayoutReturnsThreeWithFailureArtifacts()
    {
        using var temporary = new TemporaryDirectory();
        var output = System.IO.Path.Combine(temporary.Path, "reports");

        var result = await RunAsync(
            temporary.Path,
            "--text", "unbreakable",
            "--width", "3",
            "--algorithm", "knuth-plass",
            "--output", output);

        Assert.Equal(3, result.ExitCode);
        Assert.Contains("failure", result.Output, StringComparison.Ordinal);
        Assert.Contains("NoFeasibleLayout", result.Output, StringComparison.Ordinal);
        Assert.Contains("did not reach", result.Error, StringComparison.Ordinal);
        Assert.True(File.Exists(System.IO.Path.Combine(output, "summary.json")));
        Assert.True(File.Exists(System.IO.Path.Combine(output, "breakpoint-graph.svg")));
        Assert.False(File.Exists(System.IO.Path.Combine(output, "trace.txt")));
    }

    [Fact]
    public async Task OutputPathThatIsAFileReturnsIoError()
    {
        using var temporary = new TemporaryDirectory();
        var blocked = System.IO.Path.Combine(temporary.Path, "blocked");
        await File.WriteAllTextAsync(blocked, "not a directory");

        var result = await RunAsync(
            temporary.Path,
            "--text", "aa bb",
            "--width", "5",
            "--output", blocked);

        Assert.Equal(4, result.ExitCode);
        Assert.Contains("Could not write artifacts", result.Error, StringComparison.Ordinal);
        Assert.Equal("not a directory", await File.ReadAllTextAsync(blocked));
    }

    [Fact]
    public async Task MidPromotionFailureRestoresOldArtifactsAndVerboseControlsStacks()
    {
        using var temporary = new TemporaryDirectory();
        var output = System.IO.Path.Combine(temporary.Path, "reports");
        Directory.CreateDirectory(output);
        var oldArtifacts = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["comparison.html"] = "old html",
            ["layout-comparison.svg"] = "old layout",
            ["summary.json"] = "old summary",
            ["trace.txt"] = "old trace",
        };
        foreach (var pair in oldArtifacts)
        {
            await File.WriteAllTextAsync(
                System.IO.Path.Combine(output, pair.Key),
                pair.Value);
        }

        Directory.CreateDirectory(
            System.IO.Path.Combine(output, "breakpoint-graph.svg"));
        string[] arguments =
        [
            "--text", "aa bb cc",
            "--width", "5",
            "--output", output,
        ];

        var ordinary = await RunAsync(temporary.Path, arguments);

        Assert.Equal(4, ordinary.ExitCode);
        Assert.DoesNotContain("System.IO.IOException", ordinary.Error, StringComparison.Ordinal);
        Assert.DoesNotContain(" at ", ordinary.Error, StringComparison.Ordinal);
        foreach (var pair in oldArtifacts)
        {
            Assert.Equal(
                pair.Value,
                await File.ReadAllTextAsync(System.IO.Path.Combine(output, pair.Key)));
        }

        Assert.DoesNotContain(
            Directory.EnumerateFiles(output),
            path => System.IO.Path.GetFileName(path).StartsWith(
                ".", StringComparison.Ordinal));

        var verbose = await RunAsync(
            temporary.Path,
            [.. arguments, "--verbose"]);

        Assert.Equal(4, verbose.ExitCode);
        Assert.Contains("System.IO.IOException", verbose.Error, StringComparison.Ordinal);
        Assert.Contains(" at ", verbose.Error, StringComparison.Ordinal);
        foreach (var pair in oldArtifacts)
        {
            Assert.Equal(
                pair.Value,
                await File.ReadAllTextAsync(System.IO.Path.Combine(output, pair.Key)));
        }

        Assert.DoesNotContain(
            Directory.EnumerateFiles(output),
            path => System.IO.Path.GetFileName(path).StartsWith(
                ".", StringComparison.Ordinal));
    }

    [Fact]
    public async Task SingleGreedyRunRemovesStaleGraphAndOmitsItsLink()
    {
        using var temporary = new TemporaryDirectory();
        var output = System.IO.Path.Combine(temporary.Path, "reports");

        var both = await RunAsync(
            temporary.Path,
            "--text", "aa bb cc",
            "--width", "5",
            "--output", output);
        Assert.Equal(0, both.ExitCode);
        Assert.True(File.Exists(System.IO.Path.Combine(output, "breakpoint-graph.svg")));

        var greedy = await RunAsync(
            temporary.Path,
            "--text", "aa bb cc",
            "--width", "5",
            "--algorithm", "greedy",
            "--output", output,
            "--trace");

        Assert.Equal(0, greedy.ExitCode);
        Assert.False(File.Exists(System.IO.Path.Combine(output, "breakpoint-graph.svg")));
        Assert.True(File.Exists(System.IO.Path.Combine(output, "trace.txt")));
        Assert.DoesNotContain(
            "href=\"breakpoint-graph.svg\"",
            await File.ReadAllTextAsync(System.IO.Path.Combine(output, "comparison.html")),
            StringComparison.Ordinal);
        using var json = JsonDocument.Parse(
            File.ReadAllText(System.IO.Path.Combine(output, "summary.json")));
        Assert.Equal(1, json.RootElement.GetProperty("algorithms").GetArrayLength());
    }

    [Fact]
    public async Task ManagedArtifactCannotAlsoBeTheInputFile()
    {
        using var temporary = new TemporaryDirectory();
        var input = System.IO.Path.Combine(temporary.Path, "summary.json");
        await File.WriteAllTextAsync(input, "aa bb");

        var result = await RunAsync(
            temporary.Path,
            "--file", input,
            "--width", "5",
            "--output", temporary.Path);

        Assert.Equal(2, result.ExitCode);
        Assert.Contains("input file cannot", result.Error, StringComparison.Ordinal);
        Assert.Equal("aa bb", await File.ReadAllTextAsync(input));
    }

    private static async Task<ProcessResult> RunAsync(
        string workingDirectory,
        params string[] arguments)
    {
        var assembly = System.IO.Path.Combine(
            AppContext.BaseDirectory,
            "KnuthPlass.Cli.dll");
        Assert.True(File.Exists(assembly), $"CLI assembly was not found at '{assembly}'.");

        var startInfo = new ProcessStartInfo
        {
            FileName = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet",
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        startInfo.ArgumentList.Add(assembly);
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        startInfo.Environment["DOTNET_NOLOGO"] = "1";
        startInfo.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";

        using var process = Process.Start(startInfo) ??
            throw new InvalidOperationException("The CLI process could not be started.");
        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await process.WaitForExitAsync(timeout.Token);
        return new ProcessResult(
            process.ExitCode,
            await outputTask,
            await errorTask);
    }

    private static int NameRank(string? name) =>
        Array.IndexOf(AllArtifactNames, name);

    private sealed record ProcessResult(int ExitCode, string Output, string Error);

    private sealed class TemporaryDirectory : IDisposable
    {
        private static readonly string TestRoot = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "knuth-plass-cli-tests");

        public TemporaryDirectory()
        {
            Directory.CreateDirectory(TestRoot);
            Path = System.IO.Path.Combine(TestRoot, Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (!Directory.Exists(Path))
            {
                return;
            }

            var root = System.IO.Path.GetFullPath(TestRoot)
                .TrimEnd(System.IO.Path.DirectorySeparatorChar) +
                System.IO.Path.DirectorySeparatorChar;
            var target = System.IO.Path.GetFullPath(Path);
            var comparison = OperatingSystem.IsWindows()
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;
            if (!target.StartsWith(root, comparison))
            {
                throw new InvalidOperationException(
                    $"Refusing to delete unexpected test path '{target}'.");
            }

            Directory.Delete(target, recursive: true);
        }
    }
}
