# Airing Schedule Providers

This folder defines how Shoko tracks when episodes air: broadcasts, streaming
releases, delays and simulcasts. Any plugin can register
`IAiringScheduleProvider` implementations to contribute schedules; the core
ships none of its own.

It tracks *when* an episode airs, per source, channel, kind and language. It
is not episode metadata (titles and dates without a time live on `IEpisode`),
not matching (`IReleaseInfoProvider` decides what a file contains), not watch
history, and estimates never feed anything that needs to be right.

## The model

```
Channel ──< Schedule >── Episode Airing
 (where)     (one run,     (one episode,
              one track     one time)
              set)
```

| Term | Meaning |
|---|---|
| **Provider** | A source of schedules registered by a plugin, identified by a stable `Guid`. |
| **Channel** | Where something airs, from a registry every provider shares: "TOKYO MX" (`Television`), "Crunchyroll" (`Streaming`). Regional services carry the region, e.g. `Amazon (US)`. |
| **Schedule** | One provider's run of a series, optionally narrowed to a season, on one channel (or none), releasing a fixed set of tracks. Owned by the provider that created it. |
| **Track** | A `Kind` (`Original`, `Subtitled`, `Dubbed`) plus a language code and an optional country code. A language released at another time is another schedule. |
| **Episode airing** | One episode on one schedule: a time, the slot it was first scheduled for, a delay flag, a kind (`Normal`, `Advance` or `Rerun`) and an optional link. |
| **Coverage** | Which episodes a schedule is for: an optional range, and whether the run is finished. Estimates never go past it. |
| **Link** | Ties airings on one schedule into one unit: a double episode, or a season released at once. |
| **Estimate** | An airing computed from a schedule's own line for an episode with no reported time. Never stored. |

Delays, hiatuses and learned time slots belong to one schedule's line, never
to the series: a series on three channels has three independent schedules.

---

## Registering a provider

```csharp
public class MyAiringScheduleProvider(MyClient client) : IAiringScheduleProvider
{
    public string Name => "MyProvider";
    public string Description => "Broadcast times from MyProvider's public schedule.";

    // Fixed for the provider's lifetime; a track of any other kind throws.
    public IReadOnlySet<AiringKind> AvailableKinds { get; } = new HashSet<AiringKind> { AiringKind.Original };

    public async Task<bool> RefreshAsync(ISeries series, CancellationToken cancellationToken = default)
    {
        // Any series may arrive; answer false for one you cannot key on.
        if (GetMyProviderID(series) is not { } id)
            return false;

        await client.RefreshAsync(id, cancellationToken);
        return true;
    }
}
```

