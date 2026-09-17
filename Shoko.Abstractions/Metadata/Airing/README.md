# Airing Schedule Providers

This folder defines the public API surface for tracking when anime episodes
air: broadcasts, streaming releases, delays and simulcasts. Providers are
pluggable: any plugin can register one or more `IAiringScheduleProvider`
implementations to contribute schedules, on top of AniList's own broadcast
times, which ship in core.

---

## What this is, and isn't

This tracks *when* an episode airs, per source, per channel, per kind and
language, with delays and estimates layered on top. It is not:

- **Episode metadata.** Titles, synopses and air dates without a time live on
  `IEpisode` and its providers, not here.
- **Matching.** An airing schedule never decides what a video file contains.
  `IReleaseInfoProvider` does that, independently.
- **Watch history.** Nothing here tracks what a user has seen.
- **A source of truth for anything but time.** Estimates are guesses, built
  from a schedule's own cadence, and never feed matching or anything else that
  needs to be right.

## The model in one page

```
Channel ──< Schedule >── Episode Airing
 (where)     (one run,     (one episode,
              one track     one time)
              set)
```

| Term | Meaning |
|---|---|
| **Provider** | A source of schedules, AniList or a plugin's own, identified by a stable `Guid`. |
| **Channel** | Where something airs, from a registry shared by every provider: "TOKYO MX" (`Television`), "Crunchyroll" (`Streaming`). Regional services carry the region in the name, e.g. `Amazon (US)`. |
| **Schedule** | One provider's run of a series, optionally narrowed to a season, on one channel (or none), releasing a fixed set of tracks. Owned by the provider that created it. |
| **Track** | What a schedule releases: a `Kind` (`Original`, `Subtitled`, `Dubbed`) plus a language code, an optional country code, and the `TitleLanguage` inferred from them. A language released at a different time is a different schedule, not another track on this one. |
| **Episode airing** | One episode on one schedule: a time, the slot it was first scheduled for, a delay flag, and an optional link to the other airings of the same slot. |
| **Coverage** | Which episodes a schedule is for: an optional episode range, plus whether the provider considers the run finished. Estimates never go past it. |
| **Link** | Ties two or more airings on the same schedule into one unit, for a double-episode slot or a season released at once. |
| **Estimate** | An airing the service computed from a schedule's own line, for an episode nobody has reported a time for yet. Never stored, never submitted by a provider. |

A schedule is the unit everything else hangs off: a delay, a hiatus and a
learned time slot are all properties of one schedule's line of airings, never
of the series as a whole. A series airing on three channels has three
schedules, three independent lines, and a pre-emption on one never touches the
other two.

---

## Registering a provider

```csharp
public class MyAiringScheduleProvider(MyClient client) : IAiringScheduleProvider
{
    public string Name => "MyProvider";
    public string Description => "Broadcast times from MyProvider's public schedule.";

    // Declared once, up front. Submitting a track of any other kind throws.
    public IReadOnlySet<AiringKind> AvailableKinds { get; } = new HashSet<AiringKind> { AiringKind.Original };

    public async Task<bool> RefreshAsync(ISeries series, CancellationToken cancellationToken = default)
    {
        // Any series may arrive: a shoko series, an AniDB anime, a TMDB show.
        // Handle what you can key on, and answer false for the rest.
        if (GetMyProviderID(series) is not { } id)
            return false;

        await client.RefreshAsync(id, cancellationToken);
        return true;
    }
}
```

### Registering it, and usually not registering it

