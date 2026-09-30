using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using Newtonsoft.Json;
using Shoko.Abstractions.Actions;
using Shoko.Abstractions.Core.Services;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Plugin;
using Shoko.Abstractions.ScheduledActions;
using Shoko.QueueProcessor.Builder;
using Shoko.Server.Actions;
using Shoko.Server.Models.Internal;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Repositories.Direct;
using Shoko.Server.Scheduling;
using Shoko.Server.Server;
using Shoko.Server.Services;
using Shoko.Server.Settings;
using Shoko.Tests.API;
using Shoko.Tests.Infrastructure;
using Shoko.Tests.Scheduling;
using Xunit;

namespace Shoko.Tests.Services;

/// <summary>
/// Covers the action scheduler over a real queue with no workers running: what
/// it queues at start-up and on its ticks, that a run still waiting is not
/// queued twice, the state of a run through cancellation, the triggers it
/// stores and the minimum it keeps between runs, and what it takes over from an upgrade.
/// </summary>
public sealed class ScheduledActionServiceTests : IDisposable
{
    #region Fixture

    /// <summary>
    /// A clock that only moves when told to, in the test time zone, whose
    /// timers never fire: the tests tick the scheduler themselves. Setting
    /// <see cref="Now"/> moves the wall clock only; advancing moves both it and
    /// the monotonic timestamp.
    /// </summary>
    private sealed class ManualClock : TimeProvider
    {
        private long _timestamp;

        public DateTimeOffset Now { get; set; } = new(2026, 9, 28, 12, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => Now;

        public override long GetTimestamp() => _timestamp;

        public override long TimestampFrequency => TimeSpan.TicksPerSecond;

        public override TimeZoneInfo LocalTimeZone => ActionTriggerScheduleTests.CentralEurope;

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period) => new IdleTimer();

        public void Advance(TimeSpan time)
        {
            Now += time;
            _timestamp += time.Ticks;
        }

        private sealed class IdleTimer : ITimer
        {
            public bool Change(TimeSpan dueTime, TimeSpan period) => true;

            public void Dispose() { }

            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }

    /// <summary>
    /// A scheduled action of the fake registry.
    /// </summary>
    private sealed record FakeAction(
        ScheduledActionDefinition Definition,
        int JobId,
        Type? ActionType
    );

    /// <summary>
    /// The action registry, with each action's run being a
    /// <see cref="QueueControllerTests.CancellableJob"/> of its own ID.
    /// </summary>
    private sealed class FakeSource(QueueControllerTests.Fixture queue) : IScheduledActionSource
    {
        private readonly List<FakeAction> _actions = [];

        public Dictionary<Guid, int> Runs { get; } = [];

        public Dictionary<Guid, string> Refusals { get; } = [];

        /// <summary>
        /// Called once a run's job is queued, before the queuing returns, as a
        /// worker may start it then.
        /// </summary>
        public Action<Guid>? AfterQueued { get; set; }

        public Guid Add(IReadOnlyList<ActionTrigger> defaults, Type? actionType = null, TimeSpan? minimum = null, bool countsManualRuns = false)
        {
            var id = Guid.NewGuid();
            var jobId = _actions.Count + 1;
            var jobKey = JobKeyBuilder<QueueControllerTests.CancellableJob>.Create().UsingJobData(job => job.Id = jobId).Build();
            var definition = new ScheduledActionDefinition(
                id,
                $"Action {jobId}",
                null,
                ActionCategory.Miscellaneous,
                "Miscellaneous",
                false,
                null,
                Guid.Empty,
                defaults,
                minimum ?? ActionTrigger.MinimumInterval,
                countsManualRuns,
                jobKey
            );
            _actions.Add(new(definition, jobId, actionType));
            return id;
        }

        public int RunsOf(Guid actionId) => Runs.GetValueOrDefault(actionId);

        public IReadOnlyList<ScheduledActionDefinition> GetActions() => _actions.Select(action => action.Definition).ToList();

        public ScheduledActionDefinition? GetAction(Type actionType) => _actions.FirstOrDefault(action => action.ActionType == actionType)?.Definition;

        public async Task<ActionValidationResult?> InvokeAsync(Guid actionId, CancellationToken token)
        {
            var jobId = Find(actionId).JobId;
            Runs[actionId] = RunsOf(actionId) + 1;
            if (Refusals.TryGetValue(actionId, out var reason))
                return new(reason);

            await queue.Scheduler.Enqueue<QueueControllerTests.CancellableJob>(job => job.Id = jobId, ct: token);
            AfterQueued?.Invoke(actionId);
            return null;
        }

        private FakeAction Find(Guid actionId)
            => _actions.Single(action => action.Definition.ID == actionId);
    }

    /// <summary>
    /// Keeps what the scheduler logs, with the values of each entry.
    /// </summary>
    private sealed class RecordingLogger : ILogger<ScheduledActionService>
    {
        public List<(LogLevel Level, IReadOnlyDictionary<string, object?> Values)> Entries { get; } = [];

        public List<IReadOnlyDictionary<string, object?>> Warnings => Entries.Where(entry => entry.Level is LogLevel.Warning).Select(entry => entry.Values).ToList();

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
            => null;

        public bool IsEnabled(LogLevel logLevel)
            => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            var values = state is IReadOnlyList<KeyValuePair<string, object?>> list
                ? list.Where(value => value.Key is not "{OriginalFormat}").ToDictionary(value => value.Key, value => value.Value)
                : [];
            lock (Entries)
                Entries.Add((logLevel, values));
        }
    }

    private readonly string _dataPath = Path.Join(Path.GetTempPath(), $"shoko-schedule-tests-{Guid.NewGuid():N}");

    private readonly QueueControllerTests.Fixture _queue = new();

    private readonly ManualClock _clock = new();

    private readonly RecordingLogger _logger = new();

    private readonly Mock<ScheduledActionRepository> _schedules;

    private readonly Mock<ScheduledUpdateRepository> _scheduledUpdates = new(new object[] { null! });

    private readonly Mock<ISystemService> _systemService = new();

    private readonly Mock<IApplicationPaths> _applicationPaths = new();

    private readonly FakeSource _source;

    private int _nextRowID = 1;

    public ScheduledActionServiceTests()
    {
        Directory.CreateDirectory(_dataPath);
        _applicationPaths.SetupGet(paths => paths.DataPath).Returns(_dataPath);
        _systemService.SetupGet(service => service.IsStarted).Returns(true);
        _source = new(_queue);
        _schedules = CachedRepo.BuildWritable<ScheduledActionRepository, int, ScheduledAction>(row => row.ScheduledActionID);
        _schedules.Setup(repository => repository.Save(It.IsAny<ScheduledAction>())).Callback<ScheduledAction>(row =>
        {
            if (row.ScheduledActionID is 0)
                row.ScheduledActionID = _nextRowID++;
            _schedules.Object.Cache.Update(row);
        });
    }

    public void Dispose()
    {
        _queue.Dispose();
        if (Directory.Exists(_dataPath))
            Directory.Delete(_dataPath, true);
    }

    private DateTime Now => _clock.Now.UtcDateTime;

    /// <summary>
    /// A scheduler over the shared rows and queue, as a fresh start of the
    /// server would build it.
    /// </summary>
    private ScheduledActionService Service()
        => new(
            _logger,
            _source,
            _schedules.Object,
            _scheduledUpdates.Object,
            _queue.Scheduler,
            _queue.Handler,
            _queue.Events,
            _systemService.Object,
            _applicationPaths.Object,
            _clock
        );

    /// <summary>
    /// Stores the row of an action, as a trigger's run when it ran.
    /// </summary>
    private void SeedRow(Guid actionId, DateTime? lastRunAt, string? triggers = null)
        => _schedules.Object.Save(new ScheduledAction
        {
            ActionID = actionId,
            CreatedAt = Now.AddDays(-30),
            LastRunAt = lastRunAt,
            LastScheduledRunAt = lastRunAt,
            Triggers = triggers,
        });

