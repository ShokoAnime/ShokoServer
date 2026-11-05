using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Linq;

namespace Shoko.QueueProcessor.Analytics;

/// <summary>
/// The queue's spans and metrics, on the runtime's own <see cref="ActivitySource"/> and <see cref="Meter"/>, so they cost
/// next to nothing until the host listens to them. Nothing here throws: the callers are the paths that report a job's
/// outcome, and a listener's failure must never change what happens to a job.
/// </summary>
public static class QueueTelemetry
{
    /// <summary>
    /// The name of the queue's trace source and meter.
    /// </summary>
    public const string Name = "Shoko.QueueProcessor";

    /// <summary>
    /// What the name of every job's span starts with, followed by its job type, for a host that samples jobs apart from
    /// the rest of its traces.
    /// </summary>
    public const string JobSpanPrefix = "shoko.job ";

    internal const string OutcomeSuccess = "success";

    internal const string OutcomeRequeued = "requeued";

    internal const string OutcomeFailed = "failed";

    internal const string OutcomeCancelled = "cancelled";

    private const string JobTypeTag = "shoko.queue.job.type";

    private const string PoolTag = "shoko.queue.pool";

    private const string OutcomeTag = "shoko.queue.job.outcome";

    private const string PriorityTag = "shoko.queue.job.priority";

    private const string ScheduledTag = "shoko.queue.job.scheduled";

    // Not JobSpanPrefix, so these follow what queued them rather than being sampled as jobs.
    private const string QueueSpanPrefix = "shoko.job.queue ";

    private const string ErrorTypeTag = "error.type";

    private static readonly ActivitySource _source = new(Name);

    private static readonly Meter _meter = new(Name);

    // Jobs run from milliseconds to hours, far past the 10 seconds the default buckets stop at.
    private static readonly Histogram<double> _jobDuration = _meter.CreateHistogram<double>(
        "shoko.queue.job.duration",
        "s",
        "How long a job attempt took, from being picked up by a worker to its outcome.",
        advice: new() { HistogramBucketBoundaries = [0.01, 0.05, 0.1, 0.5, 1, 5, 10, 30, 60, 300, 900, 1800, 3600, 14400] }
    );

    /// <summary>
    /// Captures the trace context of the current span for a job being queued.
    /// </summary>
    /// <returns>The trace context, or <c>null</c> without a span being kept, as a link to it would lead nowhere.</returns>
    internal static string? CaptureTraceParent()
        => Activity.Current is { IdFormat: ActivityIdFormat.W3C, Recorded: true } span ? span.Id : null;

    /// <summary>
    /// Reports a job being queued, as a span under whatever queued it. Without a current span there is nothing to add
    /// it to, and the background work queuing most jobs should not start a trace of its own for each.
    /// </summary>
    /// <param name="jobType">The job's type. Never its key, title or arguments, which can hold anything.</param>
    /// <param name="priority">The job's priority.</param>
    /// <param name="isScheduled">Whether it waits for a later time.</param>
    internal static void ReportQueued(string jobType, int priority, bool isScheduled)
    {
        try
        {
            if (Activity.Current is null)
                return;

            using var span = _source.StartActivity(QueueSpanPrefix + jobType, ActivityKind.Producer);
            span?.SetTag(JobTypeTag, jobType);
            span?.SetTag(PriorityTag, priority);
            span?.SetTag(ScheduledTag, isScheduled);
        }
        catch (Exception)
        {
            // See the class's remarks.
        }
    }

    /// <summary>
    /// Starts the span of a job, which is the current one until it is finished.
    /// </summary>
    /// <param name="jobType">The job's type. Never its arguments, which can hold anything.</param>
    /// <param name="pool">The pool it runs in, if any.</param>
    /// <param name="traceParent">
    /// The trace context of what queued it, linked to rather than made its parent: a job can run long after whatever
    /// queued it has ended, and each attempt is sampled on its own.
    /// </param>
    /// <returns>The span, or <c>null</c> when no one is listening.</returns>
    internal static Activity? StartJob(string jobType, string? pool, string? traceParent = null)
    {
        try
        {
            ActivityLink[]? links = ActivityContext.TryParse(traceParent, null, isRemote: true, out var queuedBy) ? [new(queuedBy)] : null;
            var span = _source.StartActivity(JobSpanPrefix + jobType, ActivityKind.Consumer, default(ActivityContext), links: links);
            span?.SetTag(JobTypeTag, jobType);
            span?.SetTag(PoolTag, pool);
            return span;
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>
    /// Ends the span of a job.
    /// </summary>
    /// <param name="span">The span, if there is one.</param>
    /// <param name="outcome">How the attempt ended.</param>
    /// <param name="failure">What it failed with, if it did. Only its type is kept, since its message can hold anything.</param>
    internal static void FinishSpan(Activity? span, string outcome, Exception? failure = null)
    {
        try
        {
            if (span is null)
                return;

            span.SetTag(OutcomeTag, outcome);
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
    /// Ends the span of a job attempt and records its duration.
    /// </summary>
    /// <param name="span">The span, if there is one.</param>
    /// <param name="jobType">The job's type.</param>
    /// <param name="pool">The pool it ran in.</param>
    /// <param name="outcome">How the attempt ended.</param>
    /// <param name="elapsed">How long it took.</param>
    /// <param name="failure">What it failed with, if it did.</param>
    internal static void FinishJob(Activity? span, string jobType, string pool, string outcome, TimeSpan elapsed, Exception? failure)
    {
        FinishSpan(span, outcome, failure);
        try
        {
            _jobDuration.Record(elapsed.TotalSeconds, new(JobTypeTag, jobType), new(PoolTag, pool), new(OutcomeTag, outcome));
        }
        catch (Exception)
        {
            // See the class's remarks.
        }
    }

    /// <summary>
    /// Creates the gauges of the pools. They live and die with what they read, so they are on a meter of their own.
    /// </summary>
    /// <param name="getStatus">Reads the status of every pool.</param>
    /// <returns>The meter, to dispose of when the pools stop.</returns>
    internal static Meter CreatePoolGauges(Func<IReadOnlyDictionary<string, PoolStatus>> getStatus)
    {
        var meter = new Meter(Name);
        Gauge("shoko.queue.jobs.waiting", "{job}", "Jobs ready to run.", status => status.WaitingCount);
        Gauge("shoko.queue.jobs.blocked", "{job}", "Jobs an acquisition filter keeps from running.", status => status.BlockedCount);
        Gauge("shoko.queue.jobs.scheduled", "{job}", "Jobs waiting for a later time.", status => status.ScheduledCount);
        Gauge("shoko.queue.jobs.executing", "{job}", "Jobs running.", status => status.ActiveWorkers);
        Gauge("shoko.queue.workers.limit", "{worker}", "The most workers the pool can use.", status => status.MaxWorkers);
        return meter;

        void Gauge(string name, string unit, string description, Func<PoolStatus, int> read)
            => meter.CreateObservableGauge(name, () => getStatus().Values.Select(status => new Measurement<int>(read(status), new KeyValuePair<string, object?>(PoolTag, status.Name))), unit, description);
    }
}
