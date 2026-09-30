using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using Shoko.Abstractions.Actions;
using Shoko.Abstractions.Core.Services;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Plugin;
using Shoko.Abstractions.ScheduledActions;
using Shoko.Abstractions.ScheduledActions.Services;
using Shoko.QueueProcessor;
using Shoko.QueueProcessor.Abstractions;
using Shoko.QueueProcessor.Events;
using Shoko.Server.Actions;
using Shoko.Server.Models.Internal;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Repositories.Direct;
using Shoko.Server.Scheduling;
using Shoko.Server.Server;
using Shoko.Server.Settings;
using Shoko.Server.Utilities;

#nullable enable
namespace Shoko.Server.Services;

/// <summary>
/// Runs the scheduled actions on their triggers, through the queue, and keeps
/// their schedule in the database.
/// </summary>
/// <remarks>
/// A firing trigger queues a run unless one is waiting or running. The next run
/// counts from the last counted one (a trigger's, or a manual one when the action
/// counts those), so the schedule survives a restart and a missed run runs once
/// at start-up. Firings inside the minimum interval are skipped and logged once;
/// a busy queue delays runs but loses none. Runs by hand are never held back.
/// </remarks>
public sealed class ScheduledActionService : IScheduledActionService, IHostedService, IDisposable
{
    #region Constants

    /// <summary>
    /// The longest the scheduler sleeps between looks at the clock, so a clock
    /// that is set, or a trigger changed without the timer being re-armed, is
    /// noticed within it.
    /// </summary>
    internal static readonly TimeSpan MaximumSleep = TimeSpan.FromMinutes(1);

    /// <summary>
    /// The shortest the scheduler sleeps, so a timer that fires a hair early
    /// does not spin. A run due within it counts as due, or a timer that fires
    /// early would push every run back by this much, and a daily, weekly or
    /// monthly time within it after a run counts as that run.
    /// </summary>
    private static readonly TimeSpan MinimumSleep = ActionTriggerSchedule.SameRunTolerance;

    /// <summary>
    /// How the triggers are stored.
    /// </summary>
    private static readonly JsonSerializerSettings _triggerSettings = new()
    {
        Converters = [new StringEnumConverter()],
        NullValueHandling = NullValueHandling.Ignore,
    };

    /// <summary>
    /// The field a weekly trigger kept its one day of the week in, before it
    /// took several.
    /// </summary>
    private const string LegacyDayOfWeek = "DayOfWeek";

    /// <summary>
    /// The actions the core ported from its recurring jobs: the kind of row the
    /// job kept its last run in, whose time becomes the action's first last run
    /// so an upgrade does not run them all at once, and the key their update
    /// frequency was carried over under by settings migration 25.
    /// </summary>
    private static readonly (Type ActionType, ScheduledUpdateType? LastRun, string? FrequencyKey)[] _portedActions =
    [
        (typeof(UpdateAnidbCalendarAction), ScheduledUpdateType.AniDBCalendar, SettingsMigrations.AnidbCalendarFrequency),
        (typeof(GetUpdatedAnidbAnimeAction), ScheduledUpdateType.AniDBUpdates, SettingsMigrations.AnidbAnimeFrequency),
        (typeof(CheckAnidbFileUpdatesAction), ScheduledUpdateType.AniDBFileUpdates, SettingsMigrations.AnidbFileFrequency),
        (typeof(GetAnidbNotificationsAction), ScheduledUpdateType.AniDBNotify, SettingsMigrations.AnidbNotificationFrequency),
        (typeof(SyncAnidbMylistOnScheduleAction), null, SettingsMigrations.AnidbMylistFrequency),
        (typeof(CheckPluginUpdatesAction), ScheduledUpdateType.PluginUpdates, SettingsMigrations.PluginUpdatesFrequency),
    ];

    #endregion

    #region Fields

    private readonly ILogger<ScheduledActionService> _logger;

    private readonly IScheduledActionSource _source;

    private readonly ScheduledActionRepository _schedules;

    private readonly ScheduledUpdateRepository _scheduledUpdates;

    private readonly IQueueScheduler _scheduler;

    private readonly QueueHandler _queue;

    private readonly QueueStateEventHandler _queueEvents;

    private readonly ISystemService _systemService;

    private readonly IApplicationPaths _applicationPaths;

    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// Guards the rows, the loaded flag and the clock tracking.
    /// </summary>
    private readonly Lock _lock = new();

    /// <summary>
    /// Keeps the start and the ticks from overlapping.
    /// </summary>
    private readonly SemaphoreSlim _tickLock = new(1, 1);

    /// <summary>
    /// The actions by the key of their run's queue job, filled once the
    /// schedule is loaded.
    /// </summary>
    private IReadOnlyDictionary<string, ScheduledActionDefinition[]> _runKeys = new Dictionary<string, ScheduledActionDefinition[]>();

    /// <summary>
    /// The actions whose stored last run, or first sighting, lies ahead of the
    /// clock: when that was first seen, by the clock and by a monotonic
    /// timestamp. Guarded by the lock.
    /// </summary>
    private readonly Dictionary<Guid, (DateTime SeenAt, long Timestamp)> _clockBehind = [];

    /// <summary>
    /// The stored triggers already reported as unreadable or dropped, by
    /// action, so a tick does not report them again.
    /// </summary>
    private readonly ConcurrentDictionary<Guid, string> _reportedTriggers = new();

    /// <summary>
    /// How far each action's skipped trigger times were looked at and logged,
    /// in UTC, so a tick does not log them again. Written under the tick lock.
    /// </summary>
    private readonly ConcurrentDictionary<Guid, DateTime> _skipsLoggedUntil = new();

    /// <summary>
    /// When each action's last run that counts was due, in UTC: the trigger
    /// time it ran for, or when it was queued at start-up or by hand, and
    /// whether its queue job has yet to start. What the triggers skip counts
    /// from here, so a job that started late does not cost the next run. Lost
    /// on a restart, when the last run that counts stands in for it.
    /// </summary>
    private readonly ConcurrentDictionary<Guid, (DateTime DueAt, bool Waiting)> _dueAt = new();

