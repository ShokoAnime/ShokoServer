# Scheduled Actions

A scheduled action is work that runs on its own: "Check Network Availability"
every 30 minutes, "Update AniDB Calendar" when the admin says so, a plugin's
nightly clean-up. The admin sets when each one runs, and can run or cancel one
by hand. Core registers a few dozen, and a plugin can register its own.

This folder is both sides at once:

- `IScheduledAction` is an **extension point**. You implement it.
- `IScheduledActionService` is a **consumption surface**. You inject it to list
  the scheduled actions, set their triggers, and run or cancel them.

A scheduled action is not an [executable action](../../Actions/Services/README.md).
It has no scope, no caller and no permission: it is global, and only an admin
can see, run or change it. A type is one or the other, never both; the registry
rejects a type that implements `IScheduledAction` and `IExecutableAction` at
load time. Work a user should be able to start on an entity is an executable
action; work that should run on a schedule is a scheduled action.

---

## Registering a scheduled action

Implement `IScheduledAction`:

```csharp
public class PurgeMyCacheAction(MyCache cache, ILogger<PurgeMyCacheAction> logger) : IScheduledAction
{
    public string Name => "Purge My Plugin's Cache";

    public string? Description => "Drops every cached response, forcing a refetch.";

    public ActionCategory Category => ActionCategory.PluginInferred;

    public bool RequiresConfirmation => true;

    public string? ConfirmationMessage => "Everything will be refetched on the next sweep.";

    public IReadOnlyList<ActionTrigger> DefaultTriggers => [ActionTrigger.DailyAt(new TimeOnly(4, 30))];

    public async Task Execute(IProgress<decimal> progress, CancellationToken token)
    {
        var keys = cache.Keys.ToList();
        progress.Report(0);
        for (var i = 0; i < keys.Count; i++)
        {
            token.ThrowIfCancellationRequested();
            await cache.RemoveAsync(keys[i], token);
            progress.Report(100m * (i + 1) / keys.Count);
        }

        logger.LogInformation("Cache purged.");
    }
}
```

There is nothing to register in DI. `PluginManager` collects every public,
non-abstract class in your plugin's main assembly assignable to
`IScheduledAction`, registers each one as a **transient** service, and hands the
list to the scheduled action registry during startup. Put the long-lived state
in a singleton service and let the scheduled action be a thin shell over it.

