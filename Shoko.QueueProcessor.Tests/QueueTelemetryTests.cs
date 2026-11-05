using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Shoko.QueueProcessor.Abstractions;
using Shoko.QueueProcessor.Acquisition.Attributes;
using Shoko.QueueProcessor.Analytics;
using Shoko.QueueProcessor.Builder;
using Shoko.QueueProcessor.Chain;
using Shoko.QueueProcessor.Events;
using Shoko.QueueProcessor.Orchestration;
using Shoko.QueueProcessor.Scheduling;
using Shoko.QueueProcessor.Storage;
using Shoko.QueueProcessor.Workers;
using Xunit;

namespace Shoko.QueueProcessor.Tests;

/// <summary>
/// Every job attempt gets a span and a duration, whichever way it ends. The source and meter are static, so each outcome
/// has a job type of its own to tell its telemetry apart from the rest of the tests'.
/// </summary>
public class QueueTelemetryTests
{
    #region Fixture

    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(10);

    private static readonly ConcurrentDictionary<string, TaskCompletionSource> _ran = new();

    private static TaskCompletionSource Ran(string name)
        => _ran.GetOrAdd(name, _ => new(TaskCreationOptions.RunContinuationsAsynchronously));

    private static readonly ConcurrentDictionary<string, byte> _requeued = new();

    public sealed class SucceedJob : IQueueJob
    {
        public string Name { get; set; } = string.Empty;

        public string TypeName => "Succeed";

        public string Title => Name;

        public Task Process()
        {
            Ran(Name).TrySetResult();
            return Task.CompletedTask;
        }
    }

    public sealed class FailJob : IQueueJob
    {
        public string Name { get; set; } = string.Empty;

        public string TypeName => "Fail";

        public string Title => Name;

        public Task Process()
        {
            Ran(Name).TrySetResult();
            throw new InvalidOperationException("secret=hunter2");
        }
    }

    public sealed class RequeueOnceJob : IQueueJob
    {
        public string Name { get; set; } = string.Empty;

        public string TypeName => "RequeueOnce";

        public string Title => Name;

        public Task Process()
            => _requeued.TryAdd(Name, 0) ? throw new RequeueJobException() : Task.CompletedTask;
    }

    public sealed class WaitForCancelJob(IJobCancellationAccessor cancellation) : IQueueJob
    {
        public string Name { get; set; } = string.Empty;

        public string TypeName => "WaitForCancel";

        public string Title => Name;

        public async Task Process()
        {
            Ran(Name).TrySetResult();
            await Task.Delay(Timeout.Infinite, cancellation.Token);
        }
    }

    public sealed class FollowedByJob(IQueueScheduler scheduler) : IQueueJob
    {
        public string Name { get; set; } = string.Empty;

        public string? Then { get; set; }

        public string TypeName => "FollowedBy";

        public string Title => Name;

        public async Task Process()
        {
            if (Then is { } next)
                await scheduler.RunAfterCurrent<FollowedByJob>(job => job.Name = next);
        }
    }

    private sealed class Harness : IDisposable
    {
        private readonly ServiceProvider _provider;

        private readonly WorkerPool _pool;

        public QueueScheduler Scheduler { get; }

        public JobFactory JobFactory { get; }

