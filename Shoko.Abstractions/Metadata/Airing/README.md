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
| **Provider** | A source of schedules — AniList, or a plugin's own — identified by a stable `Guid`. |
| **Channel** | Where something airs, from a registry shared by every provider: "TOKYO MX" (`Television`), "Crunchyroll" (`Streaming`). Regional services carry the region in the name, e.g. `Amazon (US)`. |
| **Schedule** | One provider's run of a series, optionally narrowed to a season, on one channel (or none), releasing a fixed set of tracks. Owned by the provider that created it. |
| **Track** | What a schedule releases: a `Kind` (`Original`, `Subtitled`, `Dubbed`) plus a language code, an optional country code, and the `TitleLanguage` inferred from them. A language released at a different time is a different schedule, not another track on this one. |
| **Episode airing** | One episode on one schedule: a time, the slot it was first scheduled for, a delay flag, and an optional link to the other airings of the same slot. |
| **Coverage** | Which episodes a schedule is for — an optional episode range, plus whether the provider considers the run finished. Estimates never go past it. |
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

Core finds providers by reflecting over your assembly's *types* and then asking
the container for each one **by its concrete type**
(`ActivatorUtilities.GetServiceOrCreateInstance`). That call constructs the
instance when the type is not registered, injecting its constructor
dependencies from DI as normal, and `AddParts` holds what comes back for the
life of the process. Three branches follow from that, in the order you should
reach for them:

**1. No registration at all.** The default, and what most providers want.

```csharp
// Nothing. Core discovers MyAiringScheduleProvider, constructs it with
// constructor injection, and keeps it.
```

A provider that core only ever drives through `RefreshAsync` needs no entry in
`RegisterServices` whatsoever. That includes one running its own internal timer
for its own cadence, since the instance core holds is long-lived either way.
Anything the constructor asks for (an `HttpClient`, your own rate limiter, a
`ConfigurationProvider<T>`) still resolves from DI.

**2. Register the concrete type as a singleton,** but only when your own code
resolves the provider: a queue job or a controller of yours that calls into it,
for instance.

```csharp
services.AddSingleton<MyAiringScheduleProvider>();
```

The singleton lifetime is the whole point: it is what makes your job and core
share *one* instance. A **transient** registration here is as bad as no
registration at all, since your job and core would each construct their own. If
you register it, register it as a singleton.

This is what the three shipping providers do: `SyoboiAiringScheduleProvider`,
`AnimeScheduleProvider` and `TvMazeAiringScheduleProvider` are each registered
as singletons because each plugin's own sweep job takes the provider by concrete
type in its constructor.

**3. Never register it under the interface.**

```csharp
// Wrong, in three separate ways.
services.AddSingleton<IAiringScheduleProvider, MyAiringScheduleProvider>();
```

- **It pollutes the container for everyone else.** Resolving a single
  `IAiringScheduleProvider` returns whichever registration came *last*, so with
  two plugins doing this the winner is plugin load order, which is arbitrary and
  changes when a user installs or removes some unrelated plugin. Core or another
  plugin calling `GetRequiredService<IAiringScheduleProvider>()` then quietly
  gets your provider instead of its own.
- **Core never looks at it.** `GetExports<T>()` asks for the concrete type, so
  the interface registration is dead weight.
- **So a second instance is built.** The concrete type is still unregistered,
  `GetServiceOrCreateInstance` constructs a fresh one, and you now have two: the
  one in DI that nothing reaches, and the one core holds. Singleton state (rate
  limiters, caches, HTTP clients, warn-once flags) splits between them, and
  because the service checks every write against the instance it was handed
  at registration, code that resolves `IAiringScheduleProvider` out of DI holds
  the wrong object and every `AddOrUpdateSchedule` or `SetAirings` it makes
  throws `ArgumentException`.

Keep the reference your own constructor was given, and pass it to every write.

### The rest of the contract

- **`AvailableKinds`** works like `IHashProvider.AvailableHashTypes`: it is
  fixed for the provider's lifetime, and a schedule with a track of an
  undeclared kind throws. Languages and channels are never declared up front —
  they depend on the anime, which a provider can't know in advance.
- **`RefreshAsync(ISeries, ...)`** is the only required method, and the only
  call core ever makes into a provider. It answers `false` for a series the
  provider has nothing for — an anime it can't key on, or a show it has never
  heard of — rather than throwing. The `ISeason` and `IEpisode` overloads
  default to calling it with the entity's series, so a provider that only ever
  refreshes whole runs still answers every overload without extra code.
