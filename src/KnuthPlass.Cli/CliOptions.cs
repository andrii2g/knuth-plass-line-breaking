using KnuthPlass.Core.Breaking;
using KnuthPlass.Core.Parsing;

namespace KnuthPlass.Cli;

internal enum AlgorithmSelection
{
    Both,
    Greedy,
    KnuthPlass,
}

internal sealed record CliOptions(
    string? Text,
    string? FilePath,
    string OutputDirectory,
    AlgorithmSelection Algorithm,
    bool Trace,
    bool Verbose,
    TokenizerOptions TokenizerOptions,
    LineBreakingOptions LineBreakingOptions);

internal sealed record CliParseResult(
    CliOptions? Options,
    bool ShowHelp,
    string? Error)
{
    public static CliParseResult Help() => new(null, true, null);
    public static CliParseResult Failed(string error) => new(null, false, error);
    public static CliParseResult Succeeded(CliOptions options) => new(options, false, null);
}
