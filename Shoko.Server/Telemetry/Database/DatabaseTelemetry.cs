using System;
using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Data.Common;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using MySqlConnector;

namespace Shoko.Server.Telemetry.Database;

/// <summary>
/// The spans and durations of the statements NHibernate runs. Nothing here throws, since a listener's failure must never
/// fail a statement.
/// </summary>
internal static partial class DatabaseTelemetry
{
    /// <summary>
    /// The name of the trace source and meter.
    /// </summary>
    public const string Name = "Shoko.Server.Database";

    private const string SystemTag = "db.system.name";

    private const string OperationTag = "db.operation.name";

    private const string CollectionTag = "db.collection.name";

    private const string NamespaceTag = "db.namespace";

    private const string ServerAddressTag = "server.address";

    private const string StatementTag = "db.query.text";

    private const string RepositoryEntityTag = "shoko.repository.entity";

    private const string RepositoryOperationTag = "shoko.repository.operation";

    private const string RepositoryCountTag = "shoko.repository.count";

    private const string RepositoryFoundTag = "shoko.repository.found";

    private const string RepositoryReadsTag = "shoko.repository.reads";

    private const string RepositoryReadsByEntityTag = "shoko.repository.reads.by_entity";

    private const string ErrorTypeTag = "error.type";

    private const string OtherOperation = "OTHER";

    private const int MaxStatementLength = 8192;

    // A closed list, since the first word of a hand-written statement could be anything.
    private static readonly FrozenSet<string> _operations = FrozenSet.ToFrozenSet(
        ["SELECT", "INSERT", "UPDATE", "DELETE", "REPLACE", "MERGE", "WITH", "CREATE", "ALTER", "DROP", "TRUNCATE", "PRAGMA", "EXEC", "SET", "SHOW"],
        StringComparer.OrdinalIgnoreCase
    );

    private static readonly ActivitySource _source = new(Name);

    private static readonly Meter _meter = new(Name);

    private static readonly Histogram<double> _duration = _meter.CreateHistogram<double>(
        "db.client.operation.duration",
        "s",
        "How long a statement took.",
        advice: new() { HistogramBucketBoundaries = [0.001, 0.005, 0.01, 0.05, 0.1, 0.5, 1, 5, 10] }
    );

    private static readonly Counter<long> _repositoryReads = _meter.CreateCounter<long>(
        "shoko.repository.reads",
        "{read}",
        "How many reads the cached repositories answered from memory, and whether they found anything."
    );

    private static readonly Lock _repositoryReadsLock = new();

    private static bool _capturesStatements;

    /// <summary>
    /// Whether session factories built from now on should use <see cref="TracingBatcherFactory"/>. Off unless the
    /// telemetry is running and asked for it, so an idle server keeps NHibernate's own batcher.
    /// </summary>
    public static bool IsEnabled { get; private set; }

    /// <summary>
    /// Turns the database telemetry on.
    /// </summary>
    /// <param name="captureStatements">Whether spans keep the text of the statement, with placeholders for its values.</param>
    public static void Enable(bool captureStatements)
    {
        _capturesStatements = captureStatements;
        IsEnabled = true;
    }

    /// <summary>
    /// Turns the database telemetry off for session factories built from now on.
    /// </summary>
    public static void Disable()
    {
        _capturesStatements = false;
        IsEnabled = false;
    }

    /// <summary>
    /// Starts timing a statement, whose span is the current one until it is finished.
    /// </summary>
    /// <param name="command">The statement's command.</param>
    /// <returns>The running statement.</returns>
    public static DatabaseOperation Start(DbCommand? command)
    {
        try
        {
            var system = GetSystem(command);
            var statement = SanitizeStatement(command?.CommandText);
            var operation = GetOperation(statement);
            var collection = GetCollection(statement);
            var span = _source.StartActivity(collection is null ? operation : $"{operation} {collection}", ActivityKind.Client);
            if (span is { IsAllDataRequested: true })
            {
                span.SetTag(SystemTag, system);
                span.SetTag(OperationTag, operation);
                span.SetTag(CollectionTag, collection);
                if (_capturesStatements)
                    span.SetTag(StatementTag, statement);
            }

            return new(span, Stopwatch.GetTimestamp(), system, operation, collection, command);
        }
        catch (Exception)
        {
            return default;
        }
    }

