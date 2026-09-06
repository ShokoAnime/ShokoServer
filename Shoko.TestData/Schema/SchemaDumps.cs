using System.Text.Json;

namespace Shoko.TestData.Schema;

/// <summary>
/// Loads the per-backend schema dumps written by <c>Shoko.IntegrationTests</c>, from the directory
/// named by <see cref="DirectoryVariable"/>.
/// </summary>
/// <remarks>
/// Nothing is committed — each dump comes from migrating a real database of that backend from empty,
/// which CI does one job per backend, publishing the dumps for the comparison job to collect.
/// </remarks>
public static class SchemaDumps
{
    /// <summary>Environment variable naming the directory holding the dumps.</summary>
    public const string DirectoryVariable = "SHOKO_SCHEMA_DIR";

    public static readonly string[] Backends = ["SQLite", "MySQL", "SQLServer"];

    private static string? Directory => Environment.GetEnvironmentVariable(DirectoryVariable) is { Length: > 0 } directory ? directory : null;

    /// <summary>The file a dump for <paramref name="backend"/> is written to and read from.</summary>
    public static string FileNameFor(string backend) => $"schema-{backend}.json";

    /// <summary>Why the dumps cannot be compared, or <see langword="null"/> when they can.</summary>
    public static string? Unavailable()
    {
        if (Directory is not { } directory)
            return $"{DirectoryVariable} is not set.";

        var missing = Backends.Where(backend => !File.Exists(Path.Combine(directory, FileNameFor(backend)))).ToArray();

        return missing.Length is 0 ? null : $"No schema dump for {string.Join(" or ", missing)} in '{directory}'.";
    }

    /// <summary>The dumped schema of <paramref name="backend"/>, keyed by table then column.</summary>
    public static IReadOnlyDictionary<string, IReadOnlyDictionary<string, ColumnSnapshot>> For(string backend)
        => _schemas.GetOrAdd(backend, Read);

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, IReadOnlyDictionary<string, IReadOnlyDictionary<string, ColumnSnapshot>>> _schemas =
        new(StringComparer.OrdinalIgnoreCase);

    private static IReadOnlyDictionary<string, IReadOnlyDictionary<string, ColumnSnapshot>> Read(string backend)
    {
        var path = Path.Combine(Directory ?? throw new InvalidOperationException($"{DirectoryVariable} is not set."), FileNameFor(backend));
        using var stream = File.OpenRead(path);
        var tables = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, ColumnSnapshot>>>(stream)
            ?? throw new InvalidOperationException($"'{path}' deserialized to null.");

        return tables.ToDictionary(
            table => table.Key,
            table => (IReadOnlyDictionary<string, ColumnSnapshot>)table.Value.ToDictionary(
                column => column.Key, column => column.Value, StringComparer.OrdinalIgnoreCase),
            StringComparer.OrdinalIgnoreCase);
    }
}