    /// <summary>
    /// Whether the waiting run of each action was queued by a trigger, so the
    /// start of its job is recorded as the same kind of run. One waiting from
    /// before a restart takes the kind of the last run recorded, and a job with
    /// none, as one queued by other code, starts a run by hand.
    /// </summary>
    private readonly ConcurrentDictionary<Guid, bool> _waitingByTrigger = new();

    private ITimer? _timer;

    private bool _loaded;

    private bool _scheduling;

    /// <summary>
    /// Whether the start-up pass ran: the start-up triggers and the runs missed
    /// while the server was down. Until it did, each tick tries it again.
    /// </summary>
    private bool _startupPassDone;

    private bool _disposed;

    #endregion

    #region Constructor

    /// <summary>
    /// Creates the scheduler.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="source">The action registry.</param>
    /// <param name="schedules">The schedule rows.</param>
    /// <param name="scheduledUpdates">The rows the ported jobs kept their last run in.</param>
    /// <param name="scheduler">The queue.</param>
    /// <param name="queue">The queue's state.</param>
    /// <param name="queueEvents">Says when a queue job starts.</param>
    /// <param name="systemService">Says when the server has started.</param>
    /// <param name="applicationPaths">Where the settings carry-over is.</param>
    /// <param name="timeProvider">The clock, or <c>null</c> for the system clock.</param>
    public ScheduledActionService(
        ILogger<ScheduledActionService> logger,
        IScheduledActionSource source,
        ScheduledActionRepository schedules,
        ScheduledUpdateRepository scheduledUpdates,
        IQueueScheduler scheduler,
        QueueHandler queue,
        QueueStateEventHandler queueEvents,
        ISystemService systemService,
        IApplicationPaths applicationPaths,
        TimeProvider? timeProvider = null
    )
    {
        _logger = logger;
        _source = source;
        _schedules = schedules;
        _scheduledUpdates = scheduledUpdates;
        _scheduler = scheduler;
        _queue = queue;
        _queueEvents = queueEvents;
        _systemService = systemService;
        _applicationPaths = applicationPaths;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    #endregion

    #region Hosted Service

    /// <inheritdoc/>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _queueEvents.ExecutingJobsChanged += OnExecutingJobsChanged;
        _systemService.Started += OnServerStarted;
        if (_systemService.IsStarted)
            OnServerStarted(this, EventArgs.Empty);

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task StopAsync(CancellationToken cancellationToken)
    {
        _queueEvents.ExecutingJobsChanged -= OnExecutingJobsChanged;
        _systemService.Started -= OnServerStarted;
        lock (_lock)
        {
            _scheduling = false;
            _timer?.Dispose();
            _timer = null;
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _queueEvents.ExecutingJobsChanged -= OnExecutingJobsChanged;
        _systemService.Started -= OnServerStarted;
        _timer?.Dispose();
    }

    private void OnServerStarted(object? sender, EventArgs eventArgs)
        => _ = Task.Run(async () =>
        {
            try
            {
                await StartSchedulingAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "The action scheduler failed to start");
            }
        });

    /// <summary>
    /// Records the start of a run's queue job as the action's last run, of the
    /// kind that queued it, so the minimum interval counts from when it ran,
    /// not from when it was queued. A job no trigger queued, as one queued by
    /// hand or by other code, is a run by hand, and when that counts it is also
    /// when its next run was due.
    /// </summary>
    /// <param name="sender">The queue's events.</param>
    /// <param name="eventArgs">The jobs that started or stopped.</param>
    private void OnExecutingJobsChanged(object? sender, QueueChangedEventArgs eventArgs)
    {
        if (eventArgs.AddedItems.Count is 0)
            return;

        try
        {
            if (!_systemService.IsStarted)
                return;

            EnsureLoaded();
            foreach (var item in eventArgs.AddedItems)
            {
                if (!_runKeys.TryGetValue(item.Key, out var actions))
                    continue;

                foreach (var action in actions)
                {
                    var now = UtcNow;
                    var byTrigger = _waitingByTrigger.TryRemove(action.ID, out var waiting) && waiting;
                    if (byTrigger || action.ScheduleCountsManualRuns)
                        _dueAt.AddOrUpdate(action.ID, (now, false), (_, due) => due.Waiting ? (due.DueAt, false) : (now, false));
                    RecordRun(action.ID, now, byTrigger);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not record the start of a scheduled run");
        }
    }

    #endregion

    #region Scheduling

    private DateTime UtcNow => _timeProvider.GetUtcNow().UtcDateTime;

    /// <summary>
    /// Starts firing the triggers: runs the actions with a start-up trigger,
    /// and those that missed a run while the server was down, once each, then
    /// arms the timer. When the schedule cannot be read yet, the timer is still
    /// armed and each tick tries again, so a passing failure only delays it.
    /// </summary>
    /// <param name="token">Cancels the queuing.</param>
    /// <returns>A task that completes once the start-up runs are queued.</returns>
    /// <exception cref="InvalidOperationException">The server has not started yet.</exception>
    internal async Task StartSchedulingAsync(CancellationToken token = default)
    {
        if (!_systemService.IsStarted)
            throw new InvalidOperationException("The action schedule is available once the server has started.");

        lock (_lock)
        {
            if (_disposed || _scheduling)
                return;

            _scheduling = true;
            // The triggers are the system's: the timer never runs for whoever started the scheduling.
            if (_timer is null)
            {
                using (DetachedFlow.Suppress())
                    _timer = _timeProvider.CreateTimer(_ => _ = TickAsync(), null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
            }

        }

        await _tickLock.WaitAsync(token).ConfigureAwait(false);
        try
        {
            await RunDueAsync(token).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "The action scheduler could not start; trying again in {Sleep}", MaximumSleep);
        }
        finally
        {
            _tickLock.Release();
            Arm();
        }
    }

    /// <summary>
    /// Runs every action whose next run is due, and the start-up pass if it
    /// has not run yet.
    /// </summary>
    /// <param name="token">Cancels the queuing.</param>
    /// <returns>A task that completes once the due runs are queued.</returns>
    internal async Task TickAsync(CancellationToken token = default)
    {
        if (_disposed || !await _tickLock.WaitAsync(0, token).ConfigureAwait(false))
            return;

        try
        {
            if (!_scheduling)
                return;

            await RunDueAsync(token).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "The action scheduler failed to run the actions due");
        }
        finally
        {
            _tickLock.Release();
            Arm();
        }
    }

    /// <summary>
    /// Loads the schedule if needed and queues the runs that are due: on the
    /// first pass also the start-up triggers and the runs missed while the
    /// server was down. Called under the tick lock.
    /// </summary>
    /// <param name="token">Cancels the queuing.</param>
    /// <returns>A task that completes once the due runs are queued.</returns>
    /// <exception cref="InvalidOperationException">The server has not started yet.</exception>
    private async Task RunDueAsync(CancellationToken token)
    {
        EnsureLoaded();
        var now = UtcNow;
        ClampFutureRuns(now);

        var startup = !_startupPassDone;
        var scheduled = new HashSet<Guid>();
        foreach (var (action, triggers, row) in GetSchedules())
        {
            scheduled.Add(action.ID);
            LogSkippedRuns(action, triggers, row, now);
            if (startup && triggers.Any(trigger => trigger.Type is ActionTriggerType.Startup))
            {
                if (GetMinimumEnd(action, row, GetAnchor(action, row)) is { } end && end > now + MinimumSleep)
                {
                    LogSkippedRun(action, ActionTrigger.AtStartup, row, now);
                    continue;
                }

                await FireAsync(action, now, now, true, token).ConfigureAwait(false);
                continue;
            }

            if (GetNextRun(action, triggers, row, out var dueAt) <= now + MinimumSleep)
                await FireAsync(action, now, dueAt ?? now, true, token).ConfigureAwait(false);
        }

        // An action out of the schedule (triggers cleared) starts over from the tick
        // it comes back on, so its untriggered times are not logged as skipped.
        foreach (var actionId in _skipsLoggedUntil.Keys)
        {
            if (!scheduled.Contains(actionId))
                _skipsLoggedUntil.TryRemove(actionId, out _);
        }

        _startupPassDone = true;
    }

    /// <summary>
    /// Logs the times the triggers of an action fired inside its minimum
    /// interval since the last tick, each once, as they were skipped.
    /// </summary>
    /// <param name="action">The action.</param>
    /// <param name="triggers">The triggers in effect.</param>
    /// <param name="row">Its row, or <c>null</c>.</param>
    /// <param name="now">The time now, in UTC.</param>
    private void LogSkippedRuns(ScheduledActionDefinition action, IReadOnlyList<ActionTrigger> triggers, ScheduledAction? row, DateTime now)
    {
        // What passed before the scheduler first looked, as before a restart,
        // is not logged again.
        var until = now + MinimumSleep;
        var from = _skipsLoggedUntil.GetValueOrDefault(action.ID, now);
        if (from >= until)
            return;

        if (GetMinimumEnd(action, row, GetDueAt(action, row)) is { } end)
        {
            foreach (var (at, trigger) in ActionTriggerSchedule.GetSkippedRuns(triggers, GetAnchor(action, row), end, from, until, _timeProvider.LocalTimeZone))
                LogSkippedRun(action, trigger, row, at);
        }

        _skipsLoggedUntil[action.ID] = until;
    }

    /// <summary>
    /// Logs that a trigger of an action was skipped, as it fired inside the
    /// action's minimum interval: as a warning when the triggers ran it too
    /// close together, or at debug level when it was last run by hand.
    /// </summary>
    /// <param name="action">The action.</param>
    /// <param name="trigger">The trigger.</param>
    /// <param name="row">Its row, which holds its last run.</param>
    /// <param name="skippedAt">
    /// When the trigger fired, in UTC, which the last run is counted back
    /// from, so a tick that comes late does not say it ran longer ago than its
    /// minimum.
    /// </param>
    private void LogSkippedRun(ScheduledActionDefinition action, ActionTrigger trigger, ScheduledAction? row, DateTime skippedAt)
    {
        var lastRun = (skippedAt - GetDueAt(action, row)).ToTimeAgoString();
        var minimum = action.MinimumInterval.ToDurationString();
        if (WasLastRunByHand(action, row))
        {
            _logger.LogDebug(
                "Skipped the {Trigger} of \"{ActionName}\": it last ran {LastRun}, within its minimum of {MinimumInterval}. It was last run by hand.",
                trigger.Describe(),
                action.Name,
                lastRun,
                minimum
            );
            return;
        }

        _logger.LogWarning(
            "Skipped the {Trigger} of \"{ActionName}\": it last ran {LastRun}, within its minimum of {MinimumInterval}. Check its triggers.",
            trigger.Describe(),
            action.Name,
            lastRun,
            minimum
        );
    }

    /// <summary>
    /// Queues a run of an action, and records it as the action's last run,
    /// whether the action accepted it or not, so a refusal is not asked again
    /// on every tick.
    /// </summary>
    /// <param name="action">The action.</param>
    /// <param name="now">The time of the run, in UTC.</param>
    /// <param name="dueAt">
    /// When the run was due, in UTC: the trigger time it runs for, or now for
    /// a start-up run or a run by hand.
    /// </param>
    /// <param name="byTrigger">
    /// Whether a trigger queues it, start-up included, rather than a person by
    /// hand.
    /// </param>
    /// <param name="token">Cancels the queuing.</param>
    /// <returns>The reason the action refused, or <c>null</c>.</returns>
    private async Task<ActionValidationResult?> FireAsync(ScheduledActionDefinition action, DateTime now, DateTime dueAt, bool byTrigger, CancellationToken token)
    {
        // Noted before queuing, as a worker may start the job before the
        // queuing returns, and put back when nothing was queued.
        var countsDue = byTrigger || action.ScheduleCountsManualRuns;
        var due = dueAt < now ? dueAt : now;
        var hadDue = _dueAt.TryGetValue(action.ID, out var previousDue);
        var hadWaiting = _waitingByTrigger.TryGetValue(action.ID, out var previousByTrigger);
        if (countsDue)
            _dueAt[action.ID] = (due, true);
        _waitingByTrigger[action.ID] = byTrigger;

        ActionValidationResult? refusal = null;
        var queued = false;
        try
        {
            refusal = await _source.InvokeAsync(action.ID, token).ConfigureAwait(false);
            queued = refusal is null;
            if (refusal is not null)
                _logger.LogInformation("Skipped the scheduled run of \"{ActionName}\" ({ActionId}): {Reason}", action.Name, action.ID, refusal.Reason);
            else
                _logger.LogDebug("Queued the scheduled run of \"{ActionName}\" ({ActionId})", action.Name, action.ID);
        }
        catch (OperationCanceledException)
        {
            RestoreWaiting(action.ID, hadDue, previousDue, hadWaiting, previousByTrigger);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not queue the scheduled run of \"{ActionName}\" ({ActionId})", action.Name, action.ID);
        }

        // A refused or failed run still counts as due, but has no job to wait for.
        if (!queued)
        {
            RestoreWaiting(action.ID, hadDue, previousDue, hadWaiting, previousByTrigger);
            if (countsDue)
                _dueAt[action.ID] = (due, false);
        }

        try
        {
            RecordRun(action.ID, now, byTrigger);
        }
        catch (Exception ex)
        {
            // Asked again on the next tick; the queue keeps a run still waiting from being queued twice.
            _logger.LogError(ex, "Could not record the scheduled run of \"{ActionName}\" ({ActionId})", action.Name, action.ID);
        }

        return refusal;
    }

    /// <summary>
    /// Puts back what was noted about an action's waiting run before a run was
    /// queued, when it was not.
    /// </summary>
    /// <param name="actionId">The action ID.</param>
    /// <param name="hadDue">Whether a due time was noted.</param>
    /// <param name="previousDue">The due time noted, if any.</param>
    /// <param name="hadWaiting">Whether the kind of a waiting run was noted.</param>
    /// <param name="previousByTrigger">The kind noted, if any.</param>
    private void RestoreWaiting(Guid actionId, bool hadDue, (DateTime DueAt, bool Waiting) previousDue, bool hadWaiting, bool previousByTrigger)
    {
        if (hadDue)
            _dueAt[actionId] = previousDue;
        else
            _dueAt.TryRemove(actionId, out _);

        if (hadWaiting)
            _waitingByTrigger[actionId] = previousByTrigger;
        else
            _waitingByTrigger.TryRemove(actionId, out _);
    }

    /// <summary>
    /// Sets the timer to the earliest next run, or skipped trigger time to log,
    /// but no later than <see cref="MaximumSleep"/> from now.
    /// </summary>
    private void Arm()
    {
        try
        {
            var now = UtcNow;
            var sleep = MaximumSleep;
            foreach (var (action, triggers, row) in GetSchedules())
            {
                if (GetNextRun(action, triggers, row) is { } next && next - now < sleep)
                    sleep = next - now;

                if (_skipsLoggedUntil.TryGetValue(action.ID, out var loggedUntil) && GetMinimumEnd(action, row, GetDueAt(action, row)) is { } end)
                {
                    var anchor = GetAnchor(action, row);
                    var skipped = ActionTriggerSchedule.GetSkippedRuns(triggers, anchor, end, loggedUntil, now + sleep, _timeProvider.LocalTimeZone);
                    if (skipped.Count > 0 && skipped[0].At - now < sleep)
                        sleep = skipped[0].At - now;
                }
            }

            if (sleep < MinimumSleep)
                sleep = MinimumSleep;

            lock (_lock)
            {
                if (_scheduling && !_disposed)
                    _timer?.Change(sleep, Timeout.InfiniteTimeSpan);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "The action scheduler could not work out when to wake up next; trying again in {Sleep}", MaximumSleep);
            lock (_lock)
            {
                if (_scheduling && !_disposed)
                    _timer?.Change(MaximumSleep, Timeout.InfiniteTimeSpan);
            }
        }
    }

    #endregion

    #region Schedule Rows

    /// <summary>
    /// Makes sure every scheduled action has its row: a new one
    /// takes its first last run, as a triggered one, from the row its ported
    /// job kept, if any, and the update frequency carried over from the
    /// settings becomes its triggers.
    /// </summary>
    /// <exception cref="InvalidOperationException">The server has not started yet.</exception>
    private void EnsureLoaded()
    {
        if (_loaded)
            return;

        if (!_systemService.IsStarted)
            throw new InvalidOperationException("The action schedule is available once the server has started.");

        lock (_lock)
        {
            if (_loaded)
                return;

            var now = UtcNow;
            var carried = SettingsMigrations.ReadUpdateFrequencyCarryOver(_applicationPaths.DataPath, _logger);
            var ported = _portedActions
                .Select(entry => (Info: _source.GetAction(entry.ActionType), entry.LastRun, entry.FrequencyKey))
                .Where(entry => entry.Info is not null)
                .ToDictionary(entry => entry.Info!.ID);
            foreach (var action in _source.GetActions())
            {
                ported.TryGetValue(action.ID, out var port);
                var row = _schedules.GetByActionID(action.ID);
                var changed = false;
                if (row is null)
                {
                    var legacyLastRun = GetLegacyLastRun(port.LastRun);
                    row = new() { ActionID = action.ID, CreatedAt = now, LastRunAt = legacyLastRun, LastScheduledRunAt = legacyLastRun };
                    changed = true;
                }

                // What an upgrade carried over is the admin's old choice, so it
                // wins over the default, unless the admin already chose anew.
                if (row.Triggers is null && port.FrequencyKey is not null && carried.TryGetValue(port.FrequencyKey, out var hours))
                {
                    IReadOnlyList<ActionTrigger> triggers = hours > 0 ? [ActionTrigger.Every(GetCarriedInterval(action, hours))] : [];
                    if (!triggers.SequenceEqual(action.DefaultTriggers))
                    {
                        row.Triggers = SerializeTriggers(triggers);
                        changed = true;
                    }
                }

                if (changed)
                    _schedules.Save(row);
            }

            // Only once it is saved, so a boot that fails before this keeps it.
            SettingsMigrations.ClearUpdateFrequencyCarryOver(_applicationPaths.DataPath);
            _runKeys = _source.GetActions()
                .GroupBy(action => action.JobKey)
                .ToDictionary(group => group.Key, group => group.ToArray());

            // A run still waiting from before a restart resumes as the kind last recorded:
            // a trigger's when its last run is also its last scheduled one.
            var executing = GetExecuting();
            foreach (var action in _source.GetActions())
            {
                var jobKey = action.JobKey;
                if (!_scheduler.IsQueued(jobKey) || executing.ContainsKey(jobKey))
                    continue;

                var row = _schedules.GetByActionID(action.ID);
                _waitingByTrigger.TryAdd(action.ID, row?.LastRunAt is { } lastRunAt && row.LastScheduledRunAt == lastRunAt);
            }

            _loaded = true;
        }
    }

    /// <summary>
    /// The interval of a carried-over update frequency, raised to the action's
    /// minimum when it is under it, and cut to the longest interval allowed.
    /// </summary>
    /// <param name="action">The action.</param>
    /// <param name="hours">The frequency, in hours, above 0.</param>
    /// <returns>The interval.</returns>
    private TimeSpan GetCarriedInterval(ScheduledActionDefinition action, int hours)
    {
        var interval = TimeSpan.FromHours(hours);
        var minimum = action.MinimumInterval;
        if (interval < minimum)
        {
            _logger.LogInformation(
                "Raised the carried-over update frequency of \"{ActionName}\" ({ActionId}) from {Interval} to its minimum of {MinimumInterval}",
                action.Name,
                action.ID,
                interval,
                minimum
            );
            return minimum;
        }

        return interval > ActionTrigger.MaximumInterval ? ActionTrigger.MaximumInterval : interval;
    }

    /// <summary>
    /// When a ported job last ran, as the row it kept says.
    /// </summary>
    /// <param name="updateType">The kind of row, or <c>null</c> when the job kept none.</param>
    /// <returns>The time in UTC, or <c>null</c>.</returns>
    private DateTime? GetLegacyLastRun(ScheduledUpdateType? updateType)
    {
        if (updateType is not { } type || _scheduledUpdates.GetByUpdateType((int)type) is not { } row)
            return null;

        // The jobs wrote it in the server's local time.
        var local = DateTime.SpecifyKind(row.LastUpdate, DateTimeKind.Unspecified);
        return ActionTriggerSchedule.ToUtc(local, _timeProvider.LocalTimeZone);
    }

    /// <summary>
    /// Moves a last run or first sighting lying in the future back to when it was
    /// first seen, once the clock has stayed behind it longer than the action's
    /// minimum interval (a clock set back for good). A clock only briefly behind,
    /// as at a boot before syncing, leaves it alone.
    /// </summary>
    /// <remarks>
    /// A last run never moves back past the last scheduled one. Failures are
    /// logged, as the next tick tries again.
    /// </remarks>
    /// <param name="now">The time now, in UTC.</param>
    private void ClampFutureRuns(DateTime now)
    {
        try
        {
            lock (_lock)
            {
                foreach (var action in _source.GetActions())
                {
                    var row = _schedules.GetByActionID(action.ID);
                    var lastRunAhead = row?.LastRunAt is { } lastRunAt && DateTime.SpecifyKind(lastRunAt, DateTimeKind.Utc) > now;
                    var scheduledAhead = row?.LastScheduledRunAt is { } scheduledAt && DateTime.SpecifyKind(scheduledAt, DateTimeKind.Utc) > now;
                    var createdAhead = row is not null && DateTime.SpecifyKind(row.CreatedAt, DateTimeKind.Utc) > now;
                    if (row is null || (!lastRunAhead && !scheduledAhead && !createdAhead))
                    {
                        _clockBehind.Remove(action.ID);
                        continue;
                    }

                    // Only a time the schedule counts from is worth a warning.
                    var minimum = action.MinimumInterval;
                    var counted = GetCountedRun(action, row);
                    var countedAhead = counted is { } countedAt ? DateTime.SpecifyKind(countedAt, DateTimeKind.Utc) > now : createdAhead;
                    var level = countedAhead ? LogLevel.Warning : LogLevel.Debug;
                    if (!_clockBehind.TryGetValue(action.ID, out var behind))
                    {
                        _clockBehind[action.ID] = (now, _timeProvider.GetTimestamp());
                        if (countedAhead)
                            _logger.LogWarning(
                                "The last run of \"{ActionName}\" ({ActionId}) is in the future ({LastRunAt:o}); counting from now until the clock catches up, or for good if it has not within {MinimumInterval}",
                                action.Name,
                                action.ID,
                                counted ?? row.CreatedAt,
                                minimum
                            );
                        else
                            _logger.LogDebug(
                                "The last run by hand of \"{ActionName}\" ({ActionId}) is in the future ({LastRunAt:o}); moving it back if the clock has not caught up within {MinimumInterval}",
                                action.Name,
                                action.ID,
                                lastRunAhead ? row.LastRunAt : row.CreatedAt,
                                minimum
                            );
                        continue;
                    }

                    if (_timeProvider.GetElapsedTime(behind.Timestamp) <= minimum)
                        continue;

                    // The last run of any kind is never earlier than the last
                    // scheduled one, which may have come since.
                    var seenAt = behind.SeenAt < now ? behind.SeenAt : now;
                    if (scheduledAhead)
                        row.LastScheduledRunAt = seenAt;
                    if (lastRunAhead)
                        row.LastRunAt = row.LastScheduledRunAt is { } lastScheduledAt && DateTime.SpecifyKind(lastScheduledAt, DateTimeKind.Utc) > seenAt ? lastScheduledAt : seenAt;
                    if (createdAhead)
                        row.CreatedAt = seenAt;
                    _schedules.Save(row);
                    _clockBehind.Remove(action.ID);
                    _logger.Log(level, "The clock stayed behind the last run of \"{ActionName}\" ({ActionId}); moved it back to {SeenAt:o}", action.Name, action.ID, seenAt);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not move the future runs of the scheduled actions back");
        }
    }

    /// <summary>
    /// Records a run of an action as its last run, and as its last scheduled
    /// run when a trigger queued it, each unless a later one is already
    /// recorded.
    /// </summary>
    /// <param name="actionId">The action ID.</param>
    /// <param name="at">When it ran, in UTC.</param>
    /// <param name="byTrigger">Whether a trigger queued it, rather than a person by hand.</param>
    private void RecordRun(Guid actionId, DateTime at, bool byTrigger)
    {
        lock (_lock)
        {
            var row = _schedules.GetByActionID(actionId) ?? new() { ActionID = actionId, CreatedAt = at };
            var changed = false;
            if (row.LastRunAt is not { } lastRunAt || DateTime.SpecifyKind(lastRunAt, DateTimeKind.Utc) < at)
            {
                row.LastRunAt = at;
                changed = true;
            }

            if (byTrigger && (row.LastScheduledRunAt is not { } scheduledAt || DateTime.SpecifyKind(scheduledAt, DateTimeKind.Utc) < at))
            {
                row.LastScheduledRunAt = at;
                changed = true;
            }

            if (changed)
                _schedules.Save(row);
        }
    }

    /// <summary>
    /// Lists the actions with the triggers in effect and their rows.
    /// </summary>
    /// <returns>The schedules.</returns>
    private IEnumerable<(ScheduledActionDefinition Action, IReadOnlyList<ActionTrigger> Triggers, ScheduledAction? Row)> GetSchedules()
    {
        if (!_loaded)
            yield break;

        foreach (var action in _source.GetActions())
        {
            var row = _schedules.GetByActionID(action.ID);
            var triggers = GetTriggers(action, row);
            if (triggers.Count is 0)
                continue;

            yield return (action, triggers, row);
        }
    }

    /// <summary>
    /// When a trigger queues an action next.
    /// </summary>
    /// <param name="action">The action.</param>
    /// <param name="triggers">The triggers in effect.</param>
    /// <param name="row">Its row, or <c>null</c>.</param>
    /// <returns>
    /// The time in UTC, or <c>null</c> when only a start-up trigger, or none,
    /// is set. It may lie in the past, when a run was missed.
    /// </returns>
    private DateTime? GetNextRun(ScheduledActionDefinition action, IReadOnlyList<ActionTrigger> triggers, ScheduledAction? row)
        => GetNextRun(action, triggers, row, out _);

    /// <summary>
    /// When a trigger queues an action next: the earliest time any of its
    /// triggers fires after its last run once its minimum interval has passed
    /// since the run was due, as a time inside it is skipped, not moved to its
    /// end. A run whose job started late holds that time back until the
    /// minimum has passed since the start too.
    /// </summary>
    /// <param name="action">The action.</param>
    /// <param name="triggers">The triggers in effect.</param>
    /// <param name="row">Its row, or <c>null</c>.</param>
    /// <param name="dueAt">
    /// The trigger time the run is for, in UTC, or <c>null</c> as for the
    /// result.
    /// </param>
    /// <returns>
    /// The time in UTC, or <c>null</c> when only a start-up trigger, or none,
    /// is set. It may lie in the past, when a run was missed.
    /// </returns>
    private DateTime? GetNextRun(ScheduledActionDefinition action, IReadOnlyList<ActionTrigger> triggers, ScheduledAction? row, out DateTime? dueAt)
    {
        var anchor = GetAnchor(action, row);
        var timeZone = _timeProvider.LocalTimeZone;
        dueAt = ActionTriggerSchedule.GetNextRun(triggers, anchor, timeZone, GetMinimumEnd(action, row, GetDueAt(action, row)));
        return dueAt is { } due && GetMinimumEnd(action, row, anchor) is { } end && end > due ? end : dueAt;
    }

    /// <summary>
    /// When an action's last run that counts was due: the trigger time it ran
    /// for, or when it was queued at start-up or by hand, as long as that is
    /// known, or else that run.
    /// </summary>
    /// <param name="action">The action.</param>
    /// <param name="row">Its row, or <c>null</c>.</param>
    /// <returns>The time in UTC.</returns>
    private DateTime GetDueAt(ScheduledActionDefinition action, ScheduledAction? row)
    {
        var anchor = GetAnchor(action, row);
        return _dueAt.TryGetValue(action.ID, out var due) && due.DueAt < anchor ? due.DueAt : anchor;
    }

    /// <summary>
    /// When an action's minimum interval after a run ends: that long after the
    /// start of the minute of the run, so a daily, weekly or monthly run whose
    /// job started a few seconds late does not hold back the next one, due
    /// exactly the minimum later. See
    /// <see cref="ActionTriggerSchedule.GetMinimumIntervalEnd"/> for clock
    /// changes.
    /// </summary>
    /// <param name="action">The action.</param>
    /// <param name="row">Its row, or <c>null</c>.</param>
    /// <param name="from">When the run was, or was due, in UTC.</param>
    /// <returns>The time in UTC, on a whole minute, or <c>null</c> when it never ran a run that counts.</returns>
    private DateTime? GetMinimumEnd(ScheduledActionDefinition action, ScheduledAction? row, DateTime from)
        => GetCountedRun(action, row) is null
            ? null
            : ActionTriggerSchedule.GetMinimumIntervalEnd(from, action.MinimumInterval, _timeProvider.LocalTimeZone);

    /// <summary>
    /// The last run of an action its schedule counts: its last run of any
    /// kind when its runs by hand count, or else the last one a trigger queued.
    /// </summary>
    /// <param name="action">The action.</param>
    /// <param name="row">Its row, or <c>null</c>.</param>
    /// <returns>The time as stored, or <c>null</c> when it never ran such a run.</returns>
    private DateTime? GetCountedRun(ScheduledActionDefinition action, ScheduledAction? row)
        => action.ScheduleCountsManualRuns ? row?.LastRunAt : row?.LastScheduledRunAt;

    /// <summary>
    /// Whether the last run of an action that counts was by hand: only for an
    /// action whose runs by hand count, when its last run is later than its
    /// last scheduled one.
    /// </summary>
    /// <param name="action">The action.</param>
    /// <param name="row">Its row, or <c>null</c>.</param>
    /// <returns><c>true</c> when it was last run by hand.</returns>
    private bool WasLastRunByHand(ScheduledActionDefinition action, ScheduledAction? row)
        => row?.LastRunAt is { } lastRunAt &&
            (row.LastScheduledRunAt is not { } scheduledAt || scheduledAt < lastRunAt) &&
            action.ScheduleCountsManualRuns;

    /// <summary>
    /// The triggers in effect for an action: the admin's, or its defaults.
    /// </summary>
    /// <param name="action">The action.</param>
    /// <param name="row">Its row, or <c>null</c>.</param>
    /// <returns>The triggers.</returns>
    private IReadOnlyList<ActionTrigger> GetTriggers(ScheduledActionDefinition action, ScheduledAction? row)
        => GetCustomTriggers(action, row) ?? action.DefaultTriggers;

    /// <summary>
    /// The triggers the admin set for an action, when they are in effect.
    /// </summary>
    /// <param name="action">The action.</param>
    /// <param name="row">Its row, or <c>null</c>.</param>
    /// <returns>The triggers, or <c>null</c> when its defaults are in effect.</returns>
    private IReadOnlyList<ActionTrigger>? GetCustomTriggers(ScheduledActionDefinition action, ScheduledAction? row)
        => row?.Triggers is { } json ? DeserializeTriggers(action, json) : null;

    /// <summary>
    /// What an action's next run counts from: its last run that counts, or
    /// when the scheduler first saw it, but never later than now. One ahead of
    /// the clock counts from when that was first seen.
    /// </summary>
    /// <param name="action">The action.</param>
    /// <param name="row">Its row, or <c>null</c>.</param>
    /// <returns>The time in UTC.</returns>
    private DateTime GetAnchor(ScheduledActionDefinition action, ScheduledAction? row)
    {
        var now = UtcNow;
        var anchor = DateTime.SpecifyKind(GetCountedRun(action, row) ?? row?.CreatedAt ?? now, DateTimeKind.Utc);
        if (anchor <= now)
            return anchor;

        lock (_lock)
            return row is not null && _clockBehind.TryGetValue(row.ActionID, out var behind) && behind.SeenAt < now ? behind.SeenAt : now;
    }

    /// <summary>
    /// Stores triggers as JSON.
    /// </summary>
    /// <param name="triggers">The triggers.</param>
    /// <returns>The JSON.</returns>
    internal static string SerializeTriggers(IReadOnlyList<ActionTrigger> triggers)
        => JsonConvert.SerializeObject(triggers, _triggerSettings);

    /// <summary>
    /// Reads stored triggers as JSON. A weekly trigger stored before weekly
    /// triggers took several days, with a single <c>DayOfWeek</c>, is read as
    /// one on that day alone.
    /// </summary>
    /// <param name="json">The JSON.</param>
    /// <exception cref="JsonException">The JSON cannot be read, or is neither an array nor <c>null</c>.</exception>
    /// <returns>The triggers, <c>null</c> where one is stored as null.</returns>
    internal static List<ActionTrigger?> ReadTriggers(string json)
    {
        using var reader = new JsonTextReader(new StringReader(json)) { DateParseHandling = DateParseHandling.None };
        var token = JToken.ReadFrom(reader);
        if (token.Type is JTokenType.Null)
            return [];

        if (token is not JArray array)
            throw new JsonSerializationException($"Expected a JSON array of triggers, not {token.Type}.");

        foreach (var item in array.OfType<JObject>())
        {
            if (item.Property(LegacyDayOfWeek) is not { } legacy)
                continue;

            legacy.Remove();
            if (item.Property(nameof(ActionTrigger.DaysOfWeek)) is null && legacy.Value.Type is not JTokenType.Null)
                item[nameof(ActionTrigger.DaysOfWeek)] = new JArray(legacy.Value);
        }

        return array.ToObject<List<ActionTrigger?>>(JsonSerializer.Create(_triggerSettings)) ?? [];
    }

    /// <summary>
    /// Reads stored triggers, leaving out any that are no longer valid, such as
    /// an interval that is not whole minutes. Each problem is logged once.
    /// </summary>
    /// <param name="action">The action they are of, for the log.</param>
    /// <param name="json">The JSON.</param>
    /// <returns>
    /// The triggers, or <c>null</c> when the JSON cannot be read or none of the
    /// stored triggers is valid, so the action's defaults apply.
    /// </returns>
    private IReadOnlyList<ActionTrigger>? DeserializeTriggers(ScheduledActionDefinition action, string json)
    {
        var report = !(_reportedTriggers.TryGetValue(action.ID, out var reported) && reported == json);
        List<ActionTrigger?> stored;
        try
        {
            stored = ReadTriggers(json);
        }
        catch (JsonException ex)
        {
            if (report)
            {
                _logger.LogWarning(ex, "Could not read the triggers of \"{ActionName}\" ({ActionId}); using its defaults", action.Name, action.ID);
                _reportedTriggers[action.ID] = json;
            }

            return null;
        }

        var triggers = new List<ActionTrigger>(stored.Count);
        foreach (var trigger in stored)
        {
            if ((trigger is null ? "A trigger is null." : trigger.GetValidationError()) is not { } error)
            {
                triggers.Add(trigger!);
                continue;
            }

            if (report)
                _logger.LogWarning("Dropped a stored trigger of \"{ActionName}\" ({ActionId}), {Trigger}: {Reason}", action.Name, action.ID, JsonConvert.SerializeObject(trigger, _triggerSettings), error);
        }

        if (report)
            _reportedTriggers[action.ID] = json;

        if (stored.Count > 0 && triggers.Count is 0)
        {
            if (report)
                _logger.LogWarning("None of the stored triggers of \"{ActionName}\" ({ActionId}) is valid; using its defaults", action.Name, action.ID);

            return null;
        }

        return triggers;
    }

    #endregion

    #region IScheduledActionService

    /// <inheritdoc/>
    public IReadOnlyList<ScheduledActionInfo> GetScheduledActions()
    {
        EnsureLoaded();
        var executing = GetExecuting();
        return _source.GetActions()
            .Select(action => ToInfo(action, executing))
            .ToList();
    }

    /// <inheritdoc/>
    public ScheduledActionInfo? GetScheduledAction(Guid actionId)
    {
        EnsureLoaded();
        return FindAction(actionId) is { } action ? ToInfo(action, GetExecuting()) : null;
    }

    /// <inheritdoc/>
    public ScheduledActionInfo? GetScheduledAction<TAction>() where TAction : class, IScheduledAction
    {
        EnsureLoaded();
        return _source.GetAction(typeof(TAction)) is { } action ? ToInfo(action, GetExecuting()) : null;
    }

    /// <inheritdoc/>
    public ScheduledActionInfo SetTriggers(Guid actionId, IReadOnlyList<ActionTrigger> triggers)
    {
        ArgumentNullException.ThrowIfNull(triggers);
        return StoreTriggers(actionId, triggers);
    }

    /// <inheritdoc/>
    public ScheduledActionInfo ResetTriggers(Guid actionId)
        => StoreTriggers(actionId, null);

    /// <inheritdoc/>
    public async Task<ActionValidationResult?> InvokeAsync(Guid actionId, CancellationToken token = default)
    {
        EnsureLoaded();
        var action = FindAction(actionId) ?? throw new KeyNotFoundException($"No scheduled action has the ID {actionId}.");
        var now = UtcNow;

        // A run by hand is not held back by the minimum interval, but one still
        // waiting or running is neither queued again nor recorded as a new run.
        if (_scheduler.IsQueued(action.JobKey))
        {
            _logger.LogDebug("Did not queue \"{ActionName}\" ({ActionId}) by hand, as a run is still waiting or running", action.Name, actionId);
            return null;
        }

        var refusal = await FireAsync(action, now, now, false, token).ConfigureAwait(false);
        Arm();
        return refusal;
    }

    /// <inheritdoc/>
    public async Task<ScheduledActionInfo> Cancel(Guid actionId, CancellationToken token = default)
    {
        EnsureLoaded();
        var action = FindAction(actionId) ?? throw new KeyNotFoundException($"No scheduled action has the ID {actionId}.");
        var result = await _scheduler.Cancel(action.JobKey, token).ConfigureAwait(false);
        _logger.LogInformation("Cancelling the run of \"{ActionName}\" ({ActionId}): {Result}", action.Name, actionId, result);
        return ToInfo(action, GetExecuting());
    }

    /// <summary>
    /// Checks and stores the triggers of a scheduled action.
    /// </summary>
    /// <param name="actionId">The scheduled action ID.</param>
    /// <param name="triggers">The triggers, or <c>null</c> for its defaults.</param>
    /// <exception cref="KeyNotFoundException">No scheduled action has the ID.</exception>
    /// <exception cref="ArgumentException">A trigger is invalid, or the triggers run closer together than its minimum.</exception>
    /// <exception cref="InvalidOperationException">The server has not started yet.</exception>
    /// <returns>The scheduled action with its new triggers.</returns>
    private ScheduledActionInfo StoreTriggers(Guid actionId, IReadOnlyList<ActionTrigger>? triggers)
    {
        EnsureLoaded();
        var action = FindAction(actionId) ?? throw new KeyNotFoundException($"No scheduled action has the ID {actionId}.");
        if (triggers is not null)
        {
            for (var index = 0; index < triggers.Count; index++)
            {
                if ((triggers[index] is null ? "A trigger is null." : triggers[index].GetValidationError(action.MinimumInterval)) is { } error)
                    throw new ArgumentException($"Trigger {index}: {error}", nameof(triggers));
            }

            if (ActionTriggerSchedule.GetSpacingError(triggers, action.MinimumInterval) is { } spacingError)
                throw new ArgumentException(spacingError, nameof(triggers));
        }

        lock (_lock)
        {
            var row = _schedules.GetByActionID(actionId) ?? new() { ActionID = actionId, CreatedAt = UtcNow };
            row.Triggers = triggers is null ? null : SerializeTriggers(triggers);
            _schedules.Save(row);
        }

        _logger.LogInformation("Set the triggers of \"{ActionName}\" ({ActionId}) to {Triggers}", action.Name, actionId, triggers is null ? "its defaults" : SerializeTriggers(triggers));
        Arm();
        return ToInfo(action, GetExecuting());
    }

    /// <summary>
    /// Finds a registered scheduled action by its ID.
    /// </summary>
    /// <param name="actionId">The scheduled action ID.</param>
    /// <returns>The scheduled action, or <c>null</c>.</returns>
    private ScheduledActionDefinition? FindAction(Guid actionId)
        => _source.GetActions().FirstOrDefault(action => action.ID == actionId);

    /// <summary>
    /// The running queue jobs by key.
    /// </summary>
    /// <returns>The jobs.</returns>
    private Dictionary<string, QueueItem> GetExecuting()
        => _queue.GetExecutingJobs()
            .DistinctBy(item => item.Key)
            .ToDictionary(item => item.Key);

    /// <summary>
    /// Describes a scheduled action's schedule and the state of its run.
    /// </summary>
    /// <param name="action">The scheduled action.</param>
    /// <param name="executing">The running jobs by key.</param>
    /// <returns>The description.</returns>
    private ScheduledActionInfo ToInfo(ScheduledActionDefinition action, IReadOnlyDictionary<string, QueueItem> executing)
    {
        var row = _schedules.GetByActionID(action.ID);
        var custom = GetCustomTriggers(action, row);
        var triggers = custom ?? action.DefaultTriggers;
        var (state, progress, cancellable) = executing.TryGetValue(action.JobKey, out var item)
            ? (item.CancellationRequested ? ScheduledActionState.CancellationRequested : ScheduledActionState.Running, item.Progress, item.Cancellable)
            : _scheduler.IsQueued(action.JobKey)
                ? (ScheduledActionState.Waiting, (decimal?)null, true)
                : (ScheduledActionState.Idle, (decimal?)null, false);
        return new()
        {
            ID = action.ID,
            Name = action.Name,
            Description = action.Description,
            Category = action.Category,
            CategoryName = action.CategoryName,
            RequiresConfirmation = action.RequiresConfirmation,
            ConfirmationMessage = action.ConfirmationMessage,
            PluginID = action.PluginID,
            Triggers = triggers,
            DefaultTriggers = action.DefaultTriggers,
            MinimumInterval = action.MinimumInterval,
            ScheduleCountsManualRuns = action.ScheduleCountsManualRuns,
            HasCustomTriggers = custom is not null,
            LastRunAt = row?.LastRunAt is { } lastRunAt ? DateTime.SpecifyKind(lastRunAt, DateTimeKind.Utc) : null,
            LastScheduledRunAt = row?.LastScheduledRunAt is { } scheduledAt ? DateTime.SpecifyKind(scheduledAt, DateTimeKind.Utc) : null,
            NextRunAt = GetNextRun(action, triggers, row),
            State = state,
            Progress = progress,
            IsCancellable = cancellable,
            JobKey = action.JobKey,
        };
    }

    #endregion
}
