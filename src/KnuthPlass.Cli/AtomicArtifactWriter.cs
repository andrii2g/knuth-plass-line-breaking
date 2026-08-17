using System.Text;

namespace KnuthPlass.Cli;

internal static class AtomicArtifactWriter
{
    private static readonly UTF8Encoding Utf8WithoutBom = new(false, true);

    public static readonly string[] ManagedNames =
    [
        "comparison.html",
        "layout-comparison.svg",
        "breakpoint-graph.svg",
        "summary.json",
        "trace.txt",
    ];

    public static async Task<IReadOnlyDictionary<string, string>> WriteAllAsync(
        string outputDirectory,
        IReadOnlyDictionary<string, string> artifacts,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);
        ArgumentNullException.ThrowIfNull(artifacts);

        var allowed = ManagedNames.ToHashSet(StringComparer.Ordinal);
        if (artifacts.Count == 0 ||
            artifacts.Keys.Any(name => !allowed.Contains(name)) ||
            artifacts.Values.Any(content => content is null))
        {
            throw new ArgumentException(
                "Artifacts must contain only supported fixed filenames and non-null content.",
                nameof(artifacts));
        }

        var directory = Path.GetFullPath(outputDirectory);
        Directory.CreateDirectory(directory);

        var staged = new Dictionary<string, string>(StringComparer.Ordinal);
        var backups = new Dictionary<string, string>(StringComparer.Ordinal);
        var promoted = new List<string>();
        var committed = false;

        try
        {
            foreach (var name in ManagedNames.Where(artifacts.ContainsKey))
            {
                var temporary = TemporaryPath(directory, name, "tmp");
                await WriteNewFileAsync(
                    temporary,
                    artifacts[name],
                    cancellationToken).ConfigureAwait(false);
                staged.Add(name, temporary);
            }

            foreach (var name in ManagedNames)
            {
                var target = Path.Combine(directory, name);
                if (!File.Exists(target))
                {
                    continue;
                }

                var backup = TemporaryPath(directory, name, "bak");
                File.Move(target, backup);
                backups.Add(name, backup);
            }

            foreach (var name in ManagedNames.Where(artifacts.ContainsKey))
            {
                var target = Path.Combine(directory, name);
                File.Move(staged[name], target);
                staged.Remove(name);
                promoted.Add(name);
            }

            committed = true;
            return artifacts.Keys.ToDictionary(
                name => name,
                name => Path.Combine(directory, name),
                StringComparer.Ordinal);
        }
        catch
        {
            foreach (var name in promoted.AsEnumerable().Reverse())
            {
                TryDelete(Path.Combine(directory, name));
            }

            foreach (var pair in backups.Reverse())
            {
                if (File.Exists(pair.Value))
                {
                    File.Move(
                        pair.Value,
                        Path.Combine(directory, pair.Key),
                        overwrite: true);
                }
            }

            throw;
        }
        finally
        {
            foreach (var temporary in staged.Values)
            {
                TryDelete(temporary);
            }

            if (committed)
            {
                foreach (var backup in backups.Values)
                {
                    TryDelete(backup);
                }
            }
        }
    }

    private static string TemporaryPath(
        string directory,
        string artifactName,
        string suffix) =>
        Path.Combine(
            directory,
            $".{artifactName}.{Guid.NewGuid():N}.{suffix}");

    private static async Task WriteNewFileAsync(
        string path,
        string content,
        CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(
            path,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            4096,
            FileOptions.Asynchronous | FileOptions.WriteThrough);
        await using var writer = new StreamWriter(stream, Utf8WithoutBom);
        await writer.WriteAsync(content.AsMemory(), cancellationToken).ConfigureAwait(false);
        await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
