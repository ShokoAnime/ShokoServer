# Shoko.QueueProcessor

A lightweight, persistent, attribute-driven background job queue for .NET 10 applications. Extracted from [Shoko Server](https://github.com/ShokoAnime/ShokoServer) so plugins and other apps can use the same scheduler the server runs on.

[![NuGet](https://img.shields.io/nuget/v/Shoko.QueueProcessor.svg)](https://www.nuget.org/packages/Shoko.QueueProcessor/)

---

## Why?

Most background job libraries either pull in a heavy runtime (Hangfire, Quartz) or hand you a `Channel<T>` and leave the rest as homework. `Shoko.QueueProcessor` sits in the middle:

- **Persistent** — jobs survive crashes (EF Core / SQLite / MySQL / SQL Server).
- **DI-native** — jobs are resolved from `IServiceProvider`; constructor injection just works.
- **Attribute-driven concurrency** — pools and worker counts come from `[LimitConcurrency]`, `[DisallowConcurrencyGroup]`, etc., not boilerplate.
- **Acquisition filters** — externally-gated jobs (network down, rate-limited, DB not ready) are held out of dispatch without consuming worker threads.
- **Dedup-by-key** — enqueueing the same logical job twice is a no-op.
- **Cheap when idle** — a job enqueued *and* completed inside the flush window never touches the database.

---

## Install

```bash
dotnet add package Shoko.QueueProcessor
```

Target framework: **.NET 10.0**.

---

## How it works

```mermaid
flowchart TD
    subgraph Callers["Entry Points"]
        ENQ["Enqueue&lt;T&gt;(configure)<br/>priority: 0 (normal) or 10 (prioritize: true)<br/>scheduledAt: optional future dispatch"]
        RAC["RunAfterCurrent&lt;T&gt;(configure)<br/>priority: int.MaxValue after parent<br/>held until parent job completes"]
        ENQI["EnqueueImmediate&lt;T&gt;(configure, onComplete)<br/>priority: int.MaxValue<br/>returns Task that completes with the job"]
        REC["RecurringJobRegistry<br/>Register&lt;T&gt;(interval, configure)"]
    end

    subgraph Sched["Scheduler  (QueueScheduler)"]
        KB["JobKeyBuilder&lt;T&gt;<br/>[JobKeyMember] props → stable string key<br/>e.g. Import/HashFileJob_path:&quot;/foo.mkv&quot;"]
        SER["JobDataSerializer<br/>JSON — non-default prop values only"]
    end

    subgraph Orch["QueueOrchestrator  (in-memory state)"]
        IDX["JobKeyIndex  O(1)<br/>key → Id<br/>covers waiting + executing + after-parent<br/>duplicate key → no-op"]
        WQ[("Waiting Queue  SortedSet per pool<br/>sort: Priority DESC · QueuedAt ASC · Id ASC")]
        APC["AfterParentCallbacks<br/>parentId → {jobKey → (ctx, pool)}<br/>key claimed in index to block early dispatch"]
        EXEC[("ExecutingSet<br/>Id → ExecutingEntry<br/>(type, key, startedAt, poolName, priority)")]
        GATE["TryRegisterExecuting<br/>① global worker cap<br/>② per-type concurrency limit<br/>③ per-group concurrency limit"]
    end

    subgraph Persist["Persistence  (PersistenceBuffer + EF Core)"]
        PBUF["PersistenceBuffer<br/>batches INSERT and DELETE ops<br/>enqueue + complete within flush window<br/>→ zero DB writes  (cancel-out)"]
        DB[("QueuedJob table<br/>SQLite / MySQL / MSSQL<br/>─────────────────────<br/>Id · JobType · JobKey<br/>JobDataJson · Priority<br/>QueuedAt · ScheduledAt · RetryCount")]
    end

    subgraph PoolLayer["Worker Pools  (PoolDiscovery builds from [LimitConcurrency] + [DisallowConcurrencyGroup])"]
        PP["Pool priority slot reservation<br/>higher-priority pools reserve RunnableCount slots<br/>lower-priority pools proceed only when available &gt; reserved"]
        PA["Pool A  e.g. AniDB-HTTP<br/>workers = 1 · priority = 10"]
        PB["Pool B  e.g. Import<br/>workers = 4 · priority = 20"]
        PC["Pool Default  catch-all<br/>workers = N · priority = 999"]
    end

    subgraph AcqF["Acquisition Filters  (IAcquisitionFilter)"]
        NF["[NetworkRequired]<br/>blocks while offline"]
        DF["[DatabaseRequired]<br/>blocks until DB ready"]
        CF["Custom filter<br/>e.g. rate-limit, ban cooldown<br/>throw RequeueJobException for<br/>transient conditions"]
        FXS["Excluded-types set<br/>rebuilt on filter StateChanged event<br/>signal pools to retry acquisition"]
    end

    subgraph WrkLoop["Worker  (dedicated OS thread — not ThreadPool)"]
        WAKE["Await wake signal (channel)<br/>or idle poll tick (MaxIdlePollIntervalMs)"]
        SFILT["① Skip if type in excluded-types set"]
        SSCHED["② Skip if ScheduledAt &gt; now"]
        CGATE["③ TryRegisterExecuting<br/>concurrency gate — adds to ExecutingSet"]
        DI["DI scope<br/>Resolve job by concrete type<br/>JobDataSerializer.Apply(instance, json)<br/>Setup(serviceProvider) → PostInit()"]
        TRCK["SubExecutionTracker.CurrentJobId = id<br/>(AsyncLocal — flows through all async calls<br/>JobFactory captures call stack here)"]
        PROC["job.Process()"]
        SUCC["OnComplete<br/>· remove from ExecutingSet<br/>· buffer DELETE in PersistenceBuffer<br/>· release AfterParent children at int.MaxValue<br/>· SignalAllPools"]
        EVTS["QueueStateEventHandler<br/>OnJobExecuting / OnJobCompleted<br/>→ SignalR / UI consumers"]
        REQX["RequeueJobException<br/>→ requeue at original priority<br/>   retry count unchanged<br/>   AfterParent registrations preserved<br/>   (transient: ban, rate-limit, etc.)"]
        RERR["Real exception<br/>→ RetryPolicy.GetDelay(retryCount)<br/>   delay = BaseDelay × 2^n  (capped)"]
        RBACK["Re-insert into pool sub-queue<br/>ScheduledAt = now + delay<br/>RetryCount += 1<br/>persisted immediately"]
        RDIS["Max retries exhausted<br/>→ discard · LogError<br/>AfterParent children discarded<br/>dedup keys freed"]
    end

    subgraph WatchDog["Deadlock Watchdog  (JobWatchdog — background task)"]
        WPOLL["Poll every 15 s"]
        WCHECK["elapsed &gt; WatchdogTimeoutSeconds (default 90)?<br/>[LongRunning] jobs exempt"]
        WFIRST["First detection<br/>LogError: job type + elapsed<br/>+ IJobFactory.Execute call stack<br/>(from SubExecutionTracker if available)"]
        WHEAT["Subsequent polls while still stuck<br/>LogWarning heartbeat:<br/>job type + key + elapsed"]
    end

    %% ── Enqueue ──────────────────────────────────────────
    ENQ & REC --> KB --> SER --> IDX
    ENQI --> IDX
    IDX -- "new key" --> WQ
    WQ --> PBUF --> DB
    DB -. "load persisted jobs on startup" .-> WQ

    %% ── RunAfterCurrent ──────────────────────────────────
    RAC --> KB
    IDX -- "no parent context → enqueue priority 10" --> WQ
    IDX -- "parent executing → hold in APC" --> APC
    APC -- "same key already waiting → pull from pool" --> WQ
    APC -- "same key executing → no-op" --> EXEC

    %% ── EnqueueImmediate ─────────────────────────────────
    ENQI -- "already waiting → promote to int.MaxValue" --> WQ
    ENQI -- "already executing → await existing run" --> EXEC

    %% ── Routing to pools ─────────────────────────────────
    WQ --> PP --> PA & PB & PC
    NF & DF & CF --> FXS

    %% ── Worker acquisition ───────────────────────────────
    PA & PB & PC --> WAKE --> SFILT
    FXS --> SFILT --> SSCHED --> CGATE
    CGATE -- "approved" --> EXEC
    CGATE --> DI --> TRCK --> PROC

    %% ── Outcomes ─────────────────────────────────────────
    PROC -- "success" --> SUCC --> EVTS
    PROC -- "RequeueJobException" --> REQX --> WQ
    PROC -- "real exception" --> RERR
    RERR -- "retries remaining" --> RBACK --> WQ
    RERR -- "max retries" --> RDIS
    SUCC -- "release after-parent children" --> WQ

    %% ── Watchdog ─────────────────────────────────────────
    EXEC --> WPOLL --> WCHECK
    WCHECK -- "exempt or under threshold" --> WCHECK
    WCHECK -- "first detection" --> WFIRST
    WCHECK -- "already flagged" --> WHEAT
```

**The short version:**

1. You call `scheduler.Enqueue<MyJob>(j => j.FileId = 42)`.
2. A **stable key** is built from `[JobKeyMember]` properties (or all primitive props). If a job with that key is already waiting or executing, the call is a no-op.
3. The job is added to an in-memory waiting queue and queued for persistence via `PersistenceBuffer`. If it finishes within `FlushIntervalMs` (default 3s) it never touches the DB at all.
4. The orchestrator routes the job to the **worker pool** that handles its type. Pools are built at startup by `PoolDiscovery` from your `[LimitConcurrency]` / `[DisallowConcurrencyGroup]` attributes.
5. A worker calls **`IAcquisitionFilter.GetTypesToExclude()`** before picking a job. Network down? `NetworkRequired` jobs sit blocked without consuming the worker.
6. The worker resolves the job from DI (so your constructor injection works), applies the serialised property values, runs `Setup` → `PostInit` → `Process`.
7. On success, the row is deleted (batched). On exception, the `RetryPolicy` schedules a retry with exponential backoff — unless it's a `RequeueJobException`, in which case it goes back to the queue without incrementing the retry count (perfect for "AniDB banned, try again later").

---

## Wiring it into your app

In your host's `ConfigureServices`:

```csharp
using Shoko.QueueProcessor;

services.AddQueueProcessor(opts =>
{
    opts.Provider         = DatabaseProvider.SQLite;
    opts.ConnectionString = "Data Source=queue.db";

    // Concurrency
    opts.MaxTotalWorkers      = Environment.ProcessorCount + 4;
    opts.DefaultPoolMaxWorkers = 8;

    // Retry backoff (per-type override available via [RetryPolicy])
    opts.RetryMaxAttempts       = 8;
    opts.RetryBaseDelaySeconds  = 30;
    opts.RetryMaxDelaySeconds   = 3600;

    // PersistenceBuffer: short-lived jobs never hit disk
    opts.FlushIntervalMs = 3000;
    opts.MaxFlushBatch   = 500;

    // Runtime concurrency overrides (lowers a type's pool size; cannot exceed [LimitConcurrency])
    opts.LimitedConcurrencyOverrides["HashFileJob"] = 1;
});
```

`AddQueueProcessor` automatically scans the **calling assembly** for `IQueueJob` implementations. To scan additional assemblies (e.g. for plugins loaded out-of-process), pass them as the trailing params:

```csharp
services.AddQueueProcessor(configure: opts => { /* ... */ },
    scanAssemblies: typeof(MyPlugin).Assembly);
```

Or, from a plugin that loads *after* `AddQueueProcessor` has run (but before the host starts):

```csharp
services.AddQueueJobsFromAssembly(typeof(MyPluginJob).Assembly);
```

That's all the setup. `WorkerPoolManager` is registered as an `IHostedService` — when the host starts, it migrates the queue DB, loads persisted jobs, builds pools from attributes, and starts the workers.

---

## Creating a job

Implement `IQueueJob`:

```csharp
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Shoko.QueueProcessor.Abstractions;
using Shoko.QueueProcessor.Acquisition.Attributes;
using Shoko.QueueProcessor.Builder;
using Shoko.QueueProcessor.Concurrency;

[DatabaseRequired]                    // won't run until DB is ready
[NetworkRequired]                     // won't run while offline
[LimitConcurrency(2)]                 // shares a pool with max 2 workers
[JobKeyGroup("Import")]               // namespaces the key for readability
public class HashFileJob : IQueueJob
{
    private readonly IHashService _hashes;
    private ILogger<HashFileJob>? _logger;

    // Job data — settable props are serialised to JobDataJson
    [JobKeyMember("path", 0)]         // participates in the dedup key
    public string FilePath { get; set; } = string.Empty;

    public bool Force { get; set; }

    // Display-only — surfaced by the API / UI / SignalR
    public string TypeName => "Hash File";
    public string Title    => $"Hashing {System.IO.Path.GetFileName(FilePath)}";
    public Dictionary<string, object> Details => new() { ["Path"] = FilePath };

    // Required for the worker to deserialise without invoking your real ctor.
    // Make it protected/private — DI uses the injected ctor below.
    protected HashFileJob() { }

    // Real ctor — injected services land here.
    public HashFileJob(IHashService hashes) => _hashes = hashes;

    public void Setup(IServiceProvider sp) =>
        _logger = sp.GetRequiredService<ILogger<HashFileJob>>();

    public void PostInit() { /* state derived from FilePath, if needed */ }

    public async Task Process()
    {
        _logger?.LogInformation("Hashing {Path}", FilePath);
        await _hashes.HashAsync(FilePath, Force);
    }
}
```

### Key attributes

| Attribute | Purpose |
|---|---|
| `[LimitConcurrency(n, maxAllowed: m)]` | Caps simultaneous workers for this type (or group). `m` is the hard ceiling for runtime overrides. |
| `[DisallowConcurrencyGroup("name")]` | Puts this job into a shared pool with all other types in the same group. |
| `[DisallowConcurrentExecution]` | Shorthand for `[LimitConcurrency(1)]`. |
| `[Acquisition(priority: N)]` | Sets the pool's dispatch priority. Lower `N` = higher priority. Subclass to bundle priority with domain semantics (e.g. `[AniDBHttpRequired]`). Defaults to `AcquisitionAttribute.LowestPriority` (999) if absent. |
| `[RetryPolicy(MaxRetries = …, BaseDelaySeconds = …, MaxDelaySeconds = …)]` | Per-type override of the global retry backoff. |
| `[LongRunning]` | Exempts this job from the deadlock watchdog. Apply to jobs that are expected to run for a long time (e.g. hashing a large file, full-library scans). |
| `[DatabaseRequired]` | Job won't run while the database is unavailable. |
| `[NetworkRequired]` | Job won't run while the network is offline. Subclass to make custom gates (e.g. AniDB rate limit). |
| `[JobKeyGroup("name")]` | Namespaces the dedup key (`Import/HashFileJob_path:"…"`). |
| `[JobKeyMember(id, index)]` | Explicit field in the dedup key. Falls back to all primitive props if absent. |

### How dedup keys work

`JobKeyBuilder<T>` builds a string like `Import/HashFileJob_path:"/movies/foo.mkv"`. Two `Enqueue` calls that produce the same key collapse to one. If you have no `[JobKeyMember]` annotations, **all public settable primitive properties** participate, so two jobs with identical inputs naturally dedup.

### How job registration works

You don't manually register job types. `AddQueueProcessor` (and `AddQueueJobsFromAssembly`) reflect over assemblies, find every concrete `IQueueJob`, and:

1. Register it as **transient** in DI under its concrete type.
2. Add it to the shared `QueueJobTypeRegistry` (freezes on first read).
3. Feed the registry into `ConcurrencyRegistry` and `PoolDiscovery`, which build the pools.

The interface is used *only* for discovery — DI never resolves `IEnumerable<IQueueJob>`, so jobs aren't instantiated at startup.

---

## Enqueueing jobs

Inject `IQueueScheduler` (or `QueueHandler` for state queries) and call:

```csharp
// Fire and forget
await scheduler.Enqueue<HashFileJob>(j => j.FilePath = "/movies/foo.mkv");

// Prioritise (runs before priority 0 jobs)
await scheduler.Enqueue<HashFileJob>(j => j.FilePath = path, prioritize: true);

// Defer
await scheduler.Enqueue<CleanupJob>(scheduledAt: DateTimeOffset.UtcNow.AddHours(1));

// Convenience extension (same as Enqueue, named for the legacy Shoko call site)
await scheduler.StartJob<HashFileJob>(j => j.FilePath = path);

// Bulk
await scheduler.EnqueueRange(jobs);

// Remove a waiting job (no-op if already executing or not found)
await scheduler.Remove("Import/HashFileJob_path:\"/movies/foo.mkv\"");
await scheduler.Remove<HashFileJob>(j => j.FilePath = "/movies/foo.mkv"); // key built for you

// Control
await scheduler.Pause();
await scheduler.Resume();
await scheduler.Clear();    // wipes waiting (executing jobs run to completion)

// Inspect
var state = await scheduler.GetState();
Console.WriteLine($"{state.TotalExecuting} running, {state.TotalWaiting} waiting");
```

### Parent-child job chaining

Use `RunAfterCurrent<T>` to register a follow-up job that is held until the currently-executing
job completes, then released at maximum priority. This prevents a successor from running against
partially-committed data written by its parent.

```csharp
// Called from inside a job's Process() — child runs after the parent finishes
await scheduler.RunAfterCurrent<IndexAnimeJob>(j => j.AnimeID = animeID);
```

**Behaviour:**

| Scenario | Result |
|---|---|
| Called inside a job | Child is held; released at `int.MaxValue` priority when parent completes |
| Same key enqueued twice for same parent | Deduplicated — only one child runs |
| Child key already waiting in queue | Pulled from queue and held; re-inserted at max priority on parent completion |
| Child key already executing | No-op — the running instance is left alone |
| Parent fails (real failure / discard) | Child registrations discarded; dedup keys freed |
| Parent re-queues (`RequeueJobException`) | Child registrations preserved and fire on eventual success |
| Called outside a job context | Falls back to `Enqueue` at priority 10 |

`SubExecutionTracker` (internal) propagates the current job's ID via `AsyncLocal<Guid>` through
the full async call chain, so `RunAfterCurrent` works correctly when called from a helper
service method invoked by the job — not just from `Process()` directly.

### Recurring jobs

Resolve `RecurringJobRegistry` from DI:

```csharp
public class MyPluginStartup
{
    public MyPluginStartup(RecurringJobRegistry recurring)
    {
        recurring.Register<ImportSweepJob>(
            interval: TimeSpan.FromHours(1),
            configure: j => j.IncludeHidden = false,
            runImmediately: true);
    }
}
```

Registration is safe before *or* after `StartAsync` — late registrations arm immediately. If your job needs the DB, add `[DatabaseRequired]` and it'll naturally wait until the DB acquisition filter releases it.

---

## Acquisition filters

A filter says "while my condition holds, don't dispatch these job types." A worker that picks up a job whose type is excluded simply skips it — the slot is free for the next eligible job.

Bundled filters:

- `NetworkRequiredAcquisitionFilter` — gates `[NetworkRequired]` (and subclasses) on `IConnectivityService.NetworkAvailability`.

Implement your own by registering an `IAcquisitionFilter` in DI:

```csharp
public class MyRateLimitFilter : IAcquisitionFilter
{
    private static readonly Type[] _gated = [typeof(ExpensiveApiJob)];
    private readonly IRateLimiter _limiter;

    public MyRateLimitFilter(IRateLimiter limiter)
    {
        _limiter = limiter;
        _limiter.LimitChanged += (_, _) => StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public Type? WatchedAttributeType => null;   // applies to all pools

    public IEnumerable<Type> GetTypesToExclude() =>
        _limiter.IsThrottled ? _gated : [];

    public event EventHandler? StateChanged;
}

services.AddSingleton<IAcquisitionFilter, MyRateLimitFilter>();
```

For transient conditions where retry-with-backoff is the wrong answer (rate limited, banned, briefly offline), throw `RequeueJobException` from `Process()`. The job returns to the waiting queue at its original priority, the retry count is *not* incremented, and the filter will naturally hold it until the condition clears.

---

## Pool priority

By default every pool has equal standing when competing for the global worker slots (`MaxTotalWorkers`). If some job types need to run ahead of others — for example, rate-limited API calls that must not be crowded out by bulk imports — annotate them with `[Acquisition(priority: N)]`:

```csharp
[Acquisition(priority: 10)]          // lower number = higher priority
[LimitConcurrency(1)]
[DisallowConcurrencyGroup("AniDB_HTTP")]
public class GetAniDBAnimeJob : IQueueJob { … }

[Acquisition(priority: 20)]
[LimitConcurrency(4)]
[DisallowConcurrencyGroup("AniDB_UDP")]
public class ProcessFileJob : IQueueJob { … }

// No [Acquisition] → priority 999 (LowestPriority), runs when slots remain
public class HashFileJob : IQueueJob { … }
```

**How slot reservation works:** before a lower-priority worker picks up a job, the orchestrator counts how many runnable jobs exist across all higher-priority pools, capped at each pool's `MaxWorkers`. That sum is the number of slots "reserved" for higher-priority work. The worker proceeds only when `available > reserved`.

Example — 10 `MaxTotalWorkers`, `AniDB_HTTP` (max 1), `AniDB_UDP` (max 4), `Default` (max 10):

| Higher-priority runnable | Reserved | Available | Default workers allowed |
|---|---|---|---|
| HTTP=1, UDP=4 | 5 | 10 | 5 |
| HTTP=0, UDP=2 | 2 | 10 | 8 |
| HTTP=0, UDP=0 | 0 | 10 | 10 |

The pool priority is the **minimum** `WorkerPriority` across all job types in the pool. Types without `[Acquisition]` default to `LowestPriority` (999).

---

## Events

`QueueStateEventHandler` (singleton) exposes `QueueStarted`, `QueuePaused`, `QueueItemsAdded`, and `ExecutingJobsChanged`. Subscribe from your SignalR hub, UI, or telemetry pipeline:

```csharp
public class QueueEventEmitter
{
    public QueueEventEmitter(QueueStateEventHandler events, IHubContext<QueueHub> hub)
    {
        events.ExecutingJobsChanged += async (_, e) =>
            await hub.Clients.All.SendAsync("queue.changed", e);
    }
}
```

For point-in-time inspection without events, use `QueueHandler.GetExecutingJobs()` / `GetJobs(maxCount, offset, excludeBlocked)`.

---

## Configuration reference

All knobs live on `QueueProcessorOptions`:

| Option | Default | What it does |
|---|---|---|
| `Provider` | `SQLite` | EF Core provider: `SQLite`, `MySQL`, `SqlServer`. |
| `ConnectionString` | `"Data Source=queue.db"` | Connection string for the queue DB. |
| `MaxTotalWorkers` | `Environment.ProcessorCount + 4` | Hard ceiling across all pools. |
| `DefaultPoolMaxWorkers` | `Environment.ProcessorCount + 4` | Workers in the catch-all `"Default"` pool. |
| `FlushIntervalMs` | `3000` | Idle flush interval. Jobs that finish within this window skip the DB entirely. |
| `MaxFlushBatch` | `500` | Force-flush threshold for the persistence buffer. |
| `RetryMaxAttempts` | `8` | Global retry limit (override per-type with `[RetryPolicy]`). |
| `RetryBaseDelaySeconds` | `30` | First retry delay. Backoff = `BaseDelay * 2^n`. |
| `RetryMaxDelaySeconds` | `3600` | Cap on the backoff delay. |
| `MaxIdlePollIntervalMs` | `5000` | Worker idle poll cadence for `ScheduledAt` checks. |
| `MetricsWindowSeconds` | `60` | Sliding window for the jobs/sec metric. |
| `MetricsRollingAvgSamples` | `100` | Rolling sample count for per-type execution time. |
| `WatchdogTimeoutSeconds` | `90` | Jobs running longer than this are flagged as possible deadlocks. Jobs with `[LongRunning]` are exempt. |
| `LimitedConcurrencyOverrides` | `{}` | `JobTypeName → maxWorkers`. Lowers a pool's concurrency at runtime (can't exceed `[LimitConcurrency]`'s `MaxAllowedConcurrentJobs`). |

---

## Deadlock watchdog

`JobWatchdog` runs as a background task (started by `WorkerPoolManager`) and polls executing
jobs every 15 seconds. Any job that has been running longer than `WatchdogTimeoutSeconds` (default
90s) and is not decorated with `[LongRunning]` triggers:

1. **One `LogError`** on first detection — includes the job type, elapsed time, and the call
   stack captured at the last `IJobFactory.Execute` entry point (if any). This is the event to
   forward to your error-tracking system for aggregation.
2. **Repeated `LogWarning`** on every subsequent poll — heartbeat that the job is still stuck.

Apply `[LongRunning]` to jobs that are expected to exceed the timeout by design:

```csharp
[LongRunning]
[LimitConcurrency(2)]
public class HashFileJob : IQueueJob { … }
```

The watchdog cannot obtain a managed stack trace of a running thread without suspending it
(unavailable in .NET Core). Instead, `JobFactory.Execute<T>` captures its call stack when
invoked from within a worker job and stores it in `SubExecutionTracker` keyed by the outer job's
ID. If the job is stuck inside an `IJobFactory.Execute` call, the first-detection error log will
include exactly where in the code that call originated.

---

## Persistence model

One table, `QueuedJob`:

| Column | Notes |
|---|---|
| `Id` (Guid) | Unique instance ID, never changes. |
| `JobType` | Assembly-qualified type name (interned in memory). |
| `JobKey` | Unique dedup key (also unique-indexed). |
| `JobDataJson` | Serialised public settable props (Newtonsoft.Json). |
| `Priority` | Higher first; FIFO within priority. |
| `QueuedAt` / `ScheduledAt` | Earliest-dispatch hint for deferred / retried jobs. |
| `RetryCount` | Persisted so crash-restart doesn't reset backoff. |

There's **no status column** — executing state lives only in memory. On crash-restart, in-flight jobs are simply re-dispatched from the row that was never deleted.

Migrations are applied automatically by `WorkerPoolManager.StartAsync` (which calls `serviceProvider.MigrateQueueDatabaseAsync`).

---

## Building & contributing

```bash
dotnet build Shoko.QueueProcessor
```

NuGet publication is wired into the Shoko Server release workflow (`.github/workflows/build-release.yml`, job `queue-nuget`): tagging a release as `Shoko.QueueProcessor-v*` publishes the package automatically.

Issues and PRs are welcome on the main [Shoko Server repository](https://github.com/ShokoAnime/ShokoServer).

---

## License

Same as Shoko Server — see [LICENSE](../LICENSE).
