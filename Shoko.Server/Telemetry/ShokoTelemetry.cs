using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.IO;
using System.Linq;
using Shoko.Abstractions.Connectivity.Suspensions;

namespace Shoko.Server.Telemetry;

/// <summary>
/// The server's own trace source and meter, which go nowhere and cost next to nothing until <see cref="TelemetryRuntime"/>
/// listens to them. Nothing here throws, since a listener's failure must never fail what is being measured, and nothing
/// here takes a path, a URL or an exception's message.
/// </summary>
internal static class ShokoTelemetry
{
    /// <summary>
    /// The name of the server's trace source and meter.
    /// </summary>
    public const string ServerSourceName = "Shoko.Server";

    private const string ErrorTypeTag = "error.type";

    private const string HashProviderTag = "shoko.video.hash.provider";

    private const string ReleaseProviderTag = "shoko.release.provider";

    private const string ReleaseFoundTag = "shoko.release.found";

    private const string ImageSourceTag = "shoko.image.source";

    private const string SuspensionProviderTag = "shoko.suspension.provider";

    private const string SuspensionKindTag = "shoko.suspension.kind";

    /// <summary>
    /// The server's trace source.
    /// </summary>
    public static readonly ActivitySource Source = new(ServerSourceName);

    private static readonly Meter _meter = new(ServerSourceName);

    // Hashing a large file over a network share takes minutes, far past the 10 seconds the default buckets stop at.
    private static readonly Histogram<double> _hashDuration = _meter.CreateHistogram<double>(
        "shoko.video.hash.duration",
        "s",
        "How long a hash provider took with a file.",
        advice: new() { HistogramBucketBoundaries = [0.1, 0.5, 1, 5, 10, 30, 60, 120, 300, 600, 1200, 3600] }
    );

    private static readonly Histogram<long> _hashSize = _meter.CreateHistogram<long>(
        "shoko.video.hash.size",
        "By",
        "The size of the files hashed.",
        advice: new() { HistogramBucketBoundaries = [50_000_000, 100_000_000, 250_000_000, 500_000_000, 1_000_000_000, 2_000_000_000, 4_000_000_000, 8_000_000_000, 16_000_000_000, 32_000_000_000, 64_000_000_000] }
    );

    private static readonly Histogram<double> _releaseSearchDuration = _meter.CreateHistogram<double>(
        "shoko.release.search.duration",
        "s",
        "How long a release provider took to answer for a file.",
        advice: new() { HistogramBucketBoundaries = [0.01, 0.1, 0.5, 1, 5, 10, 30, 60, 120, 300, 600] }
    );

    private static readonly Histogram<double> _imageDownloadDuration = _meter.CreateHistogram<double>(
        "shoko.image.download.duration",
        "s",
        "How long getting an image took."
    );

