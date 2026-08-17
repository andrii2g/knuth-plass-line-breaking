using System.Globalization;
using System.Text;
using KnuthPlass.Core.Breaking;
using KnuthPlass.Core.Model;
using KnuthPlass.Core.Parsing;
using KnuthPlass.Core.Results;
using KnuthPlass.Core.Tracing;
using KnuthPlass.Rendering.Html;
using KnuthPlass.Rendering.Json;
using KnuthPlass.Rendering.Svg;
using KnuthPlass.Rendering.Text;

namespace KnuthPlass.Cli;

public static class CliApplication
{
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    public static async Task<int> RunAsync(
        IReadOnlyList<string> args,
        TextWriter standardOutput,
        TextWriter standardError,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(standardOutput);
        ArgumentNullException.ThrowIfNull(standardError);

        try
        {
            return await RunCoreAsync(
                args,
                standardOutput,
                standardError,
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            await WriteExceptionAsync(
                standardError,
                "Unexpected internal error.",
                exception,
                args.Contains("--verbose", StringComparer.Ordinal)).ConfigureAwait(false);
            return ExitCodes.UnexpectedError;
        }
    }

    private static async Task<int> RunCoreAsync(
        IReadOnlyList<string> args,
        TextWriter standardOutput,
        TextWriter standardError,
        CancellationToken cancellationToken)
    {
        var parsed = CliParser.Parse(args);
        if (parsed.ShowHelp)
        {
            await standardOutput.WriteAsync(CliParser.HelpText).ConfigureAwait(false);
            return ExitCodes.Success;
        }

        if (parsed.Error is { } parseError)
        {
            await WriteExpectedErrorAsync(standardError, parseError).ConfigureAwait(false);
            return ExitCodes.UsageOrInputError;
        }

        var cli = parsed.Options!;
        string sourceText;
        try
        {
            sourceText = cli.Text ??
                await ReadUtf8FileAsync(
                    cli.FilePath!,
                    cli.TokenizerOptions.MaxInputLength,
                    cancellationToken).ConfigureAwait(false);
        }
        catch (DecoderFallbackException exception)
        {
            await WriteExceptionAsync(
                standardError,
                "Input file is not valid UTF-8.",
                exception,
                cli.Verbose).ConfigureAwait(false);
            return ExitCodes.UsageOrInputError;
        }
        catch (ArgumentException exception)
        {
            await WriteExceptionAsync(
                standardError,
                exception.Message,
                exception,
                cli.Verbose).ConfigureAwait(false);
            return ExitCodes.UsageOrInputError;
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
            await WriteExceptionAsync(
                standardError,
                $"Could not read input file '{cli.FilePath}'.",
                exception,
                cli.Verbose).ConfigureAwait(false);
            return ExitCodes.IoError;
        }

        Paragraph paragraph;
        try
        {
            paragraph = new ParagraphTokenizer().Tokenize(
                sourceText,
                cli.TokenizerOptions);
            ValidateInputOutputSeparation(cli);
        }
        catch (ArgumentException exception)
        {
            await WriteExceptionAsync(
                standardError,
                exception.Message,
                exception,
                cli.Verbose).ConfigureAwait(false);
            return ExitCodes.UsageOrInputError;
        }

        try
        {
            var results = RunAlgorithms(paragraph, cli, out var graphResult);
            var artifacts = RenderArtifacts(paragraph, cli, results, graphResult);
            var paths = await AtomicArtifactWriter.WriteAllAsync(
                cli.OutputDirectory,
                artifacts,
                cancellationToken).ConfigureAwait(false);

            await WriteReportAsync(
                standardOutput,
                paragraph,
                cli,
                results,
                paths,
                graphResult).ConfigureAwait(false);

            if (results.Any(result => !result.IsSuccess))
            {
                await WriteExpectedErrorAsync(
                    standardError,
                    "One or more algorithms did not reach a feasible final layout.")
                    .ConfigureAwait(false);
                return ExitCodes.NoFeasibleLayout;
            }

            return ExitCodes.Success;
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
            await WriteExceptionAsync(
                standardError,
                $"Could not write artifacts to '{cli.OutputDirectory}'.",
                exception,
                cli.Verbose).ConfigureAwait(false);
            return ExitCodes.IoError;
        }
        catch (Exception exception)
        {
            await WriteExceptionAsync(
                standardError,
                "Unexpected internal error.",
                exception,
                cli.Verbose).ConfigureAwait(false);
            return ExitCodes.UnexpectedError;
        }
    }

    private static LineBreakResult[] RunAlgorithms(
        Paragraph paragraph,
        CliOptions cli,
        out LineBreakResult? graphResult)
    {
        var results = new List<LineBreakResult>(2);
        graphResult = null;

        if (cli.Algorithm is AlgorithmSelection.Both or AlgorithmSelection.Greedy)
        {
            var greedyTrace = cli.Trace && cli.Algorithm == AlgorithmSelection.Greedy
                ? new InMemoryTraceSink()
                : null;
            results.Add(new GreedyLineBreaker().Break(
                paragraph,
                cli.LineBreakingOptions,
                greedyTrace));
        }

        if (cli.Algorithm is AlgorithmSelection.Both or AlgorithmSelection.KnuthPlass)
        {
            var graphTrace = cli.Trace ? new InMemoryTraceSink() : null;
            graphResult = new KnuthPlassLineBreaker().Break(
                paragraph,
                cli.LineBreakingOptions,
                graphTrace,
                captureGraph: true);
            results.Add(graphResult);
        }

        return [.. results];
    }

    private static Dictionary<string, string> RenderArtifacts(
        Paragraph paragraph,
        CliOptions cli,
        LineBreakResult[] results,
        LineBreakResult? graphResult)
    {
        var artifacts = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["comparison.html"] = new HtmlComparisonRenderer().Render(
                paragraph,
                cli.LineBreakingOptions,
                results,
                includeTraceLink: cli.Trace),
            ["layout-comparison.svg"] = new LayoutComparisonSvgRenderer().Render(
                cli.LineBreakingOptions,
                results),
        };

        if (graphResult is not null)
        {
            artifacts.Add(
                "breakpoint-graph.svg",
                new BreakpointGraphSvgRenderer().Render(graphResult));
        }

        artifacts.Add(
            "summary.json",
            new SummaryJsonRenderer().Render(
                paragraph,
                cli.LineBreakingOptions,
                results));

        if (cli.Trace)
        {
            var tracedResult = graphResult ?? results.Single();
            artifacts.Add(
                "trace.txt",
                new TraceTextRenderer().Render(
                    tracedResult.Trace ??
                    throw new InvalidOperationException(
                        "Trace output was requested without captured events.")));
        }

        return artifacts;
    }

