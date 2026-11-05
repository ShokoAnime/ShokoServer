using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Data.Common;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using MySqlConnector;

namespace Shoko.Server.Telemetry.Database;

/// <summary>
/// The spans and durations of the statements NHibernate runs. Nothing here throws, since a listener's failure must never
/// fail a statement.
/// </summary>
internal static class DatabaseTelemetry
{
    /// <summary>
    /// The name of the trace source and meter.
    /// </summary>
    public const string Name = "Shoko.Server.Database";

    private const string SystemTag = "db.system.name";

    private const string OperationTag = "db.operation.name";

    private const string StatementTag = "db.query.text";

    private const string ErrorTypeTag = "error.type";

    private const string OtherOperation = "OTHER";

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

    private static bool _capturesStatements;

    /// <summary>
    /// Whether session factories built from now on should use <see cref="TracingBatcherFactory"/>. Off unless the
    /// telemetry is running and asked for it, so an idle server keeps NHibernate's own batcher.
    /// </summary>
    public static bool IsEnabled { get; private set; }

    /// <summary>
    /// Turns the database telemetry on.
    /// </summary>
    /// <param name="captureStatements">Whether spans keep the text of the statement. NHibernate's holds parameters, not values.</param>
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
            var operation = GetOperation(command?.CommandText);
            var span = _source.StartActivity(operation, ActivityKind.Client);
            if (span is { IsAllDataRequested: true })
            {
                span.SetTag(SystemTag, system);
                span.SetTag(OperationTag, operation);
                if (_capturesStatements)
                    span.SetTag(StatementTag, command?.CommandText);
            }

            return new(span, Stopwatch.GetTimestamp(), system, operation);
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
                if (errorType is not null)
                {
                    span.SetTag(ErrorTypeTag, errorType);
                    span.SetStatus(ActivityStatusCode.Error);
                }

                span.Dispose();
            }

            var elapsed = Stopwatch.GetElapsedTime(operation.StartedAt).TotalSeconds;
            if (errorType is null)
                _duration.Record(elapsed, new(SystemTag, operation.System), new(OperationTag, operation.Name));
            else
                _duration.Record(elapsed, new(SystemTag, operation.System), new(OperationTag, operation.Name), new KeyValuePair<string, object?>(ErrorTypeTag, errorType));
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

    private static string? GetSystem(DbCommand? command)
        => command switch
        {
            SqliteCommand => "sqlite",
            MySqlCommand => "mysql",
            SqlCommand => "microsoft.sql_server",
            _ => null,
        };
}

/// <summary>
/// A statement being timed by <see cref="DatabaseTelemetry"/>.
/// </summary>
/// <param name="Span">Its span, if anyone is listening.</param>
/// <param name="StartedAt">When it started, as a <see cref="Stopwatch"/> timestamp.</param>
/// <param name="System">The database system.</param>
/// <param name="Name">The operation's name.</param>
internal readonly record struct DatabaseOperation(Activity? Span, long StartedAt, string? System, string? Name);