    /// <summary>
    /// Starts the waiting job of an action's run, as a worker would, and
    /// returns its ID.
    /// </summary>
    private Guid StartRun(ScheduledActionService service, Guid actionId)
    {
        var key = service.GetScheduledAction(actionId)!.JobKey;
        var job = _queue.Pool.TryAcquire();
        Assert.NotNull(job);
        Assert.Equal(key, job.JobKey);
        var executing = _queue.Orchestrator.GetExecuting();
        _queue.Events.OnJobExecuting(executing.Single(entry => entry.Id == job.Id), [], 0, 0, 4);
        return job.Id;
    }

    /// <summary>
    /// Finishes a started job, as a worker would.
    /// </summary>
    private void FinishRun(Guid jobId)
    {
        var entry = _queue.Orchestrator.GetExecuting().Single(entry => entry.Id == jobId);
        _queue.Orchestrator.OnComplete(jobId);
        _queue.Events.OnJobCompleted(entry, [], 0, 0, 4);
    }

    #endregion

    #region Start-up

    [Fact]
    public async Task AStartupTrigger_QueuesTheActionOnceAtStart_AndRecordsTheRun()
    {
        var action = _source.Add([ActionTrigger.AtStartup]);
        using var service = Service();

        await service.StartSchedulingAsync(TestContext.Current.CancellationToken);
        _clock.Advance(TimeSpan.FromHours(1));
        await service.TickAsync(TestContext.Current.CancellationToken);

        Assert.Equal(1, _source.RunsOf(action));
        var info = service.GetScheduledAction(action)!;
        Assert.Equal(ScheduledActionState.Waiting, info.State);
        Assert.True(info.IsCancellable);
        Assert.Equal(Now.AddHours(-1), info.LastRunAt);
        Assert.Null(info.NextRunAt);
        Assert.True(_queue.Scheduler.IsQueued(info.JobKey));
    }

    [Fact]
    public async Task RunsMissedWhileTheServerWasDown_RunOnceAtStart()
    {
        var action = _source.Add([ActionTrigger.Every(TimeSpan.FromHours(1)), ActionTrigger.DailyAt(new TimeOnly(3, 0))]);
        SeedRow(action, Now.AddDays(-3));
        using var service = Service();

        await service.StartSchedulingAsync(TestContext.Current.CancellationToken);
        await service.TickAsync(TestContext.Current.CancellationToken);

        Assert.Equal(1, _source.RunsOf(action));
        var info = service.GetScheduledAction(action)!;
        Assert.Equal(Now, info.LastRunAt);
        Assert.Equal(Now.AddHours(1), info.NextRunAt);
    }

    [Fact]
    public async Task ANewAction_CountsFromWhenItWasFirstSeen_AndRunsWhenDue()
    {
        var action = _source.Add([ActionTrigger.Every(TimeSpan.FromHours(1))]);
        using var service = Service();

        await service.StartSchedulingAsync(TestContext.Current.CancellationToken);
        Assert.Equal(0, _source.RunsOf(action));
        Assert.Null(service.GetScheduledAction(action)!.LastRunAt);
        Assert.Equal(Now.AddHours(1), service.GetScheduledAction(action)!.NextRunAt);

        _clock.Advance(TimeSpan.FromMinutes(59));
        await service.TickAsync(TestContext.Current.CancellationToken);
        Assert.Equal(0, _source.RunsOf(action));

        _clock.Advance(TimeSpan.FromMinutes(2));
        await service.TickAsync(TestContext.Current.CancellationToken);
        Assert.Equal(1, _source.RunsOf(action));

        _clock.Advance(TimeSpan.FromMinutes(1));
        await service.TickAsync(TestContext.Current.CancellationToken);
        Assert.Equal(1, _source.RunsOf(action));
    }

    [Fact]
    public async Task ATimerFiringAHairEarly_StillRunsTheAction_SoTheIntervalDoesNotDrift()
    {
        var action = _source.Add([ActionTrigger.Every(TimeSpan.FromMinutes(1))]);
        using var service = Service();

        await service.StartSchedulingAsync(TestContext.Current.CancellationToken);
        _clock.Advance(TimeSpan.FromMinutes(1) - TimeSpan.FromMilliseconds(3));
        await service.TickAsync(TestContext.Current.CancellationToken);

        Assert.Equal(1, _source.RunsOf(action));
        Assert.Equal(Now.AddMinutes(1), service.GetScheduledAction(action)!.NextRunAt);
    }

    [Fact]
    public async Task ARestartBeforeTheIntervalIsUp_DoesNotPushTheRunBack()
    {
        var action = _source.Add([ActionTrigger.Every(TimeSpan.FromHours(24))]);
        using (var first = Service())
            await first.StartSchedulingAsync(TestContext.Current.CancellationToken);

        _clock.Advance(TimeSpan.FromHours(23));
        using (var second = Service())
            await second.StartSchedulingAsync(TestContext.Current.CancellationToken);
        Assert.Equal(0, _source.RunsOf(action));

        _clock.Advance(TimeSpan.FromHours(2));
        using var third = Service();
        await third.StartSchedulingAsync(TestContext.Current.CancellationToken);
        Assert.Equal(1, _source.RunsOf(action));
    }

    [Fact]
    public async Task AScheduleThatCannotBeSavedAtStart_IsLoadedOnALaterTick()
    {
        var action = _source.Add([ActionTrigger.AtStartup, ActionTrigger.Every(TimeSpan.FromHours(1))]);
        var saves = 0;
        _schedules.Setup(repository => repository.Save(It.IsAny<ScheduledAction>())).Callback<ScheduledAction>(row =>
        {
            if (saves++ is 0)
                throw new InvalidOperationException("database is locked");
            if (row.ScheduledActionID is 0)
                row.ScheduledActionID = _nextRowID++;
            _schedules.Object.Cache.Update(row);
        });
        using var service = Service();

        await service.StartSchedulingAsync(TestContext.Current.CancellationToken);
        Assert.Equal(0, _source.RunsOf(action));

        // The next tick loads the schedule and runs the start-up pass it missed, once.
        await service.TickAsync(TestContext.Current.CancellationToken);
        Assert.Equal(1, _source.RunsOf(action));
        await service.TickAsync(TestContext.Current.CancellationToken);
        Assert.Equal(1, _source.RunsOf(action));
        Assert.Equal(Now, service.GetScheduledAction(action)!.LastRunAt);
    }

    [Fact]
    public async Task ALastRunInTheFuture_IsCountedFromWhenItWasSeen_AndMovedBackOnceTheClockStaysBehind()
    {
        var action = _source.Add([ActionTrigger.Every(TimeSpan.FromHours(1))]);
        var future = Now.AddDays(1);
        SeedRow(action, future);
        using var service = Service();

        await service.StartSchedulingAsync(TestContext.Current.CancellationToken);
        var seenAt = Now;
        Assert.Equal(0, _source.RunsOf(action));
        Assert.Equal(future, service.GetScheduledAction(action)!.LastRunAt);
        Assert.Equal(seenAt.AddHours(1), service.GetScheduledAction(action)!.NextRunAt);

        // Behind for longer than the minimum of a minute: it was set back for good.
        _clock.Advance(TimeSpan.FromMinutes(61));
        await service.TickAsync(TestContext.Current.CancellationToken);
        Assert.Equal(1, _source.RunsOf(action));
        Assert.Equal(Now, service.GetScheduledAction(action)!.LastRunAt);
    }