Providers are found by reflection, constructed with constructor injection and
held for the life of the process, so most need no DI registration (see
[contracts the server discovers for you](../../README.md#contracts-the-server-discovers-for-you)).
Every write is checked **by reference** against that instance, so if a job or
controller of yours writes, register the **concrete** type as a singleton
(`services.AddSingleton<MyAiringScheduleProvider>()`). A transient
registration or one under the interface hands out a second instance whose
every write throws `ArgumentException`. Inside the provider, pass `this`.

- **`RefreshAsync(ISeries)`** is the only required member. The `ISeason` and
  `IEpisode` overloads default to calling it with the entity's series. It
  answers one entity on demand; walking a whole source is a
  [sweep](#core-driven-sweeps-isweepingairingscheduleprovider).
- **Configuration**: implement `IAiringScheduleProvider<TConfiguration>` to
  get a settings page, as for a release provider.
- **`MaxConcurrentRefreshes`** caps your parallel refreshes (default one).
- **Icon**: name it with `EmbeddedIconResourceName`, the way a plugin names
  its own: an absolute resource name in your assembly, SVG preferred, PNG
  accepted. The core extracts it beside your plugin as
  `<type>.airing-icon.<ext>` (`<dll>.<type>.airing-icon.<ext>` beside a lone
  dll), named after your provider's type, and uses a file already there by
  that name instead. Without either, your plugin's icon stands in. It is kept
  on `AiringScheduleProviderInfo.Icon` and served at
  `GET /api/v3/AiringSchedule/Provider/{providerID}/Icon`.

Two values on `AiringScheduleProviderInfo` (`GetProviderInfo(this)`) belong
to the user:

- **`EnabledKinds`**: a new provider starts with **none** enabled, so it is
  listed but contributes nothing until a user switches its kinds on. A track
  of a disabled kind is stored but hidden. Check your `EnabledKinds` before
  fetching a kind, and listen for `ProvidersUpdated`.
- **`Priority`** is source order, not quality: it breaks a tie when two
  providers report the same channel for the same episode, and never hides
  another provider's airings. What a viewer wants to watch is the channel and
  track preference (`AiringTrackPreference`, the `Preferred…` filter options).

---

## Writing: `FindOrRegisterChannel` → `AddOrUpdateSchedule` → `SetAirings` → `LinkAirings`

Every write takes **your provider instance** and is checked against the
schedule's stored owner; another provider's schedule or an unregistered
instance throws `ArgumentException`. This guards against mistakes, not
hostile code.

1. **`FindOrRegisterChannel(name, type)`** gets or creates a channel by its
   normalised name. Never invent a channel `Guid`.
2. **`AddOrUpdateSchedule(provider, data)`** creates or updates the run; its
   identity is `(provider, series, season, key)`.
3. **`SetAirings(provider, schedule, airings)`** replaces the schedule's whole
   line and runs delay inference over it.
4. **`LinkAirings(provider, airings)`** ties two or more existing airings into
   one slot.

```csharp
public async Task<bool> RefreshAsync(ISeries series, CancellationToken cancellationToken = default)
{
    if (await client.LookupAsync(series, cancellationToken) is not { } listing)
        return false;

    var channel = airingScheduleService.FindOrRegisterChannel(listing.ChannelName, AiringChannelType.Television);

    // The provider's own channel ID is a stable key, so a later run finds this schedule.
    var schedule = airingScheduleService.AddOrUpdateSchedule(this, new AiringScheduleData
    {
        Series = series,
        ChannelID = channel.ChannelID,
        Key = listing.ChannelId,
        Tracks = [new AiringTrackData(AiringKind.Original, "ja")],
        IsFinished = listing.HasEnded,
    });

    // OriginalAiredAt and IsDelayed left null: the service infers them from the whole line.
    var airings = listing.Episodes
        .Select(entry => new EpisodeAiringData
        {
            Episode = entry.Episode,
            AiredAt = entry.AiredAtUtc,
            Key = entry.Episode.ID.ID,
        })
        .ToList();
    var written = airingScheduleService.SetAirings(this, schedule, airings);

    foreach (var doubleBill in listing.DoubleBills)
    {
        var members = written.Where(a => doubleBill.EpisodeIDs.Any(id => id.ToString() == a.EpisodeID.ID)).ToList();
        if (members.Count >= 2)
            airingScheduleService.LinkAirings(this, members);
    }

    return true;
}
```

A later refresh reporting episode 5 two hours late, with everything after it
shifted, needs no extra work: `SetAirings` matches by key and works out that
episode 5 is the delay and the rest moved behind it.

A whole-season drop has no cadence: give every airing the same time, pass
`new EpisodeAiringUpdateOptions { InferDelays = false }`, and link the whole
season so a client renders one card. `OffsetFromOriginal` then reads how far
the release landed from each episode's earliest `Original` airing.

### Writing part of a run: `MergeAirings`

`SetAirings` reads a submission as the schedule's entire line. A source that
only publishes a window, such as the coming fortnight, writes with
`MergeAirings(provider, schedule, airings, removals, options)`: it adds or
updates what you pass, takes away what you name in `removals`, leaves the rest
alone, and still runs the full inference over the stored line.

| | `SetAirings` | `MergeAirings` |
|---|---|---|
| An airing you pass | added or updated | added or updated |
| An airing you leave out | **removed, as a hiatus** | **untouched** |
| An airing you name in `removals` | n/a | **deleted** |

Silence means opposite things in the two calls, which is why they are separate
methods; pick by the default you want.

- **Explicit removal deletes; absence is a hiatus.** An airing left out of
  `SetAirings` whose slot is still ahead is kept without a slot (the lost one
  on `OriginalAiredAt`); one in the past or outside the coverage is deleted.
  A windowed source whose removals mean "no longer listed" rather than "a
  mistake" passes `KeepRemovalsAsHiatus = true` to judge them the same way.
- **Coverage is not guessed from a delta.** `MergeAirings` never writes
  `FirstEpisodeNumber`, `LastEpisodeNumber` or `IsFinished`; only
  `AddOrUpdateSchedule` and `UpdateSchedule` do. State what this write knows on
  `EpisodeAiringUpdateOptions`, which judges its removals and nothing else.

Everything else (ownership, validation, retention, links and the event) is
shared with `SetAirings`. Naming an airing in both lists is rejected.

### What a write raises

Every write raises `AiringsUpdated` once, after the schedule is whole again,
with `Added`, `Updated` and `Withdrawn` (taken off the line, whether deleted or
kept as a hiatus; reading the schedule back tells them apart), `Airings` as
the three together, and a coarse `Reason`. An airing written back unchanged is
in none of them. A write that changed nothing is still raised, with empty
lists and `UpdateReason.None`.

`AiringsUpdated`, `ScheduleUpdated`, `ChannelRegistered` and `SweepCompleted`
are raised synchronously on the writer's thread after the rows are saved: a
slow handler holds up the writer, and one that throws makes a stored write
look failed. Keep handlers short, catch inside them, and queue heavy work.

---

## Core-driven sweeps: `ISweepingAiringScheduleProvider`

Implement [`ISweepingAiringScheduleProvider`](ISweepingAiringScheduleProvider.cs)
beside `IAiringScheduleProvider` and the server walks your whole source for
you, calling the instance it holds (nothing to register):

```csharp
public class MyAiringScheduleProvider(IAiringScheduleService airingScheduleService, MyClient client)
    : IAiringScheduleProvider, ISweepingAiringScheduleProvider
{
    public string Name => "MyProvider";

    public IReadOnlySet<AiringKind> AvailableKinds { get; } = new HashSet<AiringKind> { AiringKind.Original };

    public TimeSpan? SuggestedSweepInterval => TimeSpan.FromHours(12);

    public async Task<string?> SweepAsync(string? cursor, CancellationToken cancellationToken)
    {
        var week = cursor is null ? client.CurrentWeek : DateOnly.Parse(cursor);
        while (!cancellationToken.IsCancellationRequested)
        {
            if (await client.GetWeekAsync(week, cancellationToken) is not { } listing)
                return null;          // the sweep is over

            await WriteWeekAsync(listing, cancellationToken);
            week = week.AddDays(7);

            if (week > client.LastPublishedWeek)
                return null;
        }

        return week.ToString("O");    // out of budget: resume here
    }

    public Task<bool> RefreshAsync(ISeries series, CancellationToken cancellationToken = default)
        => RefreshOneAsync(series, cancellationToken);
}
```

The unit of work is yours (IDs, weeks, pages); the pacing is the server's:

- **Whether.** A provider with no enabled kinds is never swept, checked again
  when its chunk runs.
- **When.** The interval used is `AiringScheduleProviderInfo.SweepInterval`,
  which the user owns; `SuggestedSweepInterval` seeds it, the default is a
  day, and anything under fifteen minutes is clamped.
- **How long.** Each chunk gets the server's budget, a minute by default
  (one second to ten minutes), since it holds a shared queue worker.
- **Where from.** The cursor.

The token handed to `SweepAsync` fires when the budget runs out or the worker
shuts down: watch it and return a cursor before it fires. A chunk that runs
out first is recorded as `TimedOut`, and the next resumes from the last cursor
you returned, so the work since is lost. A chunk still running well past its
budget is reported as a possible deadlock, and holds shutdown as long.

| Outcome | What happened |
|---|---|
| `Completed` | `SweepAsync` returned; its cursor says whether the sweep carries on. |
| `TimedOut` | The budget ran out first. |
| `Stopped` | The server or queue is stopping. Not held against the provider. |
| `Cancelled` | The provider cancelled the chunk itself. |
| `Failed` | The provider threw. Other providers are unaffected. |

Telling the three cancellations apart is best effort.

### The cursor

The server stores the returned `string?` without reading it: `null` finishes
the sweep until the interval has passed, anything else queues the next chunk
at once. It must fit in **512 characters**; a longer one ends the sweep. A
chunk that returns the cursor it was given, or times out without one, made no
progress; after three in a row the sweep waits out the whole interval.

Write from a sweep with [`MergeAirings`](#writing-part-of-a-run-mergeairings),
since a chunk holds part of a line. The server keeps only where each
provider's sweep got to; each chunk raises `SweepCompleted`, bridged to
SignalR as `airing:provider.swept`, and anything wanting a history keeps its
own.

---

## Reacting to an episode airing

`SubscribeToAirings` hands each minute's airings to its subscribers as the
slots pass, from an hour kept in memory, so a plugin need not poll. A
subscription lives while its handle does and must be disposed, so keep it in
a hosted service registered from `RegisterServices`, not on the `IPlugin`
class:

```csharp
public sealed class AiredWatcher : BackgroundService
{
    private readonly IQueueScheduler _queueScheduler;

    private readonly IDisposable _subscription;

    public AiredWatcher(IAiringScheduleService airingScheduleService, IQueueScheduler queueScheduler)
    {
        _queueScheduler = queueScheduler;
        _subscription = airingScheduleService.SubscribeToAirings(OnEpisodesAired, new EpisodeAiringFilteringOptions
        {
            IncludeEstimates = false,
            Kinds = new HashSet<AiringKind> { AiringKind.Original },
        });
    }

    public override void Dispose()
    {
        _subscription.Dispose();
        base.Dispose();
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
        => Task.CompletedTask;

    private void OnEpisodesAired(EpisodeAiredEventArgs e)
    {
        // One call per minute; once per episode here.
        foreach (var group in e.Airings.GroupBy(airing => airing.ShokoEpisode?.LocalID))
        {
            if (group.Key is { } episodeID)
                _queueScheduler.Enqueue<MyJob>(job => job.EpisodeID = episodeID);
        }
    }
}
```

Each subscriber brings its own `EpisodeAiringFilteringOptions` (`null` for
everything), and the lookahead is built from the union of them: estimates are
computed only when someone wants them, and nothing at all while nobody is
subscribed.

- **One call per minute, not per airing.** A simulcast arrives as one list;
  group by `ShokoEpisode` for "aired once" or by `LinkID` for one card per
  slot. The list is never empty.
- **Estimates are predictions** with no retraction. The real airing that
  follows is a different airing with its own ID, so a handler acting on both
  acts twice. Turn them off or check `IsEstimated`.
- **Nothing is replayed** after downtime or before subscribing; read the gap
  with `GetAiringsInRange`, which takes the same options.
- **Handlers run on the ticker's thread.** Enqueue work and return. A handler
  that throws is logged and skipped.

The same dispatch reaches SignalR clients as `airing:episode.aired`; see
`Shoko.Server/API/v3/AiringSchedule.md`.

### Anchoring a read to Shoko entities

`EpisodeAiringFilteringOptions` and `AiringScheduleFilteringOptions` carry an
`EntityAnchor`:

| Value | Meaning |
|---|---|
| `Auto` (default) | `Shoko` for a read given a Shoko entity, otherwise `Raw`. |
| `Raw` | The provider's own entities, as stored. |
| `Shoko` | Only what resolves to a Shoko entity. |

The linked-entity options (`LinkedEntityAirings`, `LinkedEntitySchedules`)
decide what a read finds, and the anchor decides what survives, so `Auto`
never changes what an existing caller gets.

### Querying airings

Every airing read takes one `EpisodeAiringFilteringOptions`. A new options
object filters nothing beyond the disabled providers. Besides the schedule
filters (`ProviderIDs`, `Kinds`, `Languages`, `ChannelIDs`) it carries:

| Option | Meaning |
|---|---|
| `EpisodeTypes` | Only airings of episodes of these types. |
| `InCollection` | An `InclusionFilter` on whether the series has a shoko series. `Only` keeps the collection, `False` keeps what is not in it. |
| `IncludeMissing` | An `InclusionFilter` on series in the collection with no local files. |
| `IncludeRestricted` | An `InclusionFilter` on restricted (H) series. |
| `User` | Leaves out the series the user may not see. |
| `IncludeDateOnly` | Adds a date-only entry for each AniDB episode with an air date and no airing at all. |
| `NextOnly`, `NextPer` | Keeps the next airing per series, channel and/or kind. |

`GetAiringsInRange` takes a `DateTimeOffset` range, start inclusive and end
exclusive, compared as instants. A date-only entry (`IsDateOnly`, with
`AirDate` set and no schedule, provider, channel or time) is in the range when
its date falls between the calendar dates of the two ends, each read in its own
offset, so a caller in Tokyo asking for its own day gets that day's entries.

A next-only read keeps, per group, the earliest episode at or after the
range's start (or now, for an entity read) and answers with that episode's
best airing by preference.

---

## Rules a provider must respect

- **Submit whole lines through `SetAirings`**, since inference compares a
  schedule's entire old and new line; a source that sees part of a run uses
  `MergeAirings`. There is no single-airing write: a one-off edit is a
  `MergeAirings` naming one airing.
- **Leave `IsDelayed` and `OriginalAiredAt` null** unless your source reports
  delays itself; then pass `InferDelays = false` and set both on every airing.
  With inference off, an airing left out of `SetAirings` is deleted.
- **Mark advance screenings and reruns** with `EpisodeAiringData.Kind`. They
  are stored and read like any other airing but left out of everything the
  service learns from the line, and need a `Key` of their own when the
  regular showing is on the same schedule.
- **Name regional channels with `GetRegionalChannelName`**
  (`GetRegionalChannelName("Amazon", "US")` → `"Amazon (US)"`), never by hand,
  and never add a region to a broadcast station.
- **Respect retention.** While automatic cleanup is on, a write that would
  leave the schedule with no airing inside the window is rejected (under
  `#schedule`). Backfilling a long-running show is fine; submitting a run that
  ended years ago on its own is not.
- **A schedule's series, season, key and channel never change**, nor an
  airing's schedule, key and episode.
- **Pass a stable `Key`.** A keyless schedule's key is derived from its channel
  and tracks, so adding a language creates a new schedule.

### Which mistakes throw

Every member documents its exceptions; the shape of the contract:

| Members | Exception | When |
|---|---|---|
| All of them | `ArgumentNullException` | A required argument is `null`. |
| All but the channel and time-zone reads | `InvalidOperationException` | Startup has not handed the service its providers yet. |
| Every change, and `GetProviderInfo(provider)` | `ArgumentException` | The provider is not the registered instance, or does not own what it was handed. |
| `FindOrRegisterChannel` | `ArgumentException` | The name is blank once normalised. |
| `AddChannelAliases` | `ChannelAliasConflictException` | An alias already names another channel of the same type. |
| `AddOrUpdateSchedule`, `UpdateSchedule` | `ArgumentException` | No tracks, an undeclared kind, an unregistered or changed channel, or a backwards coverage range. |
| Anything taking a time zone | `TimeZoneNotFoundException` | The zone is neither an IANA id nor a fixed offset. |
| `SetAirings`, `MergeAirings` | `AiringScheduleValidationException` | An episode outside the schedule, a duplicate key, an airing both submitted and removed, or the retention rule. A `GenericValidationException` keyed by airing key, reporting every rejection at once. |
| `MergeAirings` | `ArgumentException` | A removal is an estimate, unknown, or on another schedule. |
| `LinkAirings`, `UnlinkAiring` | `ArgumentException` | Fewer than two airings, or more than one schedule. |
| `RefreshAsync` (the awaitable overloads) | `OperationCanceledException` | The token was cancelled; a provider's own exception is reported in the result. |

---

## Entities

Schedules and airings key on `ISeries`, `ISeason` and `IEpisode`. The service
finds every entry through `IMetadataService.GetEntry` (a plugin's
`IMetadataResolver` first, then the stores;
[resolving your own kinds](../Providers/README.md#resolving-your-own-kinds)),
and follows links through `IShokoSeries.LinkedSeries`, the `LinkedSeasons`
of a Shoko series' seasons and `IShokoEpisode.LinkedEpisodes`, so a plugin
links its entries in `IMetadataCrossReferenceStore`, not through this
service.