    /// <summary>
    /// Ends a statement's span and records its duration.
    /// </summary>
    /// <param name="operation">The running statement.</param>
    /// <param name="failure">What it failed with, if it did. Only its type is kept, since its message can hold the statement's values.</param>
    public static void Finish(in DatabaseOperation operation, Exception? failure = null)
    {
        try
        {
            if (operation.Name is null)
                return;

            var errorType = failure?.GetType().FullName;
            if (operation.Span is { } span)
            {
                // NHibernate only gives a command its connection as it runs it.
                if (span.IsAllDataRequested)
                {
                    span.SetTag(NamespaceTag, operation.Command?.Connection?.Database);
                    span.SetTag(ServerAddressTag, GetServerAddress(operation.Command));
                }

                if (errorType is not null)
                {
                    span.SetTag(ErrorTypeTag, errorType);
                    span.SetStatus(ActivityStatusCode.Error);
                }

                span.Dispose();
            }

            var tags = new TagList { { SystemTag, operation.System }, { OperationTag, operation.Name } };
            if (operation.Collection is not null)
                tags.Add(CollectionTag, operation.Collection);
            if (errorType is not null)
                tags.Add(ErrorTypeTag, errorType);
            _duration.Record(Stopwatch.GetElapsedTime(operation.StartedAt).TotalSeconds, tags);
        }
        catch (Exception)
        {
            // See the class's remarks.
        }
    }

    /// <summary>
    /// Gets the name of the operation from a statement's first word.
    /// </summary>
    /// <param name="statement">The statement.</param>
    /// <returns>The upper-cased operation, or <see cref="OtherOperation"/> for anything not known.</returns>
    internal static string GetOperation(string? statement)
    {
        if (statement is null)
            return OtherOperation;

        var text = statement.AsSpan().TrimStart();
        var end = text.IndexOfAny(" \t\r\n(");
        var word = end < 0 ? text : text[..end];
        return _operations.GetAlternateLookup<ReadOnlySpan<char>>().TryGetValue(word, out var operation) ? operation : OtherOperation;
    }

    /// <summary>
    /// Gets the table a statement is about, the first one it names.
    /// </summary>
    /// <param name="statement">The statement.</param>
    /// <returns>The table, or <c>null</c> when none can be told.</returns>
    internal static string? GetCollection(string? statement)
    {
        if (statement is null)
            return null;

        var match = CollectionRegex().Match(statement);
        return match.Success ? match.Groups["name"].Value : null;
    }

    /// <summary>
    /// Replaces the values written into a statement with placeholders, leaving the shape of the query to validate.
    /// NHibernate already sends its own as parameters, so this is for hand-written SQL.
    /// </summary>
    /// <remarks>
    /// Double-quoted text is left alone, as it names a table or column everywhere but in a MySQL string.
    /// </remarks>
    /// <param name="statement">The statement.</param>
    /// <returns>The statement without its string and number literals, cut to <see cref="MaxStatementLength"/>.</returns>
    internal static string? SanitizeStatement(string? statement)
    {
        if (statement is null)
            return null;

        var sanitized = LiteralRegex().Replace(statement, "?");
        return sanitized.Length > MaxStatementLength ? sanitized[..MaxStatementLength] : sanitized;
    }