| Member | Default | Notes |
|---|---|---|
| `Name` | none | The display name. |
| `Description` | `null` | |
| `Category` | `Miscellaneous` | The `ActionCategory` it is listed under; `PluginInferred` lists it under your plugin's name. |
| `RequiresConfirmation`, `ConfirmationMessage` | `false`, `null` | A client asks before an admin runs it by hand. |
| `DefaultTriggers` | none | When it runs on its own until the admin says otherwise. See [Triggers](#triggers). |
| `MinimumInterval` | `null`, a minute | See [Minimum interval](#minimum-interval). |
| `ScheduleCountsManualRuns` | `false` | See [Runs by hand](#runs-by-hand). |
| `Validate(token)` | allowed | Return an `ActionValidationResult` to refuse a run. |
| `Execute(progress, token)` | none | The work. |

### IDs change when you rename the class

A scheduled action's ID is a UUIDv5 of the class's **fully-qualified name**, with
the owning plugin's ID as the namespace, derived the same way an executable
action's is. Its stored triggers and last runs are kept under that ID, so a
rename or a namespace move starts it over with its defaults. To get the ID of
one of your own, ask `IScheduledActionService.GetScheduledAction<TAction>()`.

### What fails at startup

The registry throws `InvalidOperationException` while it takes the discovered
scheduled actions, which takes the server's startup down with it, for:

- a type that is also an `IExecutableAction`;
- a `MinimumInterval` that is not whole minutes from a minute to 366 days;
- a default trigger that is not valid, see [Triggers](#triggers), or whose
  interval is under the minimum;
- default daily, weekly or monthly triggers that run closer together than the
  minimum.

### A run

A trigger, or an admin by hand, queues a run. The run's queue job resolves a
fresh instance from its own container, asks `Validate` again, and awaits
`Execute`:

- **Only the run comes from the job's container.** The probe the registry takes
  at startup, and the instance the first `Validate` runs on, come from the root.
  A scoped service in the constructor is therefore resolved from the root for
  those, and an `IDisposable` action is held by the root until shutdown, so
  keep anything to dispose on a singleton.
- **`Validate` is asked twice**, on two instances: before the run is queued, so
  a run by hand is refused at once (a 400 over HTTP), and again in the queue
  right before `Execute`, where a refusal skips the run rather than failing it.
  A queue can be hours deep, so the second answer decides. Keep it cheap, free
  of side effects, and about the present.
- **`progress` is the job's.** Report a percentage from 0 to 100; it is held in
  memory only and shown on the queue job and on `ScheduledActionInfo.Progress`.
  The job shows none until the first report, so report 0 as soon as you know
  you will report.
- **`token` is the job's.** It is cancelled when an admin cancels the run, and
  when the worker pool stops. Stop by throwing an `OperationCanceledException`:
  after a cancel the run ends as cancelled and is not retried, and after a
  shutdown it is queued again. One that returns anyway completed.
- **One run at a time.** A run is keyed by the scheduled action's ID alone, so
  a run still waiting or running is not queued twice.
- **No parameters.** A scheduled action has no settable properties, and every
  run does the same work, whoever queued it. A run with options is an
  executable action's job, see [Actions](../../Actions/Services/README.md).

An exception out of `Execute` is recorded as a failure of the queue job.

---

## Triggers

Declare when a scheduled action runs on its own with `DefaultTriggers`:

```csharp
public IReadOnlyList<ActionTrigger> DefaultTriggers =>
[
    ActionTrigger.AtStartup,
    ActionTrigger.Every(TimeSpan.FromHours(6)),
    ActionTrigger.DailyAt(new TimeOnly(3, 30)),
    ActionTrigger.WeeklyOn([DayOfWeek.Monday, DayOfWeek.Friday], new TimeOnly(4, 0)),
    ActionTrigger.MonthlyOn([1, -1], new TimeOnly(5, 0)),
    ActionTrigger.OnQueueCleared,
];
```

There are six kinds of trigger, and each sets only its own fields:

| Type | Fields | Runs |
|---|---|---|
| `Interval` | `Interval` | That long after the last run, in whole minutes from a minute to 366 days, with no alignment to the clock. |
| `Daily` | `TimeOfDay` | Every day at the time of day. |
| `Weekly` | `DaysOfWeek`, `TimeOfDay` | On each of the days of the week (at least one), at the time of day. |
| `Monthly` | `DaysOfMonth`, `TimeOfDay` | On each of the days of the month (at least one), at the time of day. |
| `Startup` | none | Every time the server has started. |
| `QueueCleared` | none | Every time the job queue is cleared, see [Queue-cleared triggers](#queue-cleared-triggers). |

A time of day is whole minutes in the server's time zone. A day of the month is
1 to 31 from the start, or -1 to -31 from the end (-1 is the last day); a day a
month does not have is skipped that month. The factories (`Every`, `DailyAt`,
`WeeklyOn`, `MonthlyOn`, `AtStartup`, `OnQueueCleared`) throw on bad input, while
`ActionTrigger.GetValidationError()` returns the reason. `Describe()` writes a
trigger out in English for logs and UIs ("daily 04:00 trigger").

These are the defaults only. The admin can replace them, add to them or clear
them, and a scheduled action with no triggers, the default, only runs by hand.
The core's own recurring work (network checks, maintenance, the AniDB update
checks, plugin update checks) runs this way.

- **The next run counts from the last one.** An interval runs that long after a
  trigger last ran it; a wall-clock trigger runs at its next time after it.
  Several triggers combine, and the earliest wins.
- **Missed runs run once.** A run the server missed while it was down runs once
  at start-up, however many were missed.
- **Triggers fire once the server has started.** A stored trigger that is no
  longer valid is dropped with a warning; with none left, the defaults apply.

### Queue-cleared triggers

`ActionTrigger.OnQueueCleared` runs the action every time the whole job queue
is cleared: by an admin through `POST /api/v3/Queue/Clear`, or by code calling
`IQueueScheduler.Clear`. The clear drops the action's own waiting run with the
rest, and the trigger queues a new one right after it. It does not fire when the
queue runs out of jobs, when a single job is removed, or when the queue is
paused or resumed. A run still running through the clear is left alone, and
the minimum interval holds as for any trigger, with the skip logged at debug
level.

The core's "Check Network Availability" runs this way, besides at start-up and
every 30 minutes, so the jobs that wait on the network see a fresh answer after
a clear.

### Minimum interval

A scheduled action that calls a service which bans clients asking too often
declares how often it may run on its own at most:

```csharp
public TimeSpan? MinimumInterval => TimeSpan.FromHours(6);
```

It defaults to `null`, a minute, and must be whole minutes up to 366 days. The
scheduler holds to it twice:

- **When triggers are set.** An interval trigger under it is refused, and so
  are wall-clock triggers whose times come closer together than it (two daily
  ones an hour apart on a 6 hour action). `SetTriggers` throws an
  `ArgumentException` (a 400 over HTTP); `ActionTrigger.GetValidationError(TimeSpan)`
  gives the reason.
- **Between runs.** A time any trigger fires inside the minimum after the last
  run that counts is skipped, not moved, and logged as a warning ending in
  `Check its triggers.` A run that started late (a busy queue, a missed run
  made up at start-up) holds the next one back until the minimum has passed,
  so runs are delayed, never lost.

The `TimeSpan` extensions in `Shoko.Abstractions.Extensions`
(`ToDurationString()`, `ToTimeAgoString()`) write those messages, and plugins
may use them for their own.

### Runs by hand

A run by hand, through `IScheduledActionService.InvokeAsync`, is never held
back by the minimum. By default it does not count for the schedule: it is kept
as `LastRunAt`, while the triggers count from `LastScheduledRunAt`. A scheduled
action whose service minds how often it is asked, whoever asks, opts in:

```csharp
public bool ScheduleCountsManualRuns => true;
```

Then the triggers count from `LastRunAt`, and a trigger time skipped after a run
by hand is logged at debug level instead. The core's scheduled actions that call
AniDB declare 6 hours, the plugin update check 1 hour, and all of them opt in.

---

## `IScheduledActionService`

Inject it as a DI singleton.

| Member | Notes |
|---|---|
| `GetScheduledActions()` / `GetScheduledAction(Guid)` / `GetScheduledAction<TAction>()` | Every scheduled action, or one, as a `ScheduledActionInfo`: its metadata, triggers in effect and defaults, last and next runs in UTC, and the `State`, `Progress` and `JobKey` of its queue job. |
| `SetTriggers(Guid, triggers)` | Replaces the triggers; an empty list means never. Throws `ArgumentException` for an invalid trigger, an interval under the minimum, or wall-clock triggers closer together than it. |
| `ResetTriggers(Guid)` | Puts the defaults back in effect. |
| `InvokeAsync(Guid, token)` | Queues a run now, not held back by the minimum; does nothing while a run is waiting or running. Returns the refusal, if any. |
| `Cancel(Guid)` | Takes a waiting run out of the queue, or asks a running one to stop. |

Every member throws `InvalidOperationException` before the server has started,
and the ones taking an ID throw `KeyNotFoundException` for an ID no scheduled
action has (the getters return `null` instead). Every member is meant for an
admin: the service does not check who asks.

Over HTTP, for admins only, under `/api/v3/Action/Scheduled`:

| Route | Does |
|---|---|
| `GET /api/v3/Action/Scheduled` | Lists the scheduled actions. |
| `GET …/{actionID}` | One scheduled action. |
| `GET …/{actionID}/Triggers` | Its triggers in effect. |
| `PUT …/{actionID}/Triggers` | Replaces them. |
| `POST …/{actionID}/Triggers` | Adds one. |
| `DELETE …/{actionID}/Triggers` | Puts the defaults back. |
| `DELETE …/{actionID}/Triggers/{index}` | Removes one by its position. |
| `POST …/{actionID}` | Runs it now, without a body; a refusal is a 400. |
| `POST …/{actionID}/Cancel` | Cancels its current run. |

An invalid trigger, one under the minimum, or triggers closer together than it
are a 400 validation problem, and an unknown ID a 404.