    private static async Task<string> ReadUtf8FileAsync(
        string path,
        int maxInputLength,
        CancellationToken cancellationToken)
    {
        var maximumBytes = checked((long)maxInputLength * 4 + 3);
        await using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            81920,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        using var buffer = new MemoryStream(
            (int)Math.Min(stream.Length, maximumBytes));
        var block = new byte[81920];
        long totalBytes = 0;

        while (true)
        {
            var count = await stream.ReadAsync(
                block.AsMemory(),
                cancellationToken).ConfigureAwait(false);
            if (count == 0)
            {
                break;
            }

            totalBytes += count;
            if (totalBytes > maximumBytes)
            {
                throw new ArgumentException(
                    $"Input exceeds the {maxInputLength} character limit.");
            }

            await buffer.WriteAsync(
                block.AsMemory(0, count),
                cancellationToken).ConfigureAwait(false);
        }

        var bytes = buffer.GetBuffer();
        var length = checked((int)buffer.Length);
        var offset = length >= 3 &&
            bytes[0] == 0xEF &&
            bytes[1] == 0xBB &&
            bytes[2] == 0xBF
                ? 3
                : 0;
        var text = StrictUtf8.GetString(bytes, offset, length - offset);
        if (text.Length > maxInputLength)
        {
            throw new ArgumentException(
                $"Input exceeds the {maxInputLength} character limit.");
        }

        return text;
    }

    private static void ValidateInputOutputSeparation(CliOptions cli)
    {
        if (cli.FilePath is null)
        {
            return;
        }

        var input = Path.GetFullPath(cli.FilePath);
        var output = Path.GetFullPath(cli.OutputDirectory);
        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        if (AtomicArtifactWriter.ManagedNames.Any(name =>
            string.Equals(input, Path.Combine(output, name), comparison)))
        {
            throw new ArgumentException(
                "The input file cannot be one of the managed artifact paths.");
        }
    }