    /// <summary>
    /// Starts the span of what a repository does as a whole, around the statements it runs. Off with the rest of the
    /// database telemetry, as a refresh can save thousands of rows.
    /// </summary>
    /// <param name="operation">What it is doing.</param>
    /// <param name="entity">The name of the entity it holds.</param>
    /// <param name="count">How many entities it was handed, if any.</param>
    /// <returns>The span, or <c>null</c> when it is off or no one is listening.</returns>
    public static Activity? StartRepositorySpan(string operation, string entity, int? count = null)
    {
        try
        {
            if (!IsEnabled || !_source.HasListeners())
                return null;

            var span = _source.StartActivity($"repository.{operation} {entity}");
            span?.SetTag(RepositoryOperationTag, operation);
            span?.SetTag(RepositoryEntityTag, entity);
            if (count is not null)
                span?.SetTag(RepositoryCountTag, count);
            return span;
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>
    /// Ends the span of what a repository did.
    /// </summary>
    /// <param name="span">The span, if there is one.</param>
    /// <param name="failure">What it failed with, if it did. Only its type is kept.</param>
    /// <param name="count">How many entities it ended up with, when only known now.</param>
    public static void FinishRepositorySpan(Activity? span, Exception? failure = null, int? count = null)
    {
        try
        {
            if (span is not null && count is not null)
                span.SetTag(RepositoryCountTag, count);
        }
        catch (Exception)
        {
            // See the class's remarks.
        }

        ShokoTelemetry.FinishSpan(span, failure);
    }

    /// <summary>
    /// Counts a read of a cached repository, and adds it to the tally of the request or job it is part of. A span for
    /// each would be far too many, as a single request can read thousands of times.
    /// </summary>
    /// <param name="entity">The name of the entity read.</param>
    /// <param name="found">Whether the read found anything.</param>
    public static void RecordRepositoryRead(string entity, bool found)
    {
        if (!IsEnabled)
            return;

        try
        {
            _repositoryReads.Add(1, new(RepositoryEntityTag, entity), new(RepositoryFoundTag, found));
            if (Activity.Current is not { IsAllDataRequested: true } span)
                return;

            while (span.Parent is { } parent)
                span = parent;
            GetRepositoryReads(span).AddOrUpdate(entity, 1, static (_, count) => count + 1);
        }
        catch (Exception)
        {
            // See the class's remarks.
        }
    }

    /// <summary>
    /// Writes the tally of a span's repository reads onto it, as it ends.
    /// </summary>
    /// <param name="span">The span.</param>
    public static void WriteRepositoryReads(Activity span)
    {
        try
        {
            if (span.GetCustomProperty(RepositoryReadsTag) is not ConcurrentDictionary<string, long> reads)
                return;

            var counts = reads.OrderByDescending(pair => pair.Value).ThenBy(pair => pair.Key, StringComparer.Ordinal).ToList();
            span.SetTag(RepositoryReadsTag, counts.Sum(pair => pair.Value));
            span.SetTag(RepositoryReadsByEntityTag, string.Join(',', counts.Select(pair => $"{pair.Key}={pair.Value}")));
        }
        catch (Exception)
        {
            // See the class's remarks.
        }
    }

    private static ConcurrentDictionary<string, long> GetRepositoryReads(Activity span)
    {
        if (span.GetCustomProperty(RepositoryReadsTag) is ConcurrentDictionary<string, long> reads)
            return reads;

        lock (_repositoryReadsLock)
        {
            if (span.GetCustomProperty(RepositoryReadsTag) is ConcurrentDictionary<string, long> existing)
                return existing;

            var created = new ConcurrentDictionary<string, long>();
            span.SetCustomProperty(RepositoryReadsTag, created);
            return created;
        }
    }

    /// <summary>
    /// Gets the host a statement goes to. Never SQLite's data source, which is a path on disk.
    /// </summary>
    private static string? GetServerAddress(DbCommand? command)
        => command is SqliteCommand ? null : command?.Connection?.DataSource?.Split(',')[0];

    private static string? GetSystem(DbCommand? command)
        => command switch
        {
            SqliteCommand => "sqlite",
            MySqlCommand => "mysql",
            SqlCommand => "microsoft.sql_server",
            _ => null,
        };

    [GeneratedRegex(@"\b(?:FROM|INTO|UPDATE|TABLE|JOIN)\s+(?:(?:IF\s+(?:NOT\s+)?EXISTS|ONLY)\s+)?(?:[\[`""]?\w+[\]`""]?\.)?[\[`""]?(?<name>[A-Za-z_]\w{0,127})[\]`""]?(?=[\s();,]|$)", RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture | RegexOptions.CultureInvariant)]
    private static partial Regex CollectionRegex();

    [GeneratedRegex(@"'(?:[^']|'')*(?:'|$)|(?<![\w@?$:.])(?:0x[0-9A-Fa-f]+|\d+(?:\.\d+)?)\b", RegexOptions.ExplicitCapture | RegexOptions.CultureInvariant)]
    private static partial Regex LiteralRegex();
}

/// <summary>
/// A statement being timed by <see cref="DatabaseTelemetry"/>.
/// </summary>
/// <param name="Span">Its span, if anyone is listening.</param>
/// <param name="StartedAt">When it started, as a <see cref="Stopwatch"/> timestamp.</param>
/// <param name="System">The database system.</param>
/// <param name="Name">The operation's name.</param>
/// <param name="Collection">The table it is about, if that can be told.</param>
/// <param name="Command">Its command.</param>
internal readonly record struct DatabaseOperation(Activity? Span, long StartedAt, string? System, string? Name, string? Collection, DbCommand? Command);
