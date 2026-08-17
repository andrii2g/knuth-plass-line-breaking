using System.Globalization;
using KnuthPlass.Core.Breaking;
using KnuthPlass.Core.Parsing;

namespace KnuthPlass.Cli;

internal static class CliParser
{
    public const string HelpText = """
Usage:
  knuth-plass (--text <paragraph> | --file <path>) --width <number>
              [--algorithm both|greedy|knuth-plass]
              [--output <directory>] [--trace] [--verbose]
              [--space-width <number>] [--stretch <number>] [--shrink <number>]
              [--line-penalty <number>] [--fitness-demerit <number>]
              [--flagged-demerit <number>] [--max-ratio <number>]
              [--last-line ragged|justified]
              [--max-input-length <characters>]

Exit codes:
  0 success
  2 usage or input error
  3 no feasible layout
  4 file or output I/O error
  5 unexpected internal error
""";

    private static readonly HashSet<string> ValueOptions =
    [
        "--text",
        "--file",
        "--width",
        "--algorithm",
        "--output",
        "--space-width",
        "--stretch",
        "--shrink",
        "--line-penalty",
        "--fitness-demerit",
        "--flagged-demerit",
        "--max-ratio",
        "--last-line",
        "--max-input-length",
    ];

    public static CliParseResult Parse(IReadOnlyList<string> args)
    {
        ArgumentNullException.ThrowIfNull(args);

        if (args.Any(argument => argument is "--help" or "-h"))
        {
            return CliParseResult.Help();
        }

        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        var flags = new HashSet<string>(StringComparer.Ordinal);

        for (var index = 0; index < args.Count; index++)
        {
            var argument = args[index];
            if (argument is "--trace" or "--verbose")
            {
                if (!flags.Add(argument))
                {
                    return CliParseResult.Failed($"Option '{argument}' may be specified only once.");
                }

                continue;
            }

            if (!ValueOptions.Contains(argument))
            {
                return CliParseResult.Failed($"Unknown option '{argument}'.");
            }

            if (!values.TryAdd(argument, string.Empty))
            {
                return CliParseResult.Failed($"Option '{argument}' may be specified only once.");
            }

            if (++index >= args.Count)
            {
                return CliParseResult.Failed($"Option '{argument}' requires a value.");
            }

            values[argument] = args[index];
        }

        var hasText = values.TryGetValue("--text", out var text);
        var hasFile = values.TryGetValue("--file", out var filePath);
        if (hasText == hasFile)
        {
            return CliParseResult.Failed("Exactly one of --text and --file is required.");
        }

        if (!values.TryGetValue("--width", out var widthToken))
        {
            return CliParseResult.Failed("Option '--width' is required.");
        }

        if (!TryFiniteDouble(widthToken, out var width) || width <= 0)
        {
            return CliParseResult.Failed("--width must be a finite number greater than zero.");
        }

        var maxInputToken = values.GetValueOrDefault("--max-input-length", "100000");
        if (!int.TryParse(
                maxInputToken,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var maxInputLength) ||
            maxInputLength is <= 0 or > 1_000_000)
        {
            return CliParseResult.Failed(
                "--max-input-length must be an integer from 1 through 1000000.");
        }

        var algorithmToken = values.GetValueOrDefault("--algorithm", "both");
        var algorithm = algorithmToken switch
        {
            "both" => AlgorithmSelection.Both,
            "greedy" => AlgorithmSelection.Greedy,
            "knuth-plass" => AlgorithmSelection.KnuthPlass,
            _ => (AlgorithmSelection?)null,
        };
        if (algorithm is null)
        {
            return CliParseResult.Failed(
                "--algorithm must be one of: both, greedy, knuth-plass.");
        }

        var lastLineToken = values.GetValueOrDefault("--last-line", "ragged");
        var lastLineMode = lastLineToken switch
        {
            "ragged" => LastLineMode.Ragged,
            "justified" => LastLineMode.Justified,
            _ => (LastLineMode?)null,
        };
        if (lastLineMode is null)
        {
            return CliParseResult.Failed("--last-line must be either ragged or justified.");
        }

        if (!TryOptionDouble(values, "--space-width", 1, out var spaceWidth) ||
            !TryOptionDouble(values, "--stretch", 0.5, out var stretch) ||
            !TryOptionDouble(values, "--shrink", 1d / 3d, out var shrink) ||
            !TryOptionDouble(values, "--line-penalty", 10, out var linePenalty) ||
            !TryOptionDouble(values, "--fitness-demerit", 100, out var fitnessDemerit) ||
            !TryOptionDouble(values, "--flagged-demerit", 100, out var flaggedDemerit) ||
            !TryOptionDouble(values, "--max-ratio", 3, out var maxRatio))
        {
            return CliParseResult.Failed(
                "Numeric options must use finite invariant-culture numbers.");
        }

        if (spaceWidth < 0 || stretch < 0 || shrink < 0 ||
            linePenalty < 0 || fitnessDemerit < 0 ||
            flaggedDemerit < 0 || maxRatio < 0)
        {
            return CliParseResult.Failed(
                "Widths, flexibility, demerits, and --max-ratio must be non-negative.");
        }

        var output = values.GetValueOrDefault("--output", "artifacts");
        if (string.IsNullOrWhiteSpace(output))
        {
            return CliParseResult.Failed("--output must not be blank.");
        }

        if (hasFile && string.IsNullOrWhiteSpace(filePath))
        {
            return CliParseResult.Failed("--file must not be blank.");
        }

        try
        {
            return CliParseResult.Succeeded(new CliOptions(
                hasText ? text : null,
                hasFile ? filePath : null,
                output,
                algorithm.Value,
                flags.Contains("--trace"),
                flags.Contains("--verbose"),
                new TokenizerOptions(
                    spaceWidth,
                    stretch,
                    shrink,
                    maxInputLength),
                new LineBreakingOptions(
                    width,
                    linePenalty,
                    fitnessDemerit,
                    flaggedDemerit,
                    maxRatio,
                    lastLineMode.Value)));
        }
        catch (ArgumentException exception)
        {
            return CliParseResult.Failed(exception.Message);
        }
    }

    private static bool TryOptionDouble(
        IReadOnlyDictionary<string, string> values,
        string name,
        double defaultValue,
        out double value)
    {
        if (!values.TryGetValue(name, out var token))
        {
            value = defaultValue;
            return true;
        }

        return TryFiniteDouble(token, out value);
    }

    private static bool TryFiniteDouble(string token, out double value) =>
        double.TryParse(
            token,
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out value) &&
        double.IsFinite(value);
}