    private static async Task WriteReportAsync(
        TextWriter output,
        Paragraph paragraph,
        CliOptions cli,
        IReadOnlyList<LineBreakResult> results,
        IReadOnlyDictionary<string, string> paths,
        LineBreakResult? graphResult)
    {
        await output.WriteLineAsync(
            $"Input: words={paragraph.Words.Length.ToString(CultureInfo.InvariantCulture)} " +
            $"breakpoints={paragraph.Breakpoints.Length.ToString(CultureInfo.InvariantCulture)} " +
            $"targetWidth={Number(cli.LineBreakingOptions.TargetWidth)}")
            .ConfigureAwait(false);

        if (paragraph.HadLineBreaks)
        {
            await output.WriteLineAsync(
                "Input normalization: line breaks were normalized to word separators.")
                .ConfigureAwait(false);
        }

        await output.WriteLineAsync(
            "Algorithm     Status   Lines          Total demerits")
            .ConfigureAwait(false);
        await output.WriteLineAsync(
            "------------- -------- -------------- ---------------")
            .ConfigureAwait(false);
        foreach (var result in results)
        {
            var lineCount = result.Metrics?.LineCount.ToString(CultureInfo.InvariantCulture)
                ?? "not available";
            var total = result.TotalDemerits is { } value
                ? Number(value)
                : "not available";
            var status = result.IsSuccess ? "success" : "failure";
            await output.WriteLineAsync(
                $"{result.AlgorithmName,-13} {status,-8} {lineCount,-14} {total}")
                .ConfigureAwait(false);
            await output.WriteLineAsync(
                $"  path={FormatPath(result.SelectedBreakpointIds)}")
                .ConfigureAwait(false);
            if (result.FailureReason is { } failure)
            {
                await output.WriteLineAsync($"  failure={failure}")
                    .ConfigureAwait(false);
            }
        }

        WriteComparison(output, results, cli.LineBreakingOptions.Epsilon);

        await output.WriteLineAsync("Artifacts:").ConfigureAwait(false);
        foreach (var name in AtomicArtifactWriter.ManagedNames)
        {
            if (paths.TryGetValue(name, out var path))
            {
                await output.WriteLineAsync($"  {name}: {path}")
                    .ConfigureAwait(false);
            }
        }

        if (cli.Trace)
        {
            await output.WriteLineAsync(
                $"Trace algorithm: {(graphResult?.AlgorithmName ?? results.Single().AlgorithmName)}")
                .ConfigureAwait(false);
        }
    }

    private static void WriteComparison(
        TextWriter output,
        IReadOnlyList<LineBreakResult> results,
        double epsilon)
    {
        var greedy = results.FirstOrDefault(result =>
            result.AlgorithmName == GreedyLineBreaker.Name);
        var optimal = results.FirstOrDefault(result =>
            result.AlgorithmName == KnuthPlassLineBreaker.Name);
        if (greedy?.TotalDemerits is not { } greedyTotal ||
            optimal?.TotalDemerits is not { } optimalTotal)
        {
            output.WriteLine(
                "Comparison: results are not cost-comparable; statuses are reported separately.");
            return;
        }

        var difference = greedyTotal - optimalTotal;
        if (Math.Abs(difference) <= epsilon)
        {
            output.WriteLine(
                "Comparison: totals are equal within epsilon; no improvement is claimed.");
            return;
        }

        if (difference > 0)
        {
            var percent = greedyTotal > 0
                ? $" ({Number(difference / greedyTotal * 100)}%)"
                : string.Empty;
            output.WriteLine(
                $"Comparison: Knuth-Plass reduces total demerits by {Number(difference)}{percent}.");
        }
        else
        {
            output.WriteLine(
                $"Comparison: Greedy has lower total demerits by {Number(-difference)}.");
        }
    }

    private static string FormatPath(IReadOnlyList<int> breakpoints) =>
        breakpoints.Count == 0
            ? "not available"
            : string.Join(" -> ", breakpoints.Select(
                id => $"B{id.ToString("D2", CultureInfo.InvariantCulture)}"));

    private static string Number(double value) =>
        (value == 0 ? 0 : value).ToString("G17", CultureInfo.InvariantCulture);

    private static Task WriteExpectedErrorAsync(TextWriter error, string message) =>
        error.WriteLineAsync($"error: {message}");

    private static Task WriteExceptionAsync(
        TextWriter error,
        string message,
        Exception exception,
        bool verbose) =>
        error.WriteLineAsync(verbose
            ? $"error: {message}{Environment.NewLine}{exception}"
            : $"error: {message}");
}