    /// <summary>
    /// Starts a span of the server's, which is the current one until it is finished.
    /// </summary>
    /// <param name="name">The span's name.</param>
    /// <returns>The span, or <c>null</c> when no one is listening.</returns>
    public static Activity? StartSpan(string name)
    {
        try
        {
            return Source.StartActivity(name);
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>
    /// Ends a span of the server's.
    /// </summary>
    /// <param name="span">The span, if there is one.</param>
    /// <param name="failure">What it failed with, if it did. Only its type is kept.</param>
    public static void FinishSpan(Activity? span, Exception? failure = null)
    {
        try
        {
            if (span is null)
                return;

            if (failure is not null)
            {
                span.SetTag(ErrorTypeTag, failure.GetType().FullName);
                span.SetStatus(ActivityStatusCode.Error);
            }

            span.Dispose();
        }
        catch (Exception)
        {
            // See the class's remarks.
        }
    }

    /// <summary>
    /// Records how long a hash provider took with a file.
    /// </summary>
    /// <param name="provider">The provider's name.</param>
    /// <param name="startedAt">When it started, as a <see cref="Stopwatch"/> timestamp.</param>
    /// <param name="failure">What it failed with, if it did.</param>
    public static void RecordHashDuration(string provider, long startedAt, Exception? failure = null)
        => Record(_hashDuration, startedAt, failure, new KeyValuePair<string, object?>(HashProviderTag, provider));

    /// <summary>
    /// Records the size of a file that was hashed, on the span too.
    /// </summary>
    /// <param name="span">The span hashing it, if there is one.</param>
    /// <param name="file">The file. Only its size is kept.</param>
    public static void RecordHashedFile(Activity? span, FileInfo file)
    {
        try
        {
            span?.SetTag("file.size", file.Length);
            _hashSize.Record(file.Length);
        }
        catch (Exception)
        {
            // See the class's remarks. Reading the size can fail too, if the file went away.
        }
    }

    /// <summary>
    /// Records how long a release provider took to answer for a file.
    /// </summary>
    /// <param name="provider">The provider's name.</param>
    /// <param name="startedAt">When it started, as a <see cref="Stopwatch"/> timestamp.</param>
    /// <param name="found">Whether it found a release.</param>
    /// <param name="failure">What it failed with, if it did.</param>
    public static void RecordReleaseSearch(string provider, long startedAt, bool found, Exception? failure = null)
        => Record(_releaseSearchDuration, startedAt, failure, new(ReleaseProviderTag, provider), new(ReleaseFoundTag, found));

    /// <summary>
    /// Ends the span of a release search.
    /// </summary>
    /// <param name="span">The span, if there is one.</param>
    /// <param name="provider">The name of the provider that found a release, if one did.</param>
    /// <param name="found">Whether a release was found.</param>
    /// <param name="failure">What it failed with, if it did.</param>
    public static void FinishReleaseSearch(Activity? span, string? provider, bool found, Exception? failure)
    {
        try
        {
            span?.SetTag(ReleaseFoundTag, found);
            if (provider is not null)
                span?.SetTag(ReleaseProviderTag, provider);
        }
        catch (Exception)
        {
            // See the class's remarks.
        }

        FinishSpan(span, failure);
    }

    /// <summary>
    /// Starts the span of an image being fetched.
    /// </summary>
    /// <param name="source">The image's source.</param>
    /// <returns>The span, or <c>null</c> when no one is listening.</returns>
    public static Activity? StartImageDownload(string source)
    {
        var span = StartSpan("image.download");
        try
        {
            span?.SetTag(ImageSourceTag, source);
        }
        catch (Exception)
        {
            // See the class's remarks.
        }

        return span;
    }

    /// <summary>
    /// Records how long fetching an image took.
    /// </summary>
    /// <param name="source">The image's source.</param>
    /// <param name="startedAt">When it started, as a <see cref="Stopwatch"/> timestamp.</param>
    /// <param name="failure">What it failed with, if it did.</param>
    public static void RecordImageDownload(string source, long startedAt, Exception? failure = null)
        => Record(_imageDownloadDuration, startedAt, failure, new KeyValuePair<string, object?>(ImageSourceTag, source));

    /// <summary>
    /// Creates the gauge of the suspensions. It lives and dies with what it reads, so it is on a meter of its own.
    /// </summary>
    /// <param name="getStatuses">Reads the status of every suspension provider.</param>
    /// <returns>The meter, to dispose of when the service stops.</returns>
    public static Meter CreateSuspensionGauge(Func<IReadOnlyList<SuspensionStatus>> getStatuses)
    {
        var meter = new Meter(ServerSourceName);
        meter.CreateObservableGauge(
            "shoko.suspension.active",
            () => getStatuses().SelectMany(status => Enum.GetValues<SuspensionKind>().Select(kind => new Measurement<int>(
                status.Suspensions.Any(suspension => suspension.Kind == kind) ? 1 : 0,
                new KeyValuePair<string, object?>(SuspensionProviderTag, status.Provider.Name),
                new KeyValuePair<string, object?>(SuspensionKindTag, kind.ToString())
            ))),
            "{suspension}",
            "Whether a provider is suspended, by the kind of suspension."
        );
        return meter;
    }

    private static void Record(Histogram<double> histogram, long startedAt, Exception? failure, params ReadOnlySpan<KeyValuePair<string, object?>> tags)
    {
        try
        {
            var list = new TagList(tags);
            if (failure is not null)
                list.Add(ErrorTypeTag, failure.GetType().FullName);
            histogram.Record(Stopwatch.GetElapsedTime(startedAt).TotalSeconds, list);
        }
        catch (Exception)
        {
            // See the class's remarks.
        }
    }
}