        public Harness()
        {
            var repo = new Mock<IJobRepository>();
            repo.Setup(r => r.InsertBatchAsync(It.IsAny<IReadOnlyCollection<QueuedJob>>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            repo.Setup(r => r.DeleteBatchAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            repo.Setup(r => r.DeleteAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            repo.Setup(r => r.UpdateRetryAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

            repo.Setup(r => r.ActivateChainChildrenAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            repo.Setup(r => r.ReparentChainChildrenAsync(It.IsAny<IReadOnlyCollection<(Guid, Guid)>>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

            var chainRepo = new Mock<IJobChainContextRepository>();
            chainRepo.Setup(r => r.GetOrCreateAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((Guid id, CancellationToken _) => new JobChainContext(id));
            chainRepo.Setup(r => r.SaveAsync(It.IsAny<JobChainContext>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            chainRepo.Setup(r => r.DeleteAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

            var services = new ServiceCollection();
            services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
            services.AddSingleton(repo.Object);
            services.AddSingleton(chainRepo.Object);
            services.AddSingleton<IQueueScheduler>(_ => Scheduler!);
            services.AddScoped<JobCancellationAccessor>();
            services.AddScoped<IJobCancellationAccessor>(sp => sp.GetRequiredService<JobCancellationAccessor>());
            services.AddScoped<JobProgressAccessor>();
            services.AddScoped<IJobProgressAccessor>(sp => sp.GetRequiredService<JobProgressAccessor>());
            services.AddScoped<JobChainContextAccessor>();
            services.AddScoped<IJobChainContextAccessor>(sp => sp.GetRequiredService<JobChainContextAccessor>());
            services.AddTransient<SucceedJob>();
            services.AddTransient<FailJob>();
            services.AddTransient<RequeueOnceJob>();
            services.AddTransient<WaitForCancelJob>();
            services.AddTransient<FollowedByJob>();
            _provider = services.BuildServiceProvider();

            var scopeFactory = _provider.GetRequiredService<IServiceScopeFactory>();
            var buffer = new PersistenceBuffer(scopeFactory, NullLogger<PersistenceBuffer>.Instance, flushIntervalMs: 10);
            var concurrency = new ConcurrencyRegistry(new Dictionary<Type, int>(), new Dictionary<Type, string?>(), new Dictionary<string, int>());
            var retry = new RetryPolicyResolver(new RetryPolicy { MaxRetries = 3, BaseDelay = TimeSpan.FromHours(1) });
            var chainScopes = new ChainScopeRegistry(scopeFactory, NullLogger<ChainScopeRegistry>.Instance);
            var metrics = new QueueMetrics();
            var events = new QueueStateEventHandler();
            var orchestrator = new QueueOrchestrator(NullLogger<QueueOrchestrator>.Instance, buffer, scopeFactory, concurrency, retry, metrics, events, chainScopes, maxTotalWorkers: 2);

            Scheduler = new QueueScheduler(orchestrator, chainScopes, scopeFactory, null);
            JobFactory = new JobFactory(_provider, orchestrator);
            _pool = new WorkerPool("TelemetryPool", maxWorkers: 2, AcquisitionAttribute.LowestPriority, [typeof(SucceedJob), typeof(FailJob), typeof(RequeueOnceJob), typeof(WaitForCancelJob), typeof(FollowedByJob)], []);
            orchestrator.Initialize([], [_pool]);

            // The workers start inside a span, which none of them may keep.
            using var starter = new ActivitySource(QueueTelemetry.Name).StartActivity("pool-starter");
            _pool.Start(_provider, orchestrator, metrics, events, chainScopes);
        }

        public void Dispose()
        {
            _pool.Stop();
            _provider.Dispose();
        }
    }

    /// <summary>
    /// Collects the spans and durations of one job type.
    /// </summary>
    private sealed class Recorder : IDisposable
    {
        private readonly ActivityListener _spanListener;

        private readonly MeterListener _meterListener = new();

        private readonly string _jobType;

        public ConcurrentQueue<Activity> Spans { get; } = new();

        public ConcurrentQueue<Dictionary<string, object?>> Durations { get; } = new();

        public Recorder(Type jobType)
        {
            _jobType = JobTypeNames.Key(jobType);
            _spanListener = new()
            {
                ShouldListenTo = source => source.Name == QueueTelemetry.Name,
                Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
                ActivityStopped = span =>
                {
                    if (span.OperationName == QueueTelemetry.JobSpanPrefix + _jobType)
                        Spans.Enqueue(span);
                },
            };
            ActivitySource.AddActivityListener(_spanListener);

            _meterListener.InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Meter.Name == QueueTelemetry.Name && instrument.Name == "shoko.queue.job.duration")
                    listener.EnableMeasurementEvents(instrument);
            };
            _meterListener.SetMeasurementEventCallback<double>((_, value, tags, _) =>
            {
                var values = tags.ToArray().ToDictionary(tag => tag.Key, tag => tag.Value);
                if (Equals(values["shoko.queue.job.type"], _jobType))
                    Durations.Enqueue(values);
            });
            _meterListener.Start();
        }

        public async Task<(Activity Span, Dictionary<string, object?> Duration)> WaitForFirst()
        {
            var cancellationToken = TestContext.Current.CancellationToken;
            var stopwatch = Stopwatch.StartNew();
            while (stopwatch.Elapsed < _timeout)
            {
                if (Spans.TryPeek(out var span) && Durations.TryPeek(out var duration))
                    return (span, duration);

                await Task.Delay(10, cancellationToken);
            }

            throw new TimeoutException($"No telemetry for {_jobType}.");
        }

        public async Task<List<Activity>> WaitForSpans(int count)
        {
            var stopwatch = Stopwatch.StartNew();
            while (Spans.Count < count && stopwatch.Elapsed < _timeout)
                await Task.Delay(10, TestContext.Current.CancellationToken);

            Assert.Equal(count, Spans.Count);
            return [.. Spans];
        }

        public void Dispose()
        {
            _spanListener.Dispose();
            _meterListener.Dispose();
        }
    }

    private static string NewName() => Guid.NewGuid().ToString("N");

    private static void AssertLinksTo(Activity expected, Activity span)
    {
        var link = Assert.Single(span.Links);
        Assert.Equal(expected.TraceId, link.Context.TraceId);
        Assert.Equal(expected.SpanId, link.Context.SpanId);
    }

    #endregion

    [Fact]
    public async Task AJobQueuedInsideASpan_LinksBackToItAndStaysARoot()
    {
        using var recorder = new Recorder(typeof(SucceedJob));
        using var harness = new Harness();
        using var source = new ActivitySource(QueueTelemetry.Name);
        using var queuedBy = source.StartActivity("queued-by");

        await harness.Scheduler.Enqueue<SucceedJob>(job => job.Name = NewName(), ct: TestContext.Current.CancellationToken);

        var span = Assert.Single(await recorder.WaitForSpans(1));
        Assert.Null(span.ParentId);
        Assert.NotEqual(queuedBy!.TraceId, span.TraceId);
        AssertLinksTo(queuedBy, span);
    }

    [Fact]
    public async Task AJobQueuedOutsideASpan_HasNoLink()
    {
        using var recorder = new Recorder(typeof(SucceedJob));
        using var harness = new Harness();

        await harness.Scheduler.Enqueue<SucceedJob>(job => job.Name = NewName(), ct: TestContext.Current.CancellationToken);

        Assert.Empty(Assert.Single(await recorder.WaitForSpans(1)).Links);
    }

    [Fact]
    public void ASpanNotBeingKept_IsNotCaptured()
    {
        using var notRecorded = new Activity("not-recorded").Start();

        Assert.False(notRecorded.Recorded);
        Assert.Null(QueueTelemetry.CaptureTraceParent());
    }

    [Fact]
    public void AStoredTraceContextThatCannotBeRead_StartsTheJobWithoutALink()
    {
        using var recorder = new Recorder(typeof(SucceedJob));

        using var span = QueueTelemetry.StartJob(JobTypeNames.Key(typeof(SucceedJob)), "pool", "not-a-trace-context");

        Assert.NotNull(span);
        Assert.Empty(span.Links);
    }

    [Fact]
    public async Task EveryAttemptOfARequeuedJob_LinksBackToWhatQueuedIt()
    {
        using var recorder = new Recorder(typeof(RequeueOnceJob));
        using var harness = new Harness();
        using var source = new ActivitySource(QueueTelemetry.Name);
        using var queuedBy = source.StartActivity("queued-by");

        await harness.Scheduler.Enqueue<RequeueOnceJob>(job => job.Name = NewName(), ct: TestContext.Current.CancellationToken);

        var attempts = await recorder.WaitForSpans(2);
        Assert.All(attempts, attempt => AssertLinksTo(queuedBy!, attempt));
    }

    [Fact]
    public async Task AJobWithItsPriorityRaised_KeepsTheLinkOfWhatQueuedItFirst()
    {
        using var recorder = new Recorder(typeof(SucceedJob));
        using var harness = new Harness();
        using var source = new ActivitySource(QueueTelemetry.Name);
        var name = NewName();
        var queuedBy = source.StartActivity("queued-by");

        await harness.Scheduler.Enqueue<SucceedJob>(job => job.Name = name, scheduledAt: DateTimeOffset.UtcNow.AddHours(1), ct: TestContext.Current.CancellationToken);
        queuedBy!.Dispose();
        await harness.Scheduler.EnqueueImmediate<SucceedJob>(job => job.Name = name, ct: TestContext.Current.CancellationToken).WaitAsync(_timeout, TestContext.Current.CancellationToken);

        AssertLinksTo(queuedBy, Assert.Single(await recorder.WaitForSpans(1)));
    }

    [Fact]
    public async Task AJobQueuedByAJob_LinksBackToThatJob()
    {
        using var recorder = new Recorder(typeof(FollowedByJob));
        using var harness = new Harness();
        using var source = new ActivitySource(QueueTelemetry.Name);
        using var queuedBy = source.StartActivity("queued-by");

        await harness.Scheduler.Enqueue<FollowedByJob>(job => (job.Name, job.Then) = (NewName(), NewName()), ct: TestContext.Current.CancellationToken);

        var spans = await recorder.WaitForSpans(2);
        var first = spans.Single(span => span.Links.Single().Context.SpanId == queuedBy!.SpanId);
        AssertLinksTo(first, spans.Single(span => span != first));
    }

    [Fact]
    public async Task EveryStepOfAChain_LinksBackToWhatCreatedIt()
    {
        using var recorder = new Recorder(typeof(FollowedByJob));
        using var harness = new Harness();
        using var source = new ActivitySource(QueueTelemetry.Name);
        using var queuedBy = source.StartActivity("queued-by");

        await harness.Scheduler.CreateJobChain()
            .Then<FollowedByJob>(job => job.Name = NewName())
            .Then<FollowedByJob>(job => job.Name = NewName())
            .Enqueue();

        Assert.All(await recorder.WaitForSpans(2), span => AssertLinksTo(queuedBy!, span));
    }

    [Fact]
    public async Task ASucceedingJob_GetsARootSpanAndADuration()
    {
        using var recorder = new Recorder(typeof(SucceedJob));
        using var harness = new Harness();

        await harness.Scheduler.Enqueue<SucceedJob>(job => job.Name = NewName(), ct: TestContext.Current.CancellationToken);

        var (span, duration) = await recorder.WaitForFirst();
        Assert.Equal(ActivityKind.Consumer, span.Kind);
        Assert.Null(span.ParentId);
        Assert.Equal("success", span.GetTagItem("shoko.queue.job.outcome"));
        Assert.Equal("TelemetryPool", span.GetTagItem("shoko.queue.pool"));
        Assert.Equal(ActivityStatusCode.Unset, span.Status);
        Assert.Equal("success", duration["shoko.queue.job.outcome"]);
        Assert.Equal("TelemetryPool", duration["shoko.queue.pool"]);
    }

    [Fact]
    public async Task AFailingJob_ReportsTheErrorsTypeButNotItsMessage()
    {
        using var recorder = new Recorder(typeof(FailJob));
        using var harness = new Harness();

        await harness.Scheduler.Enqueue<FailJob>(job => job.Name = NewName(), ct: TestContext.Current.CancellationToken);

        var (span, duration) = await recorder.WaitForFirst();
        Assert.Equal("failed", span.GetTagItem("shoko.queue.job.outcome"));
        Assert.Equal(typeof(InvalidOperationException).FullName, span.GetTagItem("error.type"));
        Assert.Equal(ActivityStatusCode.Error, span.Status);
        Assert.Null(span.StatusDescription);
        Assert.DoesNotContain(span.Tags, tag => tag.Value?.Contains("hunter2") is true);
        Assert.Empty(span.Events);
        Assert.Equal("failed", duration["shoko.queue.job.outcome"]);
    }

    [Fact]
    public async Task ARequeuedJob_GetsOneSpanPerAttempt()
    {
        using var recorder = new Recorder(typeof(RequeueOnceJob));
        using var harness = new Harness();

        await harness.Scheduler.Enqueue<RequeueOnceJob>(job => job.Name = NewName(), ct: TestContext.Current.CancellationToken);

        var (span, duration) = await recorder.WaitForFirst();
        Assert.Equal("requeued", span.GetTagItem("shoko.queue.job.outcome"));
        Assert.Equal(ActivityStatusCode.Unset, span.Status);
        Assert.Equal("requeued", duration["shoko.queue.job.outcome"]);

        var stopwatch = Stopwatch.StartNew();
        while (recorder.Spans.Count < 2 && stopwatch.Elapsed < _timeout)
            await Task.Delay(10, TestContext.Current.CancellationToken);
        Assert.Equal(["requeued", "success"], recorder.Spans.Select(attempt => attempt.GetTagItem("shoko.queue.job.outcome")));
    }

    [Fact]
    public async Task ACancelledJob_ReportsItWasCancelled()
    {
        using var recorder = new Recorder(typeof(WaitForCancelJob));
        using var harness = new Harness();
        var name = NewName();

        await harness.Scheduler.Enqueue<WaitForCancelJob>(job => job.Name = name, ct: TestContext.Current.CancellationToken);
        await Ran(name).Task.WaitAsync(_timeout, TestContext.Current.CancellationToken);
        await harness.Scheduler.Cancel(JobKeyBuilder.BuildFor(new WaitForCancelJob(null!) { Name = name }), TestContext.Current.CancellationToken);

        var (span, duration) = await recorder.WaitForFirst();
        Assert.Equal("cancelled", span.GetTagItem("shoko.queue.job.outcome"));
        Assert.Equal(ActivityStatusCode.Unset, span.Status);
        Assert.Equal("cancelled", duration["shoko.queue.job.outcome"]);
    }

    [Fact]
    public async Task AThrowingListener_DoesNotChangeWhatHappensToJobs()
    {
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Meter.Name == QueueTelemetry.Name && instrument.Name == "shoko.queue.job.duration")
                meterListener.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<double>((_, _, _, _) => throw new InvalidOperationException("A listener failed."));
        listener.Start();
        using var harness = new Harness();

        // More jobs than workers, so they only all run if a failed recording leaves the workers alive.
        var names = Enumerable.Range(0, 5).Select(_ => NewName()).ToList();
        await harness.Scheduler.Enqueue<FailJob>(job => job.Name = names[0], ct: TestContext.Current.CancellationToken);
        foreach (var name in names.Skip(1))
            await harness.Scheduler.Enqueue<SucceedJob>(job => job.Name = name, ct: TestContext.Current.CancellationToken);

        await Task.WhenAll(names.Select(name => Ran(name).Task)).WaitAsync(_timeout, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task AJobRunInline_IsAChildOfWhatRunIt()
    {
        using var recorder = new Recorder(typeof(SucceedJob));
        using var harness = new Harness();
        using var source = new ActivitySource(QueueTelemetry.Name);
        using var parent = source.StartActivity("parent");

        await harness.JobFactory.Execute<SucceedJob>(job => job.Name = NewName());

        var span = Assert.Single(recorder.Spans);
        Assert.Equal(parent!.SpanId, span.ParentSpanId);
        Assert.Equal("success", span.GetTagItem("shoko.queue.job.outcome"));
        Assert.Empty(recorder.Durations);
    }

    [Fact]
    public async Task AJobRunInlineThatFails_ReportsItAndStillThrows()
    {
        using var recorder = new Recorder(typeof(FailJob));
        using var harness = new Harness();

        await Assert.ThrowsAsync<InvalidOperationException>(() => harness.JobFactory.Execute<FailJob>(job => job.Name = NewName()));

        var span = Assert.Single(recorder.Spans);
        Assert.Equal("failed", span.GetTagItem("shoko.queue.job.outcome"));
        Assert.Equal(ActivityStatusCode.Error, span.Status);
    }

    [Fact]
    public void WithoutAListener_NoSpanIsCreated()
        => Assert.Null(QueueTelemetry.StartJob("not-listened-to-" + NewName(), "pool"));

    [Fact]
    public void ThePoolGauges_ReportEveryPool()
    {
        var measurements = new ConcurrentDictionary<(string Instrument, string? Pool), int>();
        using var listener = new MeterListener();
        Meter? gauges = null;
        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (ReferenceEquals(instrument.Meter, gauges) || instrument.Meter.Name == QueueTelemetry.Name && instrument.IsObservable)
                meterListener.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<int>((instrument, value, tags, _) =>
            measurements[(instrument.Name, tags.ToArray().Single(tag => tag.Key == "shoko.queue.pool").Value as string)] = value
        );
        listener.Start();

        var pool = "gauge-pool-" + NewName();
        gauges = QueueTelemetry.CreatePoolGauges(() => new Dictionary<string, PoolStatus>
        {
            [pool] = new() { Name = pool, WaitingCount = 1, BlockedCount = 2, ScheduledCount = 3, ActiveWorkers = 4, MaxWorkers = 5 },
        });
        using (gauges)
            listener.RecordObservableInstruments();

        Assert.Equal(1, measurements[("shoko.queue.jobs.waiting", pool)]);
        Assert.Equal(2, measurements[("shoko.queue.jobs.blocked", pool)]);
        Assert.Equal(3, measurements[("shoko.queue.jobs.scheduled", pool)]);
        Assert.Equal(4, measurements[("shoko.queue.jobs.executing", pool)]);
        Assert.Equal(5, measurements[("shoko.queue.workers.limit", pool)]);
    }
}
