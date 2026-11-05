using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using Shoko.Abstractions.Metadata.Anidb.Enums;

namespace Shoko.Server.Telemetry;

/// <summary>
/// The spans and metrics of the requests sent to AniDB. Nothing here throws, since a listener's failure must never fail a
/// request or change how a ban is handled. Only a UDP command's name is kept, as its arguments hold the credentials.
/// </summary>
internal static class AniDBTelemetry
{
    /// <summary>
    /// The name of the trace source and meter.
    /// </summary>
    public const string Name = "Shoko.Server.AniDB";

    /// <summary>
    /// The transport of a UDP request.
    /// </summary>
    public const string Udp = "udp";

    /// <summary>
    /// The transport of an HTTP request.
    /// </summary>
    public const string Http = "http";

    private const string TransportTag = "shoko.anidb.transport";

    private const string CommandTag = "shoko.anidb.command";

    private const string ErrorTypeTag = "error.type";

    private const string UnknownCommand = "UNKNOWN";

    private static readonly ActivitySource _source = new(Name);

    private static readonly Meter _meter = new(Name);

    private static readonly Histogram<double> _requestDuration = _meter.CreateHistogram<double>(
        "shoko.anidb.request.duration",
        "s",
        "How long a request to AniDB took, without the time waiting for its turn."
    );

    // The rate limit makes waits of many seconds normal when the queue is busy, past where the default buckets stop.
    private static readonly Histogram<double> _rateLimitWait = _meter.CreateHistogram<double>(
        "shoko.anidb.rate_limit.wait.duration",
        "s",
        "How long a request waited for its turn under the rate limit.",
        advice: new() { HistogramBucketBoundaries = [0.01, 0.1, 0.5, 1, 2, 4, 8, 15, 30, 60, 120, 300, 600] }
    );

    private static readonly Counter<long> _bans = _meter.CreateCounter<long>(
        "shoko.anidb.bans",
        "{ban}",
        "Bans registered. An empty UDP reply is assumed to be one, so this can count lost connections too."
    );

    /// <summary>
    /// Starts a request, whose span is the current one until it is finished.
    /// </summary>
    /// <param name="transport">Its transport.</param>
    /// <param name="command">The UDP command sent, if any, of which only the name is kept.</param>
    /// <returns>The running request.</returns>
    public static AniDBRequestOperation StartRequest(string transport, string? command = null)
    {
        try
        {
            var name = command is null ? null : GetCommandName(command);
            var span = _source.StartActivity(name is null ? $"anidb.{transport}" : $"anidb.{transport} {name}", ActivityKind.Client);
            span?.SetTag(TransportTag, transport);
            if (name is not null)
                span?.SetTag(CommandTag, name);

            return new(span, Stopwatch.GetTimestamp(), transport, name);
        }
        catch (Exception)
        {
            return default;
        }
    }

    /// <summary>
    /// Ends a request's span and records its duration.
    /// </summary>
    /// <param name="request">The running request.</param>
    /// <param name="failure">What it failed with, if it did. Only its type is kept, since its message can hold anything.</param>
    public static void FinishRequest(in AniDBRequestOperation request, Exception? failure = null)
    {
        try
        {
            if (request.Transport is null)
                return;

            var errorType = failure?.GetType().FullName;
            if (request.Span is { } span)
            {
                if (errorType is not null)
                {
                    span.SetTag(ErrorTypeTag, errorType);
                    span.SetStatus(ActivityStatusCode.Error);
                }

                span.Dispose();
            }

            var tags = new TagList { { TransportTag, request.Transport } };
            if (request.Command is not null)
                tags.Add(CommandTag, request.Command);
            if (errorType is not null)
                tags.Add(ErrorTypeTag, errorType);
            _requestDuration.Record(Stopwatch.GetElapsedTime(request.StartedAt).TotalSeconds, tags);
        }
        catch (Exception)
        {
            // See the class's remarks.
        }
    }

    /// <summary>
    /// Records how long a request waited for its turn.
    /// </summary>
    /// <param name="transport">Its transport.</param>
    /// <param name="startedAt">When it started waiting, as a <see cref="Stopwatch"/> timestamp.</param>
    public static void RecordRateLimitWait(string transport, long startedAt)
    {
        try
        {
            _rateLimitWait.Record(Stopwatch.GetElapsedTime(startedAt).TotalSeconds, new KeyValuePair<string, object?>(TransportTag, transport));
        }
        catch (Exception)
        {
            // See the class's remarks.
        }
    }

    /// <summary>
    /// Counts a ban.
    /// </summary>
    /// <param name="type">What was banned.</param>
    public static void RecordBan(AnidbBanType type)
    {
        try
        {
            _bans.Add(1, new KeyValuePair<string, object?>(TransportTag, type is AnidbBanType.UDP ? Udp : Http));
        }
        catch (Exception)
        {
            // See the class's remarks.
        }
    }

    /// <summary>
    /// Gets the name of a UDP command, the letters it starts with.
    /// </summary>
    /// <param name="command">The whole command, credentials included.</param>
    /// <returns>The name, upper-cased.</returns>
    internal static string GetCommandName(string command)
    {
        var length = 0;
        while (length < command.Length && char.IsAsciiLetter(command[length]))
            length++;

        return length is 0 ? UnknownCommand : command[..length].ToUpperInvariant();
    }
}

/// <summary>
/// A request being timed by <see cref="AniDBTelemetry"/>.
/// </summary>
/// <param name="Span">Its span, if anyone is listening.</param>
/// <param name="StartedAt">When it started, as a <see cref="Stopwatch"/> timestamp.</param>
/// <param name="Transport">Its transport.</param>
/// <param name="Command">The name of the UDP command, if any.</param>
internal readonly record struct AniDBRequestOperation(Activity? Span, long StartedAt, string? Transport, string? Command);