    [Fact]
    public async Task AClockThatIsBehindOnlyAtBoot_DoesNotLoseTheLastRun()
    {
        var sixHours = TimeSpan.FromHours(6);
        var action = _source.Add([ActionTrigger.AtStartup, ActionTrigger.Every(sixHours)], minimum: sixHours);
        var realNow = _clock.Now;
        var lastRun = Now.AddHours(-1);
        SeedRow(action, lastRun);

        // A box with no clock of its own boots at the epoch, then syncs half a minute later.
        _clock.Now = DateTimeOffset.UnixEpoch;
        using var service = Service();
        await service.StartSchedulingAsync(TestContext.Current.CancellationToken);
        Assert.Equal(0, _source.RunsOf(action));

        _clock.Advance(TimeSpan.FromSeconds(30));
        await service.TickAsync(TestContext.Current.CancellationToken);
        _clock.Now = realNow + TimeSpan.FromSeconds(30);
        await service.TickAsync(TestContext.Current.CancellationToken);

        Assert.Equal(0, _source.RunsOf(action));
        Assert.Equal(lastRun, _schedules.Object.GetByActionID(action)!.LastRunAt);
        Assert.Equal(lastRun + sixHours, service.GetScheduledAction(action)!.NextRunAt);

        _clock.Advance(TimeSpan.FromHours(5));
        await service.TickAsync(TestContext.Current.CancellationToken);
        Assert.Equal(1, _source.RunsOf(action));
    }

    [Fact]
    public async Task ARunByHandInTheFuture_IsMovedBackNoFurtherThanTheLastScheduledRun_WithoutAWarning()
    {
        var hour = TimeSpan.FromHours(1);
        var action = _source.Add([ActionTrigger.Every(hour)], minimum: hour);
        _schedules.Object.Save(new ScheduledAction
        {
            ActionID = action,
            CreatedAt = Now.AddDays(-30),
            LastRunAt = Now.AddDays(1),
            LastScheduledRunAt = Now.AddMinutes(-50),
        });
        using var service = Service();
        await service.StartSchedulingAsync(TestContext.Current.CancellationToken);

        // The trigger runs before the clock has stayed behind for the minimum.
        _clock.Advance(TimeSpan.FromMinutes(10));
        await service.TickAsync(TestContext.Current.CancellationToken);
        var scheduledAt = Now;
        Assert.Equal(1, _source.RunsOf(action));

        _clock.Advance(TimeSpan.FromMinutes(55));
        await service.TickAsync(TestContext.Current.CancellationToken);

        var row = _schedules.Object.GetByActionID(action)!;
        Assert.Equal(scheduledAt, row.LastRunAt);
        Assert.Equal(scheduledAt, row.LastScheduledRunAt);
        Assert.Equal(1, _source.RunsOf(action));
        Assert.Empty(_logger.Warnings);
    }