Providers are found by reflection rather than resolved from DI, so most need no
entry in `RegisterServices` at all: core constructs the type with constructor
injection and `AddParts` holds that instance for the life of the process. That
includes a provider running its own internal timer for its own cadence, and
anything the constructor asks for (an `HttpClient`, your own rate limiter, a
`ConfigurationProvider<T>`) still resolves from DI. The full rule, and the
reasons behind each of its three branches, is [Contracts the server discovers
for you](../../README.md#contracts-the-server-discovers-for-you), in the
plugin overview.

One consequence bites harder here than for any other contract. Every write on
`IAiringScheduleService` is checked **by reference** against the instance
`AddParts` was handed, so anything else in your plugin that writes has to hold
that very object. If a job or controller of yours calls into the provider,
register the **concrete** type as a singleton:

```csharp
services.AddSingleton<MyAiringScheduleProvider>();
```

A **transient** registration, or a registration under
`IAiringScheduleProvider`, hands your job a second instance instead, and every
`AddOrUpdateSchedule` or `SetAirings` it makes throws `ArgumentException`. Keep
the reference your own constructor was given, and pass it to every write.

A provider that implements
[`ISweepingAiringScheduleProvider`](ISweepingAiringScheduleProvider.cs) needs
none of that. The server sweeps it by calling the instance it already holds, so
there is nothing of yours to hand that instance to and nothing to register. See
[Core-driven sweeps](#core-driven-sweeps-isweepingairingscheduleprovider).

### The rest of the contract

- **`AvailableKinds`** works like `IHashProvider.AvailableHashTypes`: it is
  fixed for the provider's lifetime, and a schedule with a track of an
  undeclared kind throws. Languages and channels are never declared up front,
  because they depend on the anime, which a provider can't know in advance.
- **`RefreshAsync(ISeries, ...)`** is the only required method of the base
  interface. It answers `false`, rather than
  throwing, for a series the provider has nothing for: an anime it can't key
  on, or a show it has never heard of. The `ISeason` and `IEpisode` overloads
  default to calling it with the entity's series, so a provider that only ever
  refreshes whole runs still answers every overload without extra code.
- **`RefreshAsync` is on demand, and answers one entity.** It is what a user
  clicking refresh, or another system asking for one series, goes through. It
  is never how a provider's whole source gets walked: that is
  [a sweep](#core-driven-sweeps-isweepingairingscheduleprovider), and the two
  are separate calls because they answer to different things.
- **Configuration** works exactly like a release provider's: implement
  `IAiringScheduleProvider<TConfiguration>` where `TConfiguration : IAiringScheduleProviderConfiguration`
  to have the WebUI render a settings page for your provider (an API token, a
  base URL, whichever channels to track).
- **`MaxConcurrentRefreshes`** caps how many of your own `RefreshAsync` calls
  the service runs in parallel. It defaults to one at a time.

### Enabled kinds and priority don't mean what they sound like

Both live on `AiringScheduleProviderInfo`, read through
`IAiringScheduleService.GetProviderInfo(this)`, and both are configured by the
user, not the provider:

- **`EnabledKinds`** (a subset of `AvailableKinds`) is what the user turned on.
  A track of a kind you declared but the user disabled is still stored, but
  hidden from every read. Check your own `EnabledKinds` before fetching a kind
  at all, since there's no point calling out to a dub schedule nobody asked
  for, and listen for `ProvidersUpdated` to notice when it changes.
- **`Priority`** is source order, not a ranking of "better" data or a stand-in
  for what a viewer would rather watch. Its only real job is breaking a tie
  when two enabled providers report the *same channel* for the *same episode*
  (which happens when two sources both track one streaming platform) and, a
  distant second, breaking ties during selection after track and channel
  preference. It never hides another provider's airings. What a viewer wants
  to watch is a separate, ordered list of channel and track preference (see
  `AiringTrackPreference` and the `Preferred…` options on
  `EpisodeAiringFilteringOptions`): priority answers "whose data for this
  channel", preference answers "which channel do I care about".

---

## The write flow: `FindOrRegisterChannel` → `AddOrUpdateSchedule` → `SetAirings` → `LinkAirings`

Every change goes through `IAiringScheduleService`, and every one of them
takes **your provider instance**, not an `AiringScheduleProviderInfo`. The
service checks the instance is the one it registered, then checks its ID
against the `ProviderID` stored on whatever you're changing. A schedule owned
by another provider, or a call from an unregistered instance, throws
`ArgumentException`. This guards against mistakes, not hostile code, since
plugins already run in-process as trusted code, so there is no bound "writer
handle" to fetch first. Inside the provider itself, pass `this`.

If something else in your plugin writes, a sweep job for instance, it has to be
handed the very same object, because the check is by reference and not by type.
That means registering the provider as a concrete singleton, per "Registering
it, and usually not registering it" above; a transient registration hands your
job a second instance and every write from it throws. Letting the provider do
its own writing avoids the question entirely, including when it runs its own
scheduling, because `this` is always the instance core registered.

1. **`FindOrRegisterChannel(name, type)`**: get or create a channel from the
   shared registry. An existing channel with a matching, normalised name comes
   back as-is; an unknown one is registered. Never invent a channel `Guid`
   yourself.
2. **`AddOrUpdateSchedule(provider, data)`**: create or update the one run
   this data describes. Identity is `(provider, series, season, key)`: calling
   this again with the same identity updates everything mutable on the
   existing schedule rather than creating a second one.
3. **`SetAirings(provider, schedule, airings)`**: replace the schedule's whole
   line of airings in one call. This is the normal way to write: the service
   matches new airings to old ones by key, and runs full delay inference over
   the line (see *Rules*, below). A provider that only ever sees part of a run
   at a time writes with `MergeAirings` instead; see *Writing part of a run*.
4. **`LinkAirings(provider, airings)`**: after the airings above exist, tie
   two or more of them together when one slot actually covers several
   episodes: a double-episode broadcast, or a whole season dropped at once.

### Worked example: a weekly TV run

A schedule keyed on a broadcaster's own channel ID, fed a normal week-by-week
feed. Delay inference is left on, so a pre-emption or a moved slot is worked
out for you.

```csharp
public class MyTvProvider(IAiringScheduleService airingScheduleService, MyTvGuideClient client) : IAiringScheduleProvider
{
    public string Name => "MyTvGuide";
    public IReadOnlySet<AiringKind> AvailableKinds { get; } = new HashSet<AiringKind> { AiringKind.Original };

    public async Task<bool> RefreshAsync(ISeries series, CancellationToken cancellationToken = default)
    {
        if (await client.LookupAsync(series, cancellationToken) is not { } listing)
            return false;

        // 1. Channels come from the shared registry, never made up.
        var channel = airingScheduleService.FindOrRegisterChannel(listing.ChannelName, AiringChannelType.Television);

        // 2. One schedule per (series, channel). The provider's own channel
        //    ID makes a stable key, so a later run finds the same schedule.
        var schedule = airingScheduleService.AddOrUpdateSchedule(this, new AiringScheduleData
        {
            Series = series,
            ChannelID = channel.ID,
            Key = listing.ChannelId,
            Tracks = [new AiringTrackData(AiringKind.Original, "ja")],
            IsFinished = listing.HasEnded,
        });

        // 3. One airing per episode this listing has a slot for. Leaving
        //    OriginalAiredAt and IsDelayed both null lets the service infer
        //    them from the whole line.
        var airings = listing.Episodes
            .Select(entry => new EpisodeAiringData
            {
                Episode = entry.Episode,
                AiredAt = entry.AiredAtUtc,
                // Key is a string; IEpisode.ID is an int, so convert it.
                Key = entry.Episode.ID.ToString(),
            })
            .ToList();
        var written = airingScheduleService.SetAirings(this, schedule, airings);

        // 4. Link a double-episode slot after the airings exist.
        foreach (var doubleBill in listing.DoubleBills)
        {
            // IEpisodeAiring.EpisodeID is a string, so compare it as one.
            var members = written.Where(a => doubleBill.EpisodeIDs.Any(id => id.ToString() == a.EpisodeID)).ToList();
            if (members.Count >= 2)
                airingScheduleService.LinkAirings(this, members);
        }

        return true;
    }
}
```

A later refresh that reports episode 5 two hours later, and everything after
it shifted behind it, needs no extra work: `SetAirings` sees the whole new
line, matches by key, and works out on its own that episode 5 is the delay's
cause and the rest merely moved behind it (see *Rules*).

### Worked example: a whole-season release

A streaming platform that drops every episode of a season at the same moment
has no cadence to speak of: every airing shares one timestamp, and they
belong together as a single release rather than twelve independent slots.
Delay inference has nothing useful to learn here, so it's turned off, and the
whole season is linked as one unit:

```csharp
public async Task<bool> RefreshAsync(ISeries series, CancellationToken cancellationToken = default)
{
    if (await client.LookupSeasonDropAsync(series, cancellationToken) is not { } drop)
        return false;

    var channel = airingScheduleService.FindOrRegisterChannel("MyStreamingService", AiringChannelType.Streaming);
    var schedule = airingScheduleService.AddOrUpdateSchedule(this, new AiringScheduleData
    {
        Series = series,
        ChannelID = channel.ID,
        Key = "original",
        Tracks = [new AiringTrackData(AiringKind.Subtitled, "en")],
        FirstEpisodeNumber = 1,
        LastEpisodeNumber = drop.Episodes.Count,
        IsFinished = true,
    });

    var airings = drop.Episodes
        .Select(episode => new EpisodeAiringData
        {
            Episode = episode,
            AiredAt = drop.ReleasedAtUtc,   // every episode, the same instant
            Key = episode.ID.ToString(),    // Key is a string, IEpisode.ID an int
        })
        .ToList();

    // InferDelays is meaningless for a release with no cadence: everything
    // arrives, and stays, at the same time.
    var written = airingScheduleService.SetAirings(this, schedule, airings, new EpisodeAiringUpdateOptions { InferDelays = false });

    // One link set for the whole season, so a client renders one card.
    if (written.Count >= 2)
        airingScheduleService.LinkAirings(this, written);

    return true;
}
```

`OffsetFromOriginal` on each of these airings then reads however far this
release landed from the episode's earliest known `Original` airing: a large,
uniform number for a season that streamed well after it broadcast, small or
negative for one released early or day-and-date.

### Writing part of a run: `MergeAirings`

Both examples above hand over a whole run, because `SetAirings` reads the
submission as the schedule's entire line. Some sources don't work that way. A
weekly guide that only publishes the coming fortnight, or a feed that hands you
one week at a time, would have to refetch a show's whole run every time just to
resubmit it, or else append and lose every judgement the inference makes.

`MergeAirings(provider, schedule, airings, removals, options)` is the write for
that. It adds or updates what you pass, takes away what you name in
`removals`, and leaves every other airing on the schedule alone. What it does
*not* change is the inference: the service still assembles the schedule's whole
stored line, still works out which airings merely shifted behind a break, and
still decides whether something that went away is a hiatus or history. The one
difference is where the removal set comes from.

**The one thing to get right:**

| | `SetAirings` | `MergeAirings` |
|---|---|---|
| An airing you pass | added or updated | added or updated |
| An airing you leave out | **removed** | **untouched** |
| An airing you name in `removals` | n/a | removed |

Silence means opposite things in the two calls. That is the whole reason they
are separate methods rather than one with a flag, so pick by which default you
want and never by which is shorter to call.

```csharp
public async Task<bool> RefreshAsync(ISeries series, CancellationToken cancellationToken = default)
{
    if (await client.LookupAsync(series, cancellationToken) is not { } listing)
        return false;

    var channel = airingScheduleService.FindOrRegisterChannel(listing.ChannelName, AiringChannelType.Television);
    var schedule = airingScheduleService.AddOrUpdateSchedule(this, new AiringScheduleData
    {
        Series = series,
        ChannelID = channel.ID,
        Key = listing.ChannelId,
        Tracks = [new AiringTrackData(AiringKind.Original, "ja")],
    });

    // This week's slots, and nothing else. Every earlier week this provider
    // already wrote stays exactly as it is.
    var week = listing.ThisWeek
        .Select(entry => new EpisodeAiringData
        {
            Episode = entry.Episode,
            AiredAt = entry.AiredAtUtc,
            Key = entry.Episode.ID.ToString(),
        })
        .ToList();

    // A slot the guide dropped from a week it still publishes. Say so, because
    // leaving it out of the submission no longer means anything.
    var stored = airingScheduleService.GetAiringsForSchedule(schedule.ID, new EpisodeAiringFilteringOptions { IncludeEstimates = false });
    var pulled = stored
        .Where(airing => listing.CoversSlot(airing.AiredAt) && !listing.ThisWeek.Any(entry => entry.Episode.ID.ToString() == airing.Key))
        .ToList();

    airingScheduleService.MergeAirings(this, schedule, week, pulled);
    return true;
}
```

A removal is the same signal an omission is to `SetAirings`, and **not** the
outright delete `RemoveAiring` performs. A removed airing whose slot is still
ahead of us is what a source pre-empting an episode looks like, so it is kept
without a slot, with the slot it lost on `OriginalAiredAt`. One whose slot has
already passed, or that falls outside what the schedule covers, is deleted as
history. Reach for `RemoveAiring` when you mean "this row was a mistake", and
for a `removals` entry when you mean "my source no longer lists this".

**Coverage is not guessed from a delta.** Where a run starts and ends, and
whether it has finished, decide whether a removal is a hiatus or history, and a
provider writing one week has no view on any of it. So `MergeAirings` never
writes `FirstEpisodeNumber`, `LastEpisodeNumber` or `IsFinished`; only
`AddOrUpdateSchedule` and `UpdateSchedule` do. If this particular write does
know something, say it on `EpisodeAiringUpdateOptions`, where a property left
alone means "no opinion" and the schedule's own value is read:

```csharp
// This week's feed says the run ended, so the slot the guide dropped is
// history rather than a pre-emption. The schedule's own coverage is untouched;
// call AddOrUpdateSchedule to change that.
airingScheduleService.MergeAirings(this, schedule, week, pulled, new EpisodeAiringUpdateOptions { IsFinished = true });
```

Stated coverage judges this write's removals and nothing else. It never widens
what an airing is allowed to be: an episode outside what the *schedule* covers
is still rejected, exactly as it is on `SetAirings`.

Everything else is shared with `SetAirings`. Ownership, the
episode-belongs-to-this-schedule check, duplicate keys and the retention window
are the same checks reported the same way, links are re-pointed the same way,
and the `AiringsUpdated` event is filled in the same way from what the write
did, so a consumer can't tell which of the two you used. The one extra
rejection is naming an airing in both `airings` and `removals`, which
contradicts itself.

### What a write raises

Every write dispatches `AiringsUpdated` exactly once, once the schedule is
whole again, carrying the schedule and three lists. One event per airing would
hand a consumer a half-written schedule, and a sweep over a long run writes
thousands of rows at a time, so the whole write arrives as one event instead:

- `Added`: the airings that weren't on the schedule before.
- `Updated`: the airings that kept their place while something about them
  moved, which is the slot, the slot it was first scheduled for, the delay
  flag, the link, the url, or the episode behind the key.
- `Withdrawn`: the airings the write took off the line. That is not the same as
  deleted: a slot still ahead of us is kept without one, as a hiatus, exactly
  as above, while a slot already past is deleted as history. Both are reported
  here, and reading the schedule back is what tells them apart, since the
  hiatus is still on it and the history isn't.

`Airings` is the three of them in that order, for a consumer that only wants
"what did this write touch".

An airing a write hands back exactly as it is stored is in none of the three.
The row isn't rewritten, its `LastUpdatedAt` doesn't move, and nothing is
reported for it, whichever of the two entry points the write came in through.
A write that changed nothing at all is still dispatched, with three empty lists
and a `Reason` of `UpdateReason.None`.

`Reason` is the one coarse value left for a consumer that doesn't read the
three lists apart: `Added` when the write only added, `Removed` when it only
withdrew, `None` when it did nothing, and `Updated` for everything else,
including a write that did more than one of those things.

---

## Core-driven sweeps: `ISweepingAiringScheduleProvider`

`RefreshAsync` answers one entity because something asked for it. Walking a
provider's whole source is the other half, and
[`ISweepingAiringScheduleProvider`](ISweepingAiringScheduleProvider.cs) is how a
provider opts into having the server do the walking for it. Implement it
alongside `IAiringScheduleProvider` and the server finds it, works out when the
provider is due, and calls it until it says it is done:

```csharp
public class MyAiringScheduleProvider(IAiringScheduleService airingScheduleService, MyClient client)
    : IAiringScheduleProvider, ISweepingAiringScheduleProvider
{
    public string Name => "MyProvider";

    public IReadOnlySet<AiringKind> AvailableKinds { get; } = new HashSet<AiringKind> { AiringKind.Original };

    // This source publishes a week at a time, so a week is the unit.
    public TimeSpan? SuggestedSweepInterval => TimeSpan.FromHours(12);

    public async Task<string?> SweepAsync(string? cursor, CancellationToken cancellationToken)
    {
        var week = cursor is null ? client.CurrentWeek : DateOnly.Parse(cursor);
        while (!cancellationToken.IsCancellationRequested)
        {
            if (await client.GetWeekAsync(week, cancellationToken) is not { } listing)
                return null;          // nothing more to walk: the sweep is over

            await WriteWeekAsync(listing, cancellationToken);
            week = week.AddDays(7);

            if (week > client.LastPublishedWeek)
                return null;          // reached the end of the source
        }

        return week.ToString("O");    // out of budget: resume here next time
    }

    public Task<bool> RefreshAsync(ISeries series, CancellationToken cancellationToken = default)
        => RefreshOneAsync(series, cancellationToken);
}
```

Nothing is registered for this. The server holds the instance `AddParts` was
handed and sweeps that object, so `this` inside `SweepAsync` is the same
instance every write is checked against, and there is no job, hosted service or
singleton of the plugin's involved for the container to have to line up.

### The unit is yours, the pacing is the server's

Core never learns what a chunk is. One provider walks anime ids, another walks
weeks, another walks shows, and all core does is call `SweepAsync` again with
whatever came back. What core does own is everything around that:

- **Whether you are swept at all.** A disabled provider, meaning one with no
  enabled kinds, is never swept. The gate is in core, on both the dispatch and
  the chunk itself, so a provider disabled while its chunk sits in the queue is
  still not swept. A provider whose kinds are merely narrowed *is* swept, and is
  expected to check its own `EnabledKinds` before fetching a kind nobody asked
  for, exactly as for a refresh.
- **When the next sweep starts.** `SuggestedSweepInterval` is a suggestion in
  the same sense `AvailableKinds` is a declaration: the provider says what it
  knows about its own source, and the value actually used is
  `AiringScheduleProviderInfo.SweepInterval`, which the user owns and can change
  from the WebUI. A provider that suggests nothing gets the server's default of
  a day. Anything under fifteen minutes is clamped.
- **How long one chunk may run,** which is not the provider's at all. A chunk
  holds a queue worker while it runs and a worker is shared, so the budget is
  the server's, the same for every provider, and set well inside the queue
  watchdog's own timeout.
- **Where you resume,** which is the cursor, below.

The chunks of one sweep follow each other as fast as the queue allows; the
interval only governs the gap between whole sweeps.

### The deadline, and the one token

The token handed to `SweepAsync` is cancelled when the chunk's budget runs out,
and it is linked to the worker's own shutdown token, so there is exactly one
token to observe and it covers both. Watch it and return a cursor before it
fires: that is a chunk that did its share and said where to pick up. If it fires
first, the chunk is recorded as having timed out and the next one resumes from
the last cursor you actually returned, so everything since is lost. A rate
limited source therefore gets less done per chunk rather than holding a worker
hostage, which is the whole point of chunking by deadline rather than by count.

How a chunk ended is one of five:

| Outcome | What happened |
|---|---|
| `Completed` | `SweepAsync` returned. Its cursor says whether the sweep carries on. |
| `TimedOut` | The budget ran out first. |
| `Stopped` | The server is shutting down, or the queue was stopped. Not held against the provider. |
| `Cancelled` | The provider cancelled the chunk itself, with neither of the above having fired. |
| `Failed` | The provider threw. Every other provider is unaffected. |

Telling the middle three apart is best effort, not exact. All three arrive as an
`OperationCanceledException` and the server decides between them by asking which
token was cancelled by the time it caught one, so a provider cancelling for its
own reason in the very instant the budget runs out is reported as a timeout.
Nothing downstream depends on getting that right; it decides how loudly the run
is logged and how soon the next chunk is queued.

### The cursor

`SweepAsync` returns a `string?` that is **opaque to the server**, which stores
it and hands it back without ever reading into it:

- **`null` means the sweep is finished.** Nothing resumes, and the provider is
  left alone until its interval has passed.
- **Anything else means call me again with this.** The next chunk is queued
  straight away.

Use whatever suits the walk: an id, a date, a page token, a small JSON blob. It
has to fit in **512 characters**, which is what the airing tables give a name,
and a longer one is refused and ends the sweep rather than being truncated. A
provider that needs real state keeps that state itself and puts only an
identifier for it in the cursor.

The cursor is also what decides whether a chunk got anywhere. A chunk that hands
back the cursor it was given, or that times out without having returned one at
all, has made no progress, and the server counts those. Three in a row and the
sweep stops resuming at the next opportunity and waits out the provider's whole
interval instead, until a chunk moves it on again. That is what keeps a source
too slow to finish even one unit inside a chunk from being called for ever
without advancing, and the log says which of the two it is: a chunk that ran out
of budget is slow, one that returned the same cursor is stuck.

### Writing from a sweep: reach for `MergeAirings`

A sweep almost never wants `SetAirings`, because `SetAirings` reads a submission
as a schedule's *entire* line and a chunk holds part of one. `MergeAirings`
(see [Writing part of a run](#writing-part-of-a-run-mergeairings)) is the write
a chunk wants: it adds and updates what you pass, removes what you name, leaves
every other airing on the schedule alone, and still runs the full inference over
the schedule's whole stored line. A week-at-a-time or chunk-at-a-time walk gets
the same delay and hiatus judgements as a provider that resubmits a whole run,
without refetching the run to do it.

### What a sweep leaves behind

Per provider, the server keeps where the sweep got to, when the last chunk ran,
how it ended, and how many chunks in a row have got nowhere. That is what the
driver needs to resume and nothing more, and it is overwritten every chunk.

There is no run history. Each finished chunk raises
`IAiringScheduleService.SweepCompleted` carrying the provider, the outcome, when
the chunk started and finished, and whether the sweep is over, and logs a line
with the duration worked out from those. Anything that wants a history
subscribes and keeps its own. Core also bridges the event to SignalR clients as
`airing:provider.swept`, next to `airing:episode.aired`.

---

## Reacting to an episode airing

Everything above is about *writing* the schedule. `SubscribeToAirings` is how
you *react* to it: the server keeps the next hour of airings in memory and hands
each minute's worth to its subscribers as the slots pass, so a plugin can act on
an episode airing without polling on a timer of its own.

A subscription lives for as long as something holds its handle, and has to be
disposed on shutdown, so it belongs in a hosted service rather than on the class
implementing `IPlugin`, which cannot take constructor dependencies at all (see
[the plugin overview](../../README.md#iplugin-needs-a-public-parameterless-constructor)).
Register it with `services.AddHostedService<AiredWatcher>()` from your
`RegisterServices`.

```csharp
public sealed class AiredWatcher : BackgroundService
{
    private readonly IQueueScheduler _queueScheduler;

    private readonly IDisposable _subscription;

    public AiredWatcher(IAiringScheduleService airingScheduleService, IQueueScheduler queueScheduler)
    {
        _queueScheduler = queueScheduler;
        // Only the real broadcasts, on the channels this plugin cares about.
        // Everything not asked for here is never dispatched to this handler.
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
        => Task.CompletedTask; // the subscription does the work; nothing to loop on

    private void OnEpisodesAired(EpisodeAiredEventArgs e)
    {
        // One call per minute, carrying everything that aired in it. Group it
        // however this plugin thinks about airings; here, once per episode.
        foreach (var group in e.Airings.GroupBy(airing => airing.ShokoEpisode?.ID))
        {
            if (group.Key is not { } episodeID)
                continue;

            // Hand the real work to the queue and get off the ticker's thread.
            _queueScheduler.Enqueue<MyJob>(job => job.EpisodeID = episodeID);
        }
    }
}
```

### Why a subscription rather than an event

A plain `EpisodeAired += …` cannot do either of the two things that matter
here. It cannot carry **per-subscriber filtering**, leaving every handler to get
every airing and re-filter it by hand, and it cannot let the producer **do less
work**, because an event has no idea whether anyone is listening or what they
would want. A subscription knows both:

- **Each subscriber brings its own `EpisodeAiringFilteringOptions`**, applied
  before its handler is called. Pass `null` for everything. The same options
  object the read path takes, so a filter that works on `GetAiringsInRange`
  works here unchanged.
- **The lookahead is built from the *union* of the live subscriptions.** This
  is mostly about `IncludeEstimates`: estimates are computed through the read
  path rather than stored, so they are the expensive part of filling the hour.
  If no live subscriber wants estimates, none are computed for anybody.
- **Nobody subscribed means no work at all:** no lookahead, no estimate
  pipeline, nothing. The first subscriber to arrive re-arms it on the next tick,
  not at the next quarter-hour refresh.

### Four things to build around

- **It is one call per *minute*, not per airing.** A simulcast puts several
  airings on the same minute, say the same episode at 11:25 on both テレビ愛知
  and テレビ東京, and they arrive together as one list. Group `e.Airings` by
  `ShokoEpisode` for "this episode aired, once", by `LinkID` for one card per
  slot, or leave it alone for a row per channel. A handler is only called for a
  minute that has something for it, so the list is never empty.
- **Estimates are dispatched too, and an estimate is a prediction.** Turn them
  off with `IncludeEstimates = false`, or check `IsEstimated` on each airing.
  Nothing is known to have aired, the time came out of the schedule's learned
  slot rather than a source, and **there is no retraction** if a provider later
  moves it. When the real slot arrives it is a *different* airing with its own
  stable ID, dispatched in its own minute; that is correct rather than a
  duplicate, and a handler that treats both as "it aired" will act twice.
- **Nothing is replayed,** neither what passed during downtime nor what passed
  before you subscribed. An hours-old prediction is worse than none. Read the
  gap back with `GetAiringsInRange`.
- **Handlers run on the ticker's own thread.** A slow handler holds up the
  minute and every subscriber behind it. Enqueue the work and return; Shoko has
  a queue for exactly that. A handler that throws is logged against its
  subscriber and stepped over, and never takes the tick or another subscriber
  with it.

### Polling instead: `GetAiringsInRange`

A consumer that would rather pull than be pushed reads
`GetAiringsInRange(fromUtc, toUtc, options)` on whatever cadence suits it. It
takes the **same** `EpisodeAiringFilteringOptions` and answers the same airings,
so there is no reason to re-implement any of the filtering on top of the push.
It is also how a subscriber fills the gap left by a restart. Dispose the
subscription handle and poll instead, or do both.

Core bridges the same dispatch to SignalR clients as `airing:episode.aired`; see
`Shoko.Server/API/v3/AiringSchedule.md`.

### Anchoring a read to shoko entities

Both `EpisodeAiringFilteringOptions` and `AiringScheduleFilteringOptions` carry
an `EntityAnchor`, which says *whose* entities an answer is about:

| Value | Meaning |
|---|---|
| `Auto` (the default) | Infer it. A read given a shoko entity anchors to `Shoko`; one given an AniList, TMDB or plugin entity anchors to that source, i.e. `Raw`. A read given no entity at all (`GetAiringsForSchedule(Guid)`, `GetAiringsInRange`, `GetLinkedAirings(Guid)`, `GetSchedulesForProvider`, `GetSchedulesForChannel`, and a subscription) falls back to `Raw`. |
| `Raw` | The provider's own entities, exactly as stored. Nothing is dropped for having no counterpart in the collection. |
| `Shoko` | Only what resolves to a shoko entity. An airing with no `IShokoEpisode` behind it, or a schedule with no `IShokoSeries`, drops out. |

It is an enum with an `Auto` member rather than a nullable one on purpose:
`default` lands on inference, a `switch` over it is checked by the compiler, and
it serialises over the v3 wire as a self-documenting `"Auto"` rather than an
absent field. (The `bool?` on `LinkedEntityAirings` and `LinkedEntitySchedules`
predates this and only looks the way it does because a `bool` has no room for a
third named state.)

The anchor **composes with** those linked-entity options rather than overriding
them: the links decide what a read *finds*, the anchor decides what *survives*.
A read that started from a shoko entity has already reached everything through
it, so the anchor it infers has nothing left to drop and the read is unchanged.
That is why `Auto` never alters what an existing caller gets. Anchoring a
*provider* entity's read to `Shoko` while `LinkedEntityAirings = false`
correctly answers nothing, since without the links there is no shoko episode to
reach.

---

## Rules a provider must respect

- **Ownership.** Every change is checked against the registered provider
  instance and the schedule or airing's stored owner. Passing another
  provider's schedule, or calling from an unregistered instance, throws
  `ArgumentException`; see the table below.
- **Submit whole schedules through `SetAirings`.** It's the normal path
  because delay inference needs the *whole* line at once: a pre-emption, a
  hiatus and which airings merely shifted behind it are all judgements made
  by comparing an entire schedule's old airings to its new ones. A source that
  only ever hands you part of a run goes through `MergeAirings`, which reaches
  the same inference with the removals stated rather than left out. Reach for
  `AddOrUpdateAiring` / `UpdateAiring` only for a genuinely incremental or
  manual edit to one entry; they run no cause detection and no hiatus
  inference at all.
- **Leave `IsDelayed` (and usually `OriginalAiredAt`) null and let the service
  infer them**, unless your source already reports delays itself. If it does,
  pass `EpisodeAiringUpdateOptions { InferDelays = false }` to the write and
  set both fields yourself on every airing. With inference off, airings are
  stored exactly as submitted, and an airing you don't resubmit is deleted
  rather than kept as a possible hiatus.
- **Name regional channels with `IAiringScheduleService.GetRegionalChannelName`**,
  never by hand. `GetRegionalChannelName("Amazon", "US")` → `"Amazon (US)"`.
  It is the only supported spelling, so two providers naming the same regional
  service never drift into two channels. Leave the suffix off entirely for a
  worldwide or region-unknown service, and never add one to a broadcast
  station.
- **Respect retention.** `SetAirings`, `MergeAirings`, `AddOrUpdateAiring` and
  `UpdateAiring` reject (as part of `AiringScheduleValidationException`) an
  airing older than the service's configured retention window while automatic
  cleanup is on.
  There's no point submitting a slot from four years ago; it would only be
  swept again on the next run. A provider backfilling history should trim its
  own submission to a recent window rather than relying on the write to do it.
- **A schedule's series, season, key and channel never change** once created,
  and `AddOrUpdateSchedule` rejects an attempt to change any of them. Its identity,
  though, is `(provider, series, season, key)`: that is what `GetScheduleID`
  hashes, and the channel is immutable without being part of it. Likewise an
  airing's schedule, key and episode never change on an existing row.
  `AddOrUpdateSchedule`/`UpdateSchedule` and `AddOrUpdateAiring`/`UpdateAiring`
  reject an attempt to change them.
- **Prefer passing a stable `Key`.** Without one, a schedule's key is derived
  from its channel and full track set, so adding a language to a keyless
  schedule silently creates a *new* schedule instead of updating the old one.
  Use whatever your own source already treats as stable (a channel ID, a slug,
  an internal ID) rather than anything computed from the tracks.

### Which mistakes throw

Every member documents its own exceptions with `<exception>` tags; this is
the shape of the contract:

| Members | Exception | When |
|---|---|---|
| All of them | `ArgumentNullException` | A required argument is `null`. |
| All but the channel and time-zone reads | `InvalidOperationException` | `AddParts` has not run yet. |
| Every change, and `GetProviderInfo(provider)` | `ArgumentException` | The provider isn't the registered instance, or doesn't own the schedule or airing it was handed. |
| `FindOrRegisterChannel` | `ArgumentException` | The name is blank once normalised. |
| `AddChannelAliases` | `ChannelAliasConflictException` | An alias is another channel's own name, or another channel's alias, of the same type. Carries the alias, the channel already holding it, and which of the two it holds it as, so a bulk seeder can skip that one and move on. |
| `AddOrUpdateSchedule`, `UpdateSchedule` | `ArgumentException` | No tracks; a track of a kind the provider doesn't declare; an unregistered channel; a different channel for the same identity; a coverage range whose first episode is after its last. |
| Anything taking a time zone | `TimeZoneNotFoundException` | The zone can't be normalised to an IANA id or a fixed offset. |
| `SetAirings`, `MergeAirings`, `AddOrUpdateAiring`, `UpdateAiring` | `AiringScheduleValidationException` | An episode outside the schedule's series, season or coverage; two airings sharing a key in one call; an airing both submitted and removed by the same `MergeAirings` call; an airing older than the retention window while cleanup is on. This is a `GenericValidationException`, so a batch call reports *every* rejected airing at once, keyed by airing key, instead of failing on the first. |
| `MergeAirings` | `ArgumentException` | A removal is an estimate, is unknown, or is on another schedule. |
| `LinkAirings`, `UnlinkAiring` | `ArgumentException` | Fewer than two airings passed to `LinkAirings`, or the airings span more than one schedule. |
| `RefreshAsync` (the awaitable overloads) | `OperationCanceledException` | The token was cancelled. A provider that throws instead is reported in the result, never re-thrown. |

---

## Entity resolvers

Schedules and airings key on `ISeries`/`ISeason`/`IEpisode`, which the core
entities (AniDB, TMDB, AniList, Shoko) already implement. A plugin with its
*own* series, seasons or episodes, one backed by another metadata source
entirely, registers an `IAiringScheduleEntityResolver` so the service can
still enrich and follow links for them, the same role
`IImageCrossReferenceResolver` plays for images:

```csharp
public class MyEntityResolver : IAiringScheduleEntityResolver
{
    public string Name => "MyProvider";

    // Key → entity, used both to enrich a stored schedule or airing back into
    // your own types, and to answer range queries.
    // DataSource is a closed enum : byte, so a plugin cannot add a member to
    // it. Everything a plugin owns arrives as DataSource.Plugin.
    public IMetadata? GetEntity(DataSource source, DataEntityType type, string id)
        => source == DataSource.Plugin ? _repository.GetByID(type, id) : null;

    // Shoko entity → this resolver's entities linked to it, in link order.
    // Added to what the service already walks itself (IShokoSeries.LinkedSeries,
    // IShokoSeason.LinkedSeasons, IShokoEpisode.LinkedEpisodes).
    public IEnumerable<IMetadata> GetLinkedEntities(IMetadata shokoEntity)
        => shokoEntity is IShokoSeries series ? _repository.GetLinkedTo(series) : [];
}
```

Resolvers are discovered exactly the way providers are, so the same rule
applies: register nothing unless your own code, typically the provider next to
it, resolves the resolver itself, in which case register the concrete type as a
singleton so the two of you share one instance, and never register it under
`IAiringScheduleEntityResolver`.

```csharp
// Only if your own code resolves it.
services.AddSingleton<MyEntityResolver>();
```

If your provider only ever schedules AniDB, TMDB or AniList entities directly,
which is the common case since a plugin usually keys on IDs a metadata source
already understands, you don't need a resolver at all.