- **Fetching is entirely the provider's own job.** Core never walks providers
  to collect a series on its own schedule; each provider owns its cadence and
  its own rate limits through its own recurring jobs (see
  `RecurringJobRegistry` in the main `CLAUDE.md`), and `RefreshAsync` exists so
  a user or another system can ask for one, on demand, right now.
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
  at all — there's no point calling out to a dub schedule nobody asked for —
  and listen for `ProvidersUpdated` to notice when it changes.
- **`Priority`** is source order, not a ranking of "better" data or a stand-in
  for what a viewer would rather watch. Its only real job is breaking a tie
  when two enabled providers report the *same channel* for the *same episode*
  — which happens when two sources both track one streaming platform — and, a
  distant second, breaking ties during selection after track and channel
  preference. It never hides another provider's airings. What a viewer wants
  to watch is a separate, ordered list of channel and track preference (see
  `AiringTrackPreference` and the `Preferred…` options on
  `EpisodeAiringFilteringOptions`) — priority answers "whose data for this
  channel", preference answers "which channel do I care about".

---

## The write flow: `FindOrRegisterChannel` → `AddOrUpdateSchedule` → `SetAirings` → `LinkAirings`

Every change goes through `IAiringScheduleService`, and every one of them
takes **your provider instance**, not an `AiringScheduleProviderInfo`. The
service checks the instance is the one it registered, then checks its ID
against the `ProviderID` stored on whatever you're changing. A schedule owned
by another provider, or a call from an unregistered instance, throws
`ArgumentException`. This guards against mistakes, not hostile code — plugins
already run in-process as trusted code — so there's no bound "writer handle"
to fetch first; just keep the `IAiringScheduleProvider` reference your
constructor was given, and pass it every time.

1. **`FindOrRegisterChannel(name, type)`** — get or create a channel from the
   shared registry. An existing channel with a matching, normalised name comes
   back as-is; an unknown one is registered. Never invent a channel `Guid`
   yourself.
2. **`AddOrUpdateSchedule(provider, data)`** — create or update the one run
   this data describes. Identity is `(provider, series, season, key)`: calling
   this again with the same identity updates everything mutable on the
   existing schedule rather than creating a second one.
3. **`SetAirings(provider, schedule, airings)`** — replace the schedule's whole
   line of airings in one call. This is the normal way to write: the service
   matches new airings to old ones by key, and runs full delay inference over
   the line (see *Rules*, below).
4. **`LinkAirings(provider, airings)`** — after the airings above exist, tie
   two or more of them together when one slot actually covers several
   episodes: a double-episode broadcast, or a whole season dropped at once.

### Worked example: a weekly TV run

A schedule keyed on a broadcaster's own channel ID, fed a normal week-by-week
feed. Delay inference is left on, so a pre-emption or a moved slot is worked
out for you.

```csharp
public class MyTvProvider(IAiringScheduleService airingScheduleService) : IAiringScheduleProvider
{
    public string Name => "MyTvGuide";
    public IReadOnlySet<AiringKind> AvailableKinds { get; } = new HashSet<AiringKind> { AiringKind.Original };

    public async Task<bool> RefreshAsync(ISeries series, CancellationToken cancellationToken = default)
    {
        if (await client.LookupAsync(series, cancellationToken) is not { } listing)
            return false;

        // 1. Channels come from the shared registry — never made up.
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
                Key = entry.Episode.ID,
            })
            .ToList();
        var written = airingScheduleService.SetAirings(this, schedule, airings);

        // 4. Link a double-episode slot after the airings exist.
        foreach (var doubleBill in listing.DoubleBills)
        {
            var members = written.Where(a => doubleBill.EpisodeIDs.Contains(a.EpisodeID)).ToList();
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
has no cadence to speak of — every airing shares one timestamp, and they
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
            Key = episode.ID,
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
release landed from the episode's earliest known `Original` airing — a large,
uniform number for a season that streamed well after it broadcast, small or
negative for one released early or day-and-date.

---

## Reacting to an episode airing

Everything above is about *writing* the schedule. `SubscribeToAirings` is how
you *react* to it: the server keeps the next hour of airings in memory and hands
each minute's worth to its subscribers as the slots pass, so a plugin can act on
an episode airing without polling on a timer of its own.

```csharp
public class Plugin : IPlugin, IDisposable
{
    private readonly IDisposable _subscription;

    public Plugin(IAiringScheduleService airingScheduleService, IQueueScheduler queueScheduler)
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