    [Fact]
    public async Task BeforeTheServerHasStarted_TheScheduleIsUnavailable()
    {
        _systemService.SetupGet(service => service.IsStarted).Returns(false);
        _source.Add([ActionTrigger.AtStartup]);
        using var service = Service();

        Assert.Throws<InvalidOperationException>(() => service.GetScheduledActions());
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.StartSchedulingAsync(TestContext.Current.CancellationToken));
    }

    #endregion

    #region Queue

    [Fact]
    public async Task ATriggerFiringWhileARunIsStillWaiting_DoesNotQueueItTwice()
    {
        var action = _source.Add([ActionTrigger.Every(TimeSpan.FromMinutes(1))]);
        using var service = Service();
        await service.StartSchedulingAsync(TestContext.Current.CancellationToken);

        _clock.Advance(TimeSpan.FromMinutes(1));
        await service.TickAsync(TestContext.Current.CancellationToken);
        _clock.Advance(TimeSpan.FromMinutes(1));
        await service.TickAsync(TestContext.Current.CancellationToken);

        Assert.Equal(2, _source.RunsOf(action));
        Assert.Equal(1, _queue.Handler.TotalCount);
    }

    [Fact]
    public async Task TheStateOfARun_FollowsItsJob_ThroughCancellation()
    {
        var action = _source.Add([ActionTrigger.AtStartup]);
        using var service = Service();
        await service.StartSchedulingAsync(TestContext.Current.CancellationToken);
        Assert.Equal(ScheduledActionState.Waiting, service.GetScheduledAction(action)!.State);

        Assert.NotNull(_queue.Pool.TryAcquire());
        var running = service.GetScheduledAction(action)!;
        Assert.Equal(ScheduledActionState.Running, running.State);
        Assert.True(running.IsCancellable);
        Assert.Null(running.Progress);

        var cancelled = await service.Cancel(action, TestContext.Current.CancellationToken);
        Assert.Equal(ScheduledActionState.CancellationRequested, cancelled.State);
        Assert.Equal(ScheduledActionState.CancellationRequested, service.GetScheduledActions().Single().State);
    }

    [Fact]
    public async Task CancellingAWaitingRun_RemovesIt_AndFreesItsKey()
    {
        var action = _source.Add([ActionTrigger.AtStartup]);
        using var service = Service();
        await service.StartSchedulingAsync(TestContext.Current.CancellationToken);

        var info = await service.Cancel(action, TestContext.Current.CancellationToken);

        Assert.Equal(ScheduledActionState.Idle, info.State);
        Assert.False(info.IsCancellable);
        Assert.False(_queue.Scheduler.IsQueued(info.JobKey));
        Assert.Null(await service.InvokeAsync(action, token: TestContext.Current.CancellationToken));
        Assert.True(_queue.Scheduler.IsQueued(info.JobKey));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RunningNow_QueuesTheAction_AsItsLastRun_WhichTheTriggersCountOnlyWhenItSaysSo(bool countsManualRuns)
    {
        var action = _source.Add([ActionTrigger.Every(TimeSpan.FromHours(6))], countsManualRuns: countsManualRuns);
        using var service = Service();
        await service.StartSchedulingAsync(TestContext.Current.CancellationToken);
        var firstSeen = Now;
        _clock.Advance(TimeSpan.FromHours(2));

        Assert.Null(await service.InvokeAsync(action, token: TestContext.Current.CancellationToken));

        var info = service.GetScheduledAction(action)!;
        Assert.Equal(1, _source.RunsOf(action));
        Assert.Equal(ScheduledActionState.Waiting, info.State);
        Assert.Equal(countsManualRuns, info.ScheduleCountsManualRuns);
        Assert.Equal(Now, info.LastRunAt);
        Assert.Null(info.LastScheduledRunAt);
        Assert.Equal(countsManualRuns ? Now.AddHours(6) : firstSeen.AddHours(6), info.NextRunAt);
    }

    [Fact]
    public async Task ARefusedRun_IsNotQueued_ButCountsAsARun()
    {
        var action = _source.Add([ActionTrigger.Every(TimeSpan.FromHours(1))]);
        _source.Refusals[action] = "Not now.";
        SeedRow(action, Now.AddDays(-1));
        using var service = Service();

        await service.StartSchedulingAsync(TestContext.Current.CancellationToken);
        await service.TickAsync(TestContext.Current.CancellationToken);

        var info = service.GetScheduledAction(action)!;
        Assert.Equal(1, _source.RunsOf(action));
        Assert.Equal(ScheduledActionState.Idle, info.State);
        Assert.Equal(Now, info.LastRunAt);
        Assert.Equal("Not now.", (await service.InvokeAsync(action, token: TestContext.Current.CancellationToken))?.Reason);
    }

    [Fact]
    public async Task AJobStartingBeforeItsQueuingReturns_IsStillTheTriggersRun()
    {
        var token = TestContext.Current.CancellationToken;
        var action = _source.Add([ActionTrigger.Every(TimeSpan.FromHours(1))]);
        SeedRow(action, Now.AddHours(-1));
        using var service = Service();
        _systemService.SetupGet(system => system.IsStarted).Returns(false);
        await service.StartAsync(token);
        _systemService.SetupGet(system => system.IsStarted).Returns(true);
        _source.AfterQueued = actionId =>
        {
            _clock.Advance(TimeSpan.FromMinutes(1));
            FinishRun(StartRun(service, actionId));
        };

        await service.StartSchedulingAsync(token);
        var startedAt = Now;
        var info = service.GetScheduledAction(action)!;
        Assert.Equal(startedAt, info.LastRunAt);
        Assert.Equal(startedAt, info.LastScheduledRunAt);

        // Nothing is left behind for a job other code queues next.
        _source.AfterQueued = null;
        await _queue.Scheduler.Enqueue<QueueControllerTests.CancellableJob>(job => job.Id = 1, ct: token);
        _clock.Advance(TimeSpan.FromMinutes(1));
        FinishRun(StartRun(service, action));
        info = service.GetScheduledAction(action)!;
        Assert.Equal(Now, info.LastRunAt);
        Assert.Equal(startedAt, info.LastScheduledRunAt);
        await service.StopAsync(token);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ARunWaitingFromBeforeARestart_StartsAsTheKindOfRunLastRecorded(bool byTrigger)
    {
        var token = TestContext.Current.CancellationToken;
        var action = _source.Add([ActionTrigger.Every(TimeSpan.FromHours(6))]);
        var scheduledAt = Now.AddHours(-2);
        SeedRow(action, scheduledAt);
        if (!byTrigger)
        {
            var row = _schedules.Object.GetByActionID(action)!;
            row.LastRunAt = Now.AddHours(-1);
            _schedules.Object.Save(row);
        }

        await _queue.Scheduler.Enqueue<QueueControllerTests.CancellableJob>(job => job.Id = 1, ct: token);
        using var service = Service();
        _systemService.SetupGet(system => system.IsStarted).Returns(false);
        await service.StartAsync(token);
        _systemService.SetupGet(system => system.IsStarted).Returns(true);
        await service.StartSchedulingAsync(token);

        _clock.Advance(TimeSpan.FromMinutes(5));
        FinishRun(StartRun(service, action));

        var info = service.GetScheduledAction(action)!;
        Assert.Equal(Now, info.LastRunAt);
        Assert.Equal(byTrigger ? Now : scheduledAt, info.LastScheduledRunAt);
        await service.StopAsync(token);
    }

    #endregion

    #region Triggers

    [Fact]
    public void SetTriggers_AreStored_AndSurviveARestart()
    {
        var action = _source.Add([ActionTrigger.Every(TimeSpan.FromHours(24))]);
        IReadOnlyList<ActionTrigger> triggers =
        [
            ActionTrigger.AtStartup,
            ActionTrigger.Every(TimeSpan.FromMinutes(90)),
            ActionTrigger.DailyAt(new TimeOnly(3, 30)),
            ActionTrigger.WeeklyOn(DayOfWeek.Monday, new TimeOnly(9, 0)),
        ];
        using (var first = Service())
        {
            var info = first.SetTriggers(action, triggers);
            Assert.True(info.HasCustomTriggers);
            Assert.Equal(triggers, info.Triggers);
            Assert.Equal([ActionTrigger.Every(TimeSpan.FromHours(24))], info.DefaultTriggers);
        }

        using var second = Service();
        var restored = second.GetScheduledAction(action)!;
        Assert.True(restored.HasCustomTriggers);
        Assert.Equal(triggers, restored.Triggers);

        var reset = second.ResetTriggers(action);
        Assert.False(reset.HasCustomTriggers);
        Assert.Equal([ActionTrigger.Every(TimeSpan.FromHours(24))], reset.Triggers);
        Assert.Null(_schedules.Object.GetByActionID(action)!.Triggers);
    }

    [Fact]
    public void StoredTriggers_ReadBackInTheirOwnOrder()
    {
        var json = ScheduledActionService.SerializeTriggers(
        [
            ActionTrigger.WeeklyOn([DayOfWeek.Friday, DayOfWeek.Monday], new TimeOnly(9, 0)),
            ActionTrigger.MonthlyOn([16, -1], new TimeOnly(4, 0)),
            ActionTrigger.Every(TimeSpan.FromMinutes(90)),
        ]);

        Assert.Equal(
            [ActionTrigger.WeeklyOn([DayOfWeek.Monday, DayOfWeek.Friday], new TimeOnly(9, 0)), ActionTrigger.MonthlyOn([-1, 16], new TimeOnly(4, 0)), ActionTrigger.Every(TimeSpan.FromMinutes(90))],
            ScheduledActionService.ReadTriggers(json)
        );
    }

    [Fact]
    public void AWeeklyTriggerStoredWithOneDay_IsReadAsASetOfThatDay()
    {
        const string Stored = """[{"Type":"Weekly","TimeOfDay":"09:00:00","DayOfWeek":"Monday"},{"Type":"Startup"}]""";
        var action = _source.Add([ActionTrigger.Every(TimeSpan.FromHours(24))]);
        SeedRow(action, Now, Stored);
        using var service = Service();

        var info = service.GetScheduledAction(action)!;

        Assert.True(info.HasCustomTriggers);
        Assert.Equal([ActionTrigger.WeeklyOn(DayOfWeek.Monday, new TimeOnly(9, 0)), ActionTrigger.AtStartup], info.Triggers);
        Assert.Equal([ActionTrigger.WeeklyOn(DayOfWeek.Monday, new TimeOnly(9, 0))], ScheduledActionService.ReadTriggers("""[{"Type":"Weekly","TimeOfDay":"09:00:00","DayOfWeek":1}]"""));
        Assert.Equal(new DateTime(2026, 10, 5, 7, 0, 0, DateTimeKind.Utc), info.NextRunAt);
    }

    [Fact]
    public async Task NoTriggers_MeansTheActionNeverRunsOnItsOwn()
    {
        var action = _source.Add([ActionTrigger.AtStartup, ActionTrigger.Every(TimeSpan.FromHours(1))]);
        SeedRow(action, Now.AddDays(-3));
        using var service = Service();
        service.SetTriggers(action, []);

        await service.StartSchedulingAsync(TestContext.Current.CancellationToken);

        Assert.Equal(0, _source.RunsOf(action));
        Assert.Empty(service.GetScheduledAction(action)!.Triggers);
        Assert.Null(service.GetScheduledAction(action)!.NextRunAt);
    }

    [Fact]
    public void AnInvalidTriggerOrAnUnknownAction_IsRefused()
    {
        var action = _source.Add([]);
        using var service = Service();

        Assert.Throws<ArgumentException>(() => service.SetTriggers(action, [new ActionTrigger { Type = ActionTriggerType.Weekly, TimeOfDay = new(3, 0) }]));
        Assert.Throws<KeyNotFoundException>(() => service.SetTriggers(Guid.NewGuid(), []));
        Assert.Null(service.GetScheduledAction(Guid.NewGuid()));
    }

    [Fact]
    public void StoredTriggersThatAreNoLongerValid_AreDropped_AndTheDefaultsApplyWhenNoneIsLeft()
    {
        var dropped = _source.Add([ActionTrigger.Every(TimeSpan.FromHours(24))]);
        var mixed = _source.Add([ActionTrigger.Every(TimeSpan.FromHours(24))]);
        SeedRow(dropped, null, """[{"Type":"Interval","Interval":"00:01:30"}]""");
        SeedRow(mixed, null, """[{"Type":"Interval","Interval":"00:01:30"},{"Type":"Interval","Interval":"02:00:00"}]""");
        using var service = Service();

        var fallback = service.GetScheduledAction(dropped)!;
        Assert.False(fallback.HasCustomTriggers);
        Assert.Equal([ActionTrigger.Every(TimeSpan.FromHours(24))], fallback.Triggers);
        Assert.Equal(Now.AddDays(-29), fallback.NextRunAt);

        var kept = service.GetScheduledAction(mixed)!;
        Assert.True(kept.HasCustomTriggers);
        Assert.Equal([ActionTrigger.Every(TimeSpan.FromHours(2))], kept.Triggers);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("""{"Type":"Daily","TimeOfDay":"04:00:00"}""")]
    public void StoredTriggersThatCanNotBeRead_FallBackToTheDefaults(string stored)
    {
        var action = _source.Add([ActionTrigger.Every(TimeSpan.FromHours(24))]);
        SeedRow(action, null, stored);
        using var service = Service();

        var info = service.GetScheduledAction(action)!;

        Assert.False(info.HasCustomTriggers);
        Assert.Equal([ActionTrigger.Every(TimeSpan.FromHours(24))], info.Triggers);
        Assert.Throws<JsonSerializationException>(() => ScheduledActionService.ReadTriggers("\"Daily\""));
        Assert.Empty(ScheduledActionService.ReadTriggers("null"));
    }

    #endregion

    #region Minimum Interval

    private static readonly TimeSpan SixHours = TimeSpan.FromHours(6);

    [Fact]
    public void AnIntervalUnderTheMinimum_IsRefused_AndTheMinimumIsShown()
    {
        var limited = _source.Add([], minimum: SixHours);
        var free = _source.Add([]);
        using var service = Service();

        var error = Assert.Throws<ArgumentException>(() => service.SetTriggers(limited, [ActionTrigger.DailyAt(new(3, 0)), ActionTrigger.Every(TimeSpan.FromHours(5))]));
        Assert.Contains("Trigger 1", error.Message);
        Assert.Contains("6 hours", error.Message);
        Assert.Throws<ArgumentException>(() => service.SetTriggers(free, [new ActionTrigger { Type = ActionTriggerType.Interval, Interval = TimeSpan.FromSeconds(150) }]));
        Assert.False(service.GetScheduledAction(limited)!.HasCustomTriggers);

        var info = service.SetTriggers(limited, [ActionTrigger.Every(SixHours), ActionTrigger.DailyAt(new(3, 0))]);
        Assert.Equal(SixHours, info.MinimumInterval);
        Assert.Equal(TimeSpan.FromMinutes(1), service.GetScheduledAction(free)!.MinimumInterval);
        Assert.Equal(TimeSpan.FromMinutes(1), service.SetTriggers(free, [ActionTrigger.Every(TimeSpan.FromMinutes(1))]).MinimumInterval);
    }

    /// <summary>
    /// Asserts a skip entry names <paramref name="trigger"/>, the first
    /// action, the time since its last run and its minimum.
    /// </summary>
    /// <param name="entry">The values of the entry.</param>
    /// <param name="trigger">The trigger whose time was skipped.</param>
    /// <param name="sinceLastRun">How long ago the action last ran.</param>
    private static void AssertSkip(IReadOnlyDictionary<string, object?> entry, ActionTrigger trigger, TimeSpan sinceLastRun)
    {
        Assert.Equal(trigger.Describe(), entry["Trigger"]);
        Assert.Equal("Action 1", entry["ActionName"]);
        Assert.Equal(sinceLastRun.ToTimeAgoString(), entry["LastRun"]);
        Assert.Equal(SixHours.ToDurationString(), entry["MinimumInterval"]);
    }

    [Fact]
    public async Task AWallClockTimeInsideTheMinimum_IsSkipped_AndLoggedOnce()
    {
        // Stored before such triggers were refused; 03:00 and 04:00 in Central Europe are 01:00 and
        // 02:00 UTC in summer. The previous day's 04:00, skipped while the server was down, is not logged.
        var action = _source.Add([], minimum: SixHours);
        _clock.Now = new(2026, 9, 28, 0, 30, 0, TimeSpan.Zero);
        SeedRow(action, new DateTime(2026, 9, 27, 1, 0, 0, DateTimeKind.Utc), ScheduledActionService.SerializeTriggers([ActionTrigger.DailyAt(new(3, 0)), ActionTrigger.DailyAt(new(4, 0))]));
        using var service = Service();

        await service.StartSchedulingAsync(TestContext.Current.CancellationToken);
        Assert.Equal(0, _source.RunsOf(action));
        Assert.Equal(new DateTime(2026, 9, 28, 1, 0, 0, DateTimeKind.Utc), service.GetScheduledAction(action)!.NextRunAt);

        _clock.Advance(TimeSpan.FromMinutes(30));
        await service.TickAsync(TestContext.Current.CancellationToken);
        Assert.Equal(1, _source.RunsOf(action));
        Assert.Empty(_logger.Warnings);

        // The 04:00 time is skipped, not moved to 09:00: the next run is 03:00 the next day.
        var nextDay = new DateTime(2026, 9, 29, 1, 0, 0, DateTimeKind.Utc);
        Assert.Equal(nextDay, service.GetScheduledAction(action)!.NextRunAt);
        foreach (var step in new[] { TimeSpan.FromMinutes(59), TimeSpan.FromMinutes(1), TimeSpan.Zero, TimeSpan.FromMinutes(1), TimeSpan.FromHours(6) })
        {
            _clock.Advance(step);
            await service.TickAsync(TestContext.Current.CancellationToken);
            Assert.Equal(1, _source.RunsOf(action));
            Assert.Equal(nextDay, service.GetScheduledAction(action)!.NextRunAt);
        }

        AssertSkip(Assert.Single(_logger.Warnings), ActionTrigger.DailyAt(new(4, 0)), TimeSpan.FromHours(1));

        _clock.Advance(nextDay - Now);
        await service.TickAsync(TestContext.Current.CancellationToken);
        Assert.Equal(2, _source.RunsOf(action));
        Assert.Single(_logger.Warnings);
    }

    [Fact]
    public async Task AStoredIntervalUnderTheMinimum_SkipsToTheNextIntervalPastIt()
    {
        var action = _source.Add([], minimum: SixHours);
        SeedRow(action, Now, ScheduledActionService.SerializeTriggers([ActionTrigger.Every(TimeSpan.FromHours(4))]));
        var ranAt = Now;
        using var service = Service();

        await service.StartSchedulingAsync(TestContext.Current.CancellationToken);
        Assert.Equal(0, _source.RunsOf(action));
        Assert.Equal(ranAt.AddHours(8), service.GetScheduledAction(action)!.NextRunAt);

        _clock.Advance(TimeSpan.FromHours(4));
        await service.TickAsync(TestContext.Current.CancellationToken);
        Assert.Equal(0, _source.RunsOf(action));
        AssertSkip(Assert.Single(_logger.Warnings), ActionTrigger.Every(TimeSpan.FromHours(4)), TimeSpan.FromHours(4));

        // Not at the end of the minimum, but on the next interval.
        _clock.Advance(TimeSpan.FromHours(2));
        await service.TickAsync(TestContext.Current.CancellationToken);
        Assert.Equal(0, _source.RunsOf(action));
        Assert.Equal(ranAt.AddHours(8), service.GetScheduledAction(action)!.NextRunAt);

        _clock.Advance(TimeSpan.FromHours(2));
        await service.TickAsync(TestContext.Current.CancellationToken);
        Assert.Equal(1, _source.RunsOf(action));
        Assert.Equal(Now.AddHours(8), service.GetScheduledAction(action)!.NextRunAt);
        Assert.Single(_logger.Warnings);
    }

    [Fact]
    public void TriggersRunningCloserThanTheMinimum_AreRefused()
    {
        var action = _source.Add([], minimum: SixHours);
        using var service = Service();

        Assert.Throws<ArgumentException>(() => service.SetTriggers(action, [ActionTrigger.DailyAt(new(3, 0)), ActionTrigger.DailyAt(new(4, 0))]));
        Assert.False(service.GetScheduledAction(action)!.HasCustomTriggers);

        Assert.True(service.SetTriggers(action, [ActionTrigger.DailyAt(new(3, 0)), ActionTrigger.DailyAt(new(9, 0))]).HasCustomTriggers);
    }

    [Fact]
    public async Task AWallClockRun_WhoseJobStartedLate_DoesNotPushTheNextOneOffItsMinute()
    {
        // 04:00 and 10:00 are 02:00 and 08:00 UTC in summer; the 04:00 run's
        // job started five seconds late, and the 10:00 one is exactly six hours on.
        var action = _source.Add([ActionTrigger.DailyAt(new(4, 0)), ActionTrigger.DailyAt(new(10, 0))], minimum: SixHours);
        _clock.Now = new(2026, 9, 28, 5, 0, 0, TimeSpan.Zero);
        SeedRow(action, new DateTime(2026, 9, 28, 2, 0, 5, DateTimeKind.Utc));
        using var service = Service();

        await service.StartSchedulingAsync(TestContext.Current.CancellationToken);

        Assert.Equal(0, _source.RunsOf(action));
        Assert.Equal(new DateTime(2026, 9, 28, 8, 0, 0, DateTimeKind.Utc), service.GetScheduledAction(action)!.NextRunAt);
    }

    [Fact]
    public async Task AWallClockTimer_FiringAHairEarly_RunsOnce_AndTheNextIsTomorrowOnTheMinute()
    {
        var action = _source.Add([ActionTrigger.MonthlyOn([28, 29], new TimeOnly(4, 0))]);
        _clock.Now = new(2026, 9, 28, 1, 0, 0, TimeSpan.Zero);
        SeedRow(action, Now.AddDays(-1));
        using var service = Service();
        await service.StartSchedulingAsync(TestContext.Current.CancellationToken);
        Assert.Equal(new DateTime(2026, 9, 28, 2, 0, 0, DateTimeKind.Utc), service.GetScheduledAction(action)!.NextRunAt);

        _clock.Advance(TimeSpan.FromHours(1) - TimeSpan.FromMilliseconds(3));
        await service.TickAsync(TestContext.Current.CancellationToken);
        Assert.Equal(1, _source.RunsOf(action));

        _clock.Advance(TimeSpan.FromMinutes(2));
        await service.TickAsync(TestContext.Current.CancellationToken);
        Assert.Equal(1, _source.RunsOf(action));
        Assert.Equal(new DateTime(2026, 9, 29, 2, 0, 0, DateTimeKind.Utc), service.GetScheduledAction(action)!.NextRunAt);
    }

    [Fact]
    public async Task AStartupTrigger_WithFrequentRestarts_IsSkippedInsideTheMinimum()
    {
        var action = _source.Add([ActionTrigger.AtStartup], minimum: SixHours);
        using (var first = Service())
            await first.StartSchedulingAsync(TestContext.Current.CancellationToken);
        Assert.Equal(1, _source.RunsOf(action));
        var ranAt = Now;

        _clock.Advance(TimeSpan.FromHours(1));
        using (var second = Service())
        {
            await second.StartSchedulingAsync(TestContext.Current.CancellationToken);
            Assert.Equal(1, _source.RunsOf(action));
            Assert.Equal(ranAt, second.GetScheduledAction(action)!.LastRunAt);
            Assert.Null(second.GetScheduledAction(action)!.NextRunAt);
            await second.TickAsync(TestContext.Current.CancellationToken);
        }

        AssertSkip(Assert.Single(_logger.Warnings), ActionTrigger.AtStartup, TimeSpan.FromHours(1));

        _clock.Advance(TimeSpan.FromHours(5));
        using var third = Service();
        await third.StartSchedulingAsync(TestContext.Current.CancellationToken);
        Assert.Equal(2, _source.RunsOf(action));
        Assert.Single(_logger.Warnings);
    }

    [Fact]
    public async Task AStoredIntervalUnderTheMinimum_SkipsEachTimeInsideIt()
    {
        var action = _source.Add([], minimum: SixHours);
        SeedRow(action, Now.AddHours(-2), ScheduledActionService.SerializeTriggers([ActionTrigger.Every(TimeSpan.FromHours(1))]));
        using var service = Service();

        await service.StartSchedulingAsync(TestContext.Current.CancellationToken);

        Assert.Equal(0, _source.RunsOf(action));
        Assert.Equal(Now.AddHours(4), service.GetScheduledAction(action)!.NextRunAt);
        _clock.Advance(TimeSpan.FromHours(4));
        await service.TickAsync(TestContext.Current.CancellationToken);
        Assert.Equal(1, _source.RunsOf(action));

        // The hourly times skipped since the start, each logged once, even when one tick finds them all.
        Assert.Equal(
            new[] { 3, 4, 5 }.Select(hours => TimeSpan.FromHours(hours).ToTimeAgoString()),
            _logger.Warnings.Select(warning => warning["LastRun"])
        );
    }

    [Fact]
    public async Task ARunByHand_IsNotHeldBackByTheMinimum_ButIsNotQueuedTwice()
    {
        var action = _source.Add([ActionTrigger.Every(SixHours)], minimum: SixHours, countsManualRuns: true);
        SeedRow(action, Now.AddHours(-1));
        var scheduledAt = Now.AddHours(-1);
        using var service = Service();
        await service.StartSchedulingAsync(TestContext.Current.CancellationToken);
        Assert.Equal(0, _source.RunsOf(action));

        Assert.Null(await service.InvokeAsync(action, token: TestContext.Current.CancellationToken));
        Assert.Equal(1, _source.RunsOf(action));
        var ranAt = Now;

        // Still waiting: neither queued again nor counted as a new run.
        _clock.Advance(TimeSpan.FromMinutes(10));
        Assert.Null(await service.InvokeAsync(action, token: TestContext.Current.CancellationToken));
        Assert.Equal(1, _source.RunsOf(action));
        Assert.Equal(ranAt, service.GetScheduledAction(action)!.LastRunAt);
        Assert.Equal(1, _queue.Handler.TotalCount);

        await service.Cancel(action, TestContext.Current.CancellationToken);
        Assert.Null(await service.InvokeAsync(action, token: TestContext.Current.CancellationToken));
        Assert.Equal(2, _source.RunsOf(action));
        Assert.Equal(Now, service.GetScheduledAction(action)!.LastRunAt);
        Assert.Equal(scheduledAt, service.GetScheduledAction(action)!.LastScheduledRunAt);
        Assert.Equal(Now + SixHours, service.GetScheduledAction(action)!.NextRunAt);
    }

    /// <summary>
    /// The entries a skipped trigger time logged, at any level.
    /// </summary>
    private List<(LogLevel Level, IReadOnlyDictionary<string, object?> Values)> SkipEntries()
        => _logger.Entries.Where(entry => entry.Values.ContainsKey("Trigger") && entry.Values.ContainsKey("LastRun")).ToList();

    [Fact]
    public async Task ARunByHand_OfAnActionThatDoesNotCountThem_DoesNotMoveItsInterval()
    {
        var action = _source.Add([ActionTrigger.Every(SixHours)], minimum: SixHours);
        SeedRow(action, Now.AddHours(-5));
        var scheduledAt = Now.AddHours(-5);
        using var service = Service();
        _systemService.SetupGet(system => system.IsStarted).Returns(false);
        await service.StartAsync(TestContext.Current.CancellationToken);
        _systemService.SetupGet(system => system.IsStarted).Returns(true);
        await service.StartSchedulingAsync(TestContext.Current.CancellationToken);

        _clock.Advance(TimeSpan.FromMinutes(30));
        Assert.Null(await service.InvokeAsync(action, token: TestContext.Current.CancellationToken));

        // Nor does the start of its job.
        _clock.Advance(TimeSpan.FromMinutes(5));
        FinishRun(StartRun(service, action));
        var info = service.GetScheduledAction(action)!;
        Assert.Equal(Now, info.LastRunAt);
        Assert.Equal(scheduledAt, info.LastScheduledRunAt);
        Assert.Equal(scheduledAt + SixHours, info.NextRunAt);

        _clock.Advance(scheduledAt + SixHours - Now);
        await service.TickAsync(TestContext.Current.CancellationToken);
        Assert.Equal(2, _source.RunsOf(action));
        Assert.Equal(Now, service.GetScheduledAction(action)!.LastScheduledRunAt);
        Assert.Empty(SkipEntries());
        await service.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task ARunByHand_OfAnActionThatDoesNotCountThem_SkipsNoWallClockTime()
    {
        // 04:00 is 02:00 UTC in summer.
        var action = _source.Add([ActionTrigger.DailyAt(new(4, 0))], minimum: SixHours);
        _clock.Now = new(2026, 9, 28, 0, 30, 0, TimeSpan.Zero);
        SeedRow(action, new DateTime(2026, 9, 27, 2, 0, 0, DateTimeKind.Utc));
        var due = new DateTime(2026, 9, 28, 2, 0, 0, DateTimeKind.Utc);
        using var service = Service();
        await service.StartSchedulingAsync(TestContext.Current.CancellationToken);

        _clock.Advance(TimeSpan.FromMinutes(30));
        Assert.Null(await service.InvokeAsync(action, token: TestContext.Current.CancellationToken));
        await service.Cancel(action, TestContext.Current.CancellationToken);
        Assert.Equal(due, service.GetScheduledAction(action)!.NextRunAt);

        _clock.Advance(due - Now);
        await service.TickAsync(TestContext.Current.CancellationToken);
        Assert.Equal(2, _source.RunsOf(action));
        Assert.Empty(SkipEntries());
    }

    [Fact]
    public async Task ARunByHand_OfAnActionThatCountsThem_SkipsAWallClockTimeInsideItsMinimum_AtDebugLevel()
    {
        // 04:00 is 02:00 UTC in summer.
        var action = _source.Add([ActionTrigger.DailyAt(new(4, 0))], minimum: SixHours, countsManualRuns: true);
        _clock.Now = new(2026, 9, 28, 0, 30, 0, TimeSpan.Zero);
        SeedRow(action, new DateTime(2026, 9, 27, 2, 0, 0, DateTimeKind.Utc));
        using var service = Service();
        await service.StartSchedulingAsync(TestContext.Current.CancellationToken);

        _clock.Advance(TimeSpan.FromMinutes(30));
        Assert.Null(await service.InvokeAsync(action, token: TestContext.Current.CancellationToken));
        await service.Cancel(action, TestContext.Current.CancellationToken);
        var nextDay = new DateTime(2026, 9, 29, 2, 0, 0, DateTimeKind.Utc);
        Assert.Equal(nextDay, service.GetScheduledAction(action)!.NextRunAt);

        _clock.Advance(TimeSpan.FromHours(1));
        await service.TickAsync(TestContext.Current.CancellationToken);
        Assert.Equal(1, _source.RunsOf(action));
        Assert.Equal(nextDay, service.GetScheduledAction(action)!.NextRunAt);

        var skip = Assert.Single(SkipEntries());
        Assert.Equal(LogLevel.Debug, skip.Level);
        AssertSkip(skip.Values, ActionTrigger.DailyAt(new(4, 0)), TimeSpan.FromHours(1));
    }

    [Fact]
    public async Task ASkipAfterATriggeredRun_OfAnActionThatCountsRunsByHand_IsStillAWarning()
    {
        var action = _source.Add([], minimum: SixHours, countsManualRuns: true);
        SeedRow(action, Now, ScheduledActionService.SerializeTriggers([ActionTrigger.Every(TimeSpan.FromHours(4))]));
        using var service = Service();
        await service.StartSchedulingAsync(TestContext.Current.CancellationToken);

        _clock.Advance(TimeSpan.FromHours(4));
        await service.TickAsync(TestContext.Current.CancellationToken);

        Assert.Equal(LogLevel.Warning, Assert.Single(SkipEntries()).Level);
    }

    [Fact]
    public async Task ARunThatWaitedInTheQueue_CountsFromWhenItStarted()
    {
        var action = _source.Add([ActionTrigger.Every(SixHours)], minimum: SixHours);
        SeedRow(action, Now - SixHours);
        using var service = Service();

        // Hooked to the queue, without the start in the background racing this one.
        _systemService.SetupGet(system => system.IsStarted).Returns(false);
        await service.StartAsync(TestContext.Current.CancellationToken);
        _systemService.SetupGet(system => system.IsStarted).Returns(true);
        await service.StartSchedulingAsync(TestContext.Current.CancellationToken);
        Assert.Equal(1, _source.RunsOf(action));
        var queuedAt = Now;

        // It waits behind other jobs for almost the whole interval.
        _clock.Advance(TimeSpan.FromMinutes(355));
        var jobId = StartRun(service, action);
        var startedAt = Now;
        Assert.Equal(startedAt, service.GetScheduledAction(action)!.LastRunAt);
        Assert.Equal(startedAt, service.GetScheduledAction(action)!.LastScheduledRunAt);
        _clock.Advance(TimeSpan.FromMinutes(1));
        FinishRun(jobId);

        _clock.Advance(queuedAt + SixHours - Now);
        await service.TickAsync(TestContext.Current.CancellationToken);
        Assert.Equal(1, _source.RunsOf(action));
        Assert.Equal(startedAt + SixHours, service.GetScheduledAction(action)!.NextRunAt);

        _clock.Advance(startedAt + SixHours - TimeSpan.FromMinutes(1) - Now);
        await service.TickAsync(TestContext.Current.CancellationToken);
        Assert.Equal(1, _source.RunsOf(action));

        _clock.Advance(TimeSpan.FromMinutes(1));
        await service.TickAsync(TestContext.Current.CancellationToken);
        Assert.Equal(2, _source.RunsOf(action));
        await service.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task AWallClockRun_WhoseJobStartedMinutesLate_DelaysTheNextOne_ButDoesNotSkipIt()
    {
        // 02:00, 08:00, 14:00 and 20:00 are 00:00, 06:00, 12:00 and 18:00 UTC
        // in summer, exactly the minimum apart.
        var triggers = new[] { 2, 8, 14, 20 }.Select(hour => ActionTrigger.DailyAt(new(hour, 0))).ToArray();
        var action = _source.Add(triggers, minimum: SixHours);
        _clock.Now = new(2026, 9, 27, 23, 59, 0, TimeSpan.Zero);
        SeedRow(action, new DateTime(2026, 9, 27, 18, 0, 0, DateTimeKind.Utc));
        using var service = Service();
        _systemService.SetupGet(system => system.IsStarted).Returns(false);
        await service.StartAsync(TestContext.Current.CancellationToken);
        _systemService.SetupGet(system => system.IsStarted).Returns(true);
        await service.StartSchedulingAsync(TestContext.Current.CancellationToken);

        _clock.Advance(TimeSpan.FromMinutes(1));
        await service.TickAsync(TestContext.Current.CancellationToken);
        Assert.Equal(1, _source.RunsOf(action));

        // Its job waits three minutes behind others.
        _clock.Advance(TimeSpan.FromMinutes(3) + TimeSpan.FromSeconds(10));
        FinishRun(StartRun(service, action));
        var delayed = new DateTime(2026, 9, 28, 6, 3, 0, DateTimeKind.Utc);
        Assert.Equal(delayed, service.GetScheduledAction(action)!.NextRunAt);

        _clock.Advance(new DateTime(2026, 9, 28, 6, 0, 0, DateTimeKind.Utc) - Now);
        await service.TickAsync(TestContext.Current.CancellationToken);
        Assert.Equal(1, _source.RunsOf(action));

        _clock.Advance(delayed - Now);
        await service.TickAsync(TestContext.Current.CancellationToken);
        Assert.Equal(2, _source.RunsOf(action));
        FinishRun(StartRun(service, action));

        // The 06:00 time it ran for is what the 12:00 one counts from, so that one is not skipped.
        Assert.Equal(delayed.AddHours(6), service.GetScheduledAction(action)!.NextRunAt);
        Assert.Empty(_logger.Warnings);
        await service.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task AWallClockTime_OnTheMinimum_RunsOnTheDayTheClockGoesForward()
    {
        // 00:00 on 29 March is 23:00 UTC the day before, and 06:00 is 04:00
        // UTC, as the clock skips an hour in between.
        var triggers = new[] { 0, 6, 12, 18 }.Select(hour => ActionTrigger.DailyAt(new(hour, 0))).ToArray();
        var action = _source.Add(triggers, minimum: SixHours);
        _clock.Now = new(2026, 3, 29, 3, 0, 0, TimeSpan.Zero);
        SeedRow(action, new DateTime(2026, 3, 28, 23, 0, 0, DateTimeKind.Utc));
        using var service = Service();

        await service.StartSchedulingAsync(TestContext.Current.CancellationToken);
        var sixAM = new DateTime(2026, 3, 29, 4, 0, 0, DateTimeKind.Utc);
        Assert.Equal(sixAM, service.GetScheduledAction(action)!.NextRunAt);

        _clock.Advance(sixAM - Now);
        await service.TickAsync(TestContext.Current.CancellationToken);
        Assert.Equal(1, _source.RunsOf(action));
        Assert.Empty(_logger.Warnings);
    }

    [Fact]
    public async Task TriggersSetAgainAfterBeingCleared_DoNotLogTheTimesTheyWereNotSet()
    {
        var action = _source.Add([ActionTrigger.DailyAt(new(12, 0))], minimum: SixHours);
        _clock.Now = new(2026, 9, 28, 0, 0, 0, TimeSpan.Zero);
        SeedRow(action, Now.AddHours(-1));
        using var service = Service();
        await service.StartSchedulingAsync(TestContext.Current.CancellationToken);
        Assert.Equal(0, _source.RunsOf(action));

        service.SetTriggers(action, []);
        _clock.Advance(TimeSpan.FromMinutes(1));
        await service.TickAsync(TestContext.Current.CancellationToken);

        _clock.Advance(TimeSpan.FromHours(1));
        Assert.Null(await service.InvokeAsync(action, token: TestContext.Current.CancellationToken));
        Assert.Equal(1, _source.RunsOf(action));

        _clock.Advance(TimeSpan.FromHours(7));
        // 06:00 is 04:00 UTC in summer.
        service.SetTriggers(action, [ActionTrigger.DailyAt(new(6, 0))]);
        await service.TickAsync(TestContext.Current.CancellationToken);
        Assert.Empty(_logger.Warnings);
    }

    [Fact]
    public void CarriedOverFrequencies_UnderTheMinimum_AreRaisedToIt()
    {
        var notifications = _source.Add([], typeof(GetAnidbNotificationsAction), SixHours);
        var files = _source.Add([ActionTrigger.Every(TimeSpan.FromHours(24))], typeof(CheckAnidbFileUpdatesAction), SixHours);
        var plugins = _source.Add([ActionTrigger.Every(SixHours)], typeof(CheckPluginUpdatesAction), TimeSpan.FromHours(1));
        var path = SettingsMigrations.UpdateFrequencyCarryOverPath(_dataPath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonConvert.SerializeObject(new Dictionary<string, int>
        {
            [SettingsMigrations.AnidbNotificationFrequency] = 1,
            [SettingsMigrations.AnidbFileFrequency] = 12,
            [SettingsMigrations.PluginUpdatesFrequency] = 1,
        }));
        using var service = Service();

        var byId = service.GetScheduledActions().ToDictionary(info => info.ID);

        Assert.Equal([ActionTrigger.Every(SixHours)], byId[notifications].Triggers);
        Assert.Equal([ActionTrigger.Every(TimeSpan.FromHours(12))], byId[files].Triggers);
        Assert.Equal([ActionTrigger.Every(TimeSpan.FromHours(1))], byId[plugins].Triggers);
    }

    #endregion

    #region Upgrade

    [Fact]
    public void CarriedOverFrequencies_BecomeTriggers_UnlessTheyAreTheDefaults()
    {
        var notifications = _source.Add([], typeof(GetAnidbNotificationsAction));
        var files = _source.Add([ActionTrigger.Every(TimeSpan.FromHours(24))], typeof(CheckAnidbFileUpdatesAction));
        var calendar = _source.Add([], typeof(UpdateAnidbCalendarAction));
        var plugins = _source.Add([ActionTrigger.Every(TimeSpan.FromHours(6))], typeof(CheckPluginUpdatesAction));
        SeedRow(calendar, null, "[]");
        var path = SettingsMigrations.UpdateFrequencyCarryOverPath(_dataPath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonConvert.SerializeObject(new Dictionary<string, int>
        {
            [SettingsMigrations.AnidbNotificationFrequency] = 6,
            [SettingsMigrations.AnidbFileFrequency] = 24,
            [SettingsMigrations.AnidbCalendarFrequency] = 12,
            [SettingsMigrations.PluginUpdatesFrequency] = 0,
        }));
        using var service = Service();

        var byId = service.GetScheduledActions().ToDictionary(info => info.ID);

        Assert.Equal([ActionTrigger.Every(TimeSpan.FromHours(6))], byId[notifications].Triggers);
        Assert.True(byId[notifications].HasCustomTriggers);
        Assert.False(byId[files].HasCustomTriggers);
        Assert.Empty(byId[calendar].Triggers);
        Assert.Empty(byId[plugins].Triggers);
        Assert.True(byId[plugins].HasCustomTriggers);
        Assert.False(File.Exists(path));
    }

    [Fact]
    public async Task AnUnreadableCarryOver_IsMovedAside_AndTheDefaultsStay()
    {
        var notifications = _source.Add([ActionTrigger.Every(TimeSpan.FromHours(24))], typeof(GetAnidbNotificationsAction));
        var path = SettingsMigrations.UpdateFrequencyCarryOverPath(_dataPath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "{ \"truncated\": ");
        using var service = Service();

        await service.StartSchedulingAsync(TestContext.Current.CancellationToken);

        var info = service.GetScheduledAction(notifications)!;
        Assert.False(info.HasCustomTriggers);
        Assert.Equal([ActionTrigger.Every(TimeSpan.FromHours(24))], info.Triggers);
        Assert.False(File.Exists(path));
        Assert.True(File.Exists(path + ".unreadable"));
    }

    [Fact]
    public void APortedJobsLastRun_IsTakenFromItsOldRow()
    {
        var notifications = _source.Add([ActionTrigger.Every(TimeSpan.FromHours(6))], typeof(GetAnidbNotificationsAction));
        // The job wrote it in local time: 10:00 in summer is 08:00 UTC.
        _scheduledUpdates.Setup(repository => repository.GetByUpdateType((int)ScheduledUpdateType.AniDBNotify))
            .Returns(new ScheduledUpdate { UpdateType = (int)ScheduledUpdateType.AniDBNotify, LastUpdate = new DateTime(2026, 9, 28, 10, 0, 0), UpdateDetails = string.Empty });
        using var service = Service();

        var info = service.GetScheduledAction(notifications)!;

        Assert.Equal(new DateTime(2026, 9, 28, 8, 0, 0, DateTimeKind.Utc), info.LastRunAt);
        Assert.Equal(new DateTime(2026, 9, 28, 8, 0, 0, DateTimeKind.Utc), info.LastScheduledRunAt);
        Assert.Equal(new DateTime(2026, 9, 28, 14, 0, 0, DateTimeKind.Utc), info.NextRunAt);
    }

    #endregion
}