    public void Dispose() => _subscription.Dispose();

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
  `ArgumentException` — see the table below.
- **Submit whole schedules through `SetAirings`.** It's the normal path
  because delay inference needs the *whole* line at once — a pre-emption, a
  hiatus and which airings merely shifted behind it are all judgements made
  by comparing an entire schedule's old airings to its new ones. Reach for
  `AddOrUpdateAiring` / `UpdateAiring` only for a genuinely incremental or
  manual edit to one entry; they run no cause detection and no hiatus
  inference at all.
- **Leave `IsDelayed` (and usually `OriginalAiredAt`) null and let the service
  infer them**, unless your source already reports delays itself. If it does,
  pass `EpisodeAiringUpdateOptions { InferDelays = false }` to `SetAirings` and
  set both fields yourself on every airing — with inference off, airings are
  stored exactly as submitted, and an airing you don't resubmit is deleted
  rather than kept as a possible hiatus.
- **Name regional channels with `IAiringScheduleService.GetRegionalChannelName`**,
  never by hand — `GetRegionalChannelName("Amazon", "US")` → `"Amazon (US)"`.
  It is the only supported spelling, so two providers naming the same regional
  service never drift into two channels. Leave the suffix off entirely for a
  worldwide or region-unknown service, and never add one to a broadcast
  station.
- **Respect retention.** `SetAirings`, `AddOrUpdateAiring` and `UpdateAiring`
  reject (as part of `AiringScheduleValidationException`) an airing older than
  the service's configured retention window while automatic cleanup is on.
  There's no point submitting a slot from four years ago; it would only be
  swept again on the next run. A provider backfilling history should trim its
  own submission to a recent window rather than relying on the write to do it.
- **A schedule's series, season, key and channel never change** once created —
  changing any of them is a different schedule, by design (its identity is
  exactly those four things). Likewise an airing's schedule, key and episode
  never change on an existing row. `AddOrUpdateSchedule`/`UpdateSchedule` and
  `AddOrUpdateAiring`/`UpdateAiring` reject an attempt to change them.
- **Prefer passing a stable `Key`.** Without one, a schedule's key is derived
  from its channel and full track set, so adding a language to a keyless
  schedule silently creates a *new* schedule instead of updating the old one.
  Use whatever your own source already treats as stable — a channel ID, a
  slug, an internal ID — rather than anything computed from the tracks.

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
| `SetAirings`, `AddOrUpdateAiring`, `UpdateAiring` | `AiringScheduleValidationException` | An episode outside the schedule's series, season or coverage; two airings sharing a key in one call; an airing older than the retention window while cleanup is on. This is a `GenericValidationException`, so a batch call reports *every* rejected airing at once, keyed by airing key, instead of failing on the first. |
| `LinkAirings`, `UnlinkAiring` | `ArgumentException` | Fewer than two airings passed to `LinkAirings`, or the airings span more than one schedule. |
| `RefreshAsync` (the awaitable overloads) | `OperationCanceledException` | The token was cancelled. A provider that throws instead is reported in the result, never re-thrown. |

---

## Entity resolvers

Schedules and airings key on `ISeries`/`ISeason`/`IEpisode`, which the core
entities (AniDB, TMDB, AniList, Shoko) already implement. A plugin with its
*own* series, seasons or episodes — one backed by another metadata source
entirely — registers an `IAiringScheduleEntityResolver` so the service can
still enrich and follow links for them, the same role
`IImageCrossReferenceResolver` plays for images:

```csharp
public class MyEntityResolver : IAiringScheduleEntityResolver
{
    public string Name => "MyProvider";

    // Key → entity, used both to enrich a stored schedule or airing back into
    // your own types, and to answer range queries.
    public IMetadata? GetEntity(DataSource source, DataEntityType type, string id)
        => source == MyDataSource.Instance ? _repository.GetByID(type, id) : null;

    // Shoko entity → this resolver's entities linked to it, in link order.
    // Added to what the service already walks itself (IShokoSeries.LinkedSeries,
    // IShokoSeason.LinkedSeasons, IShokoEpisode.LinkedEpisodes).
    public IEnumerable<IMetadata> GetLinkedEntities(IMetadata shokoEntity)
        => shokoEntity is IShokoSeries series ? _repository.GetLinkedTo(series) : [];
}
```

Resolvers are discovered exactly the way providers are, so the same three
branches apply:

1. **Usually, register nothing.** A resolver core only ever calls to look an
   entity up or follow a link needs no entry in `RegisterServices`; it will be
   constructed with constructor injection and held.
2. **Register the concrete type as a singleton** when your own code, typically
   the provider next to it, resolves the resolver itself, so the two of you
   share one instance. A transient registration would hand you a different
   object than core holds.
3. **Never register it as `IAiringScheduleEntityResolver`.** It would make a
   single-instance resolve of that interface return whichever plugin registered
   last, core would ignore it and build a second instance anyway, and any
   caching the resolver does would split between the two.

```csharp
// Only if your own code resolves it.
services.AddSingleton<MyEntityResolver>();
```

If your provider only ever schedules AniDB, TMDB or AniList entities directly
— the common case, since a plugin usually keys on IDs a metadata source
already understands — you don't need a resolver at all.
