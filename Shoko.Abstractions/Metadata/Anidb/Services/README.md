# AniDB Services

This folder defines the three services a plugin uses to talk to AniDB: the
metadata service, the MyList service, and the AVDump service.

**These are services you consume, not contracts you implement.** There is no
AniDB provider interface, nothing here is discovered by reflection, and core
registers all three itself. A plugin takes what it needs in a constructor and
lets DI hand it the one live instance:

```csharp
public class MyJob(IAnidbService anidbService, IMylistService mylistService)
{
    // ...
}
```

| Interface | What it is for |
|---|---|
| `IAnidbService` | Ban state, the local title search, refreshing an anime, tags, images, purging. |
| `IMylistService` | Everything to do with the user's AniDB MyList: read, add, update, remove, sync. |
| `IAnidbAvdumpService` | Driving AVDump over local files. Core resolves this to the same object as `IAnidbService`, so the two are interchangeable as far as lifetime goes. |

---

## Read the cache before you call anything

Almost everything AniDB knows about an anime is already in the local database,
and reading it costs nothing. None of the following touches the network:

```csharp
// From a shoko series, straight to the AniDB record behind it.
IAnidbAnime anime = shokoSeries.AnidbAnime;

// Or by AniDB anime ID, through IMetadataService.
var series = metadataService.GetSeriesByProviderID(anidbAnimeID, IMetadataService.ProviderName.AniDB);

// Episodes come along the same way.
IAnidbEpisode anidbEpisode = shokoEpisode.AnidbEpisode;
```

`IAnidbAnime.Resources` is worth knowing about: it carries the external IDs
AniDB tracks for an anime, already turned into `Resource` entries with a name
and a URL. That is how a plugin keyed on some *other* site's ID finds its own
id without asking anybody:

```csharp
// AniDB exposes a Syoboi title id as a CrossReference resource named "syoboi",
// with the URL https://cal.syoboi.jp/tid/{id}/time
var syoboi = anime.Resources
    .FirstOrDefault(r => r.Type == ResourceType.CrossReference && r.Name == "syoboi");
```

Alongside `"syoboi"` the same list carries `"allcinema"`, `"Anison"`,
`"bangumi"`, `".lain"`, `"AnimeNewsNetwork"`, `"VNDB"` and `"MyAnimeList"` as
cross-references, the official sites as `Website` resources, Wikipedia as
`Metadata`, and Crunchyroll, Funimation and HiDive as `Streaming`. Parse the
URL for the id; the shipping Syoboi plugin does exactly this rather than
carrying a mapping list of its own.

Call the services below when the cached copy is missing or stale, or when you
need something the cache cannot answer.

The similar anime AniDB's users vote on are part of that cache, read from
`IAnidbAnime.Suggestions`. See
[relations and suggestions](../../README.md).

---

## `IAnidbService`

### Ban state, first

AniDB bans clients that misbehave, and a ban is measured in hours, not seconds.
The service publishes the current state so a plugin can get out of the way:

| Member | Meaning |
|---|---|
| `IsAnidbHttpBanned` | The HTTP API is currently refusing us. |
| `IsAnidbUdpBanned` | The UDP API is currently refusing us. |
| `IsAnidbUdpReachable` | The UDP connection is alive and the network is up. Not a ban check; a ban can be in effect while this is `true`. |
| `LastHttpBanEventArgs`, `LastUdpBanEventArgs` | The last ban event, whether or not it is still in effect. |
| `BanOccurred`, `BanExpired` | Events for both transports, so a long-running plugin can pause and resume without polling. |

```csharp
anidbService.BanOccurred += (_, e) => _paused = true;
anidbService.BanExpired  += (_, e) => _paused = false;
```

### Searching titles is local

```csharp
IReadOnlyList<IAnidbAnimeSearchResult> results = anidbService.SearchAnime("cowboy bebop", fuzzy: true);
IAnidbAnimeSearchResult? exact = anidbService.SearchAnimeByID(23);
```

Both search the locally cached AniDB title dump, not AniDB itself, despite the
"remote search" framing in the older parts of the API. They are synchronous,
they make no request, and they cannot get you banned. Use them freely.

### Refreshing an anime

Two shapes, and the difference matters:

| Member | Behaviour |
|---|---|
| `RefreshAnimeByID(id, method, ct)` / `RefreshAnime(anime, method, ct)` | Does the work inline and awaits it. Returns the refreshed `IAnidbAnime` (`RefreshAnimeByID` returns `null` when the anime does not exist on AniDB). Throws `AnidbHttpBannedException`, carrying `ExpiresAt`, when a ban is in the way. |
| `ScheduleRefreshOfAnimeByID(id, method, prioritize)` / `ScheduleRefreshOfAnime(anime, method, prioritize)` | Queues the refresh job and returns. The queue's own AniDB acquisition filters and concurrency group then apply. Called from inside a job, the new job runs straight after that one, ahead of everything already waiting; called from anywhere else, it goes to the front of the queue. |

**Prefer the scheduled form for anything that is not a direct response to a
user action.** A queued job waits for a ban to lift and respects the HTTP
bulkhead; an inline call throws in your face and has to be handled.

`AnidbRefreshMethod` is a `[Flags]` enum, and `Auto` (the default) is not a
flag combination at all: it means "work it out from the server settings",
which is usually what you want. Setting anything explicitly opts out of that
entirely, so spell out every flag you need:

| Flag | Effect |
|---|---|
| `Remote` | Allow the AniDB HTTP API. |
| `Cache` | Allow the local AniDB HTTP cache. |
| `PreferCacheOverRemote` | Try the cache before the network. |
| `DeferToRemoteIfUnsuccessful` | Fall back to a later remote update if this one fails. |
| `IgnoreTimeCheck` | Refresh even though the record was updated recently. |
| `IgnoreHttpBans` | Ask anyway during a ban. Do not reach for this. |
| `DownloadRelations` | Follow related anime, to the configured depth. |
| `CreateShokoSeries` | Create the `IShokoSeries` if there isn't one. |
| `SkipSupplementaryUpdate` | Skip the TMDB and AniList follow-up. |
| `Default` | `Cache` plus `Remote` plus `DeferToRemoteIfUnsuccessful`. |
| `None` | Do nothing. Both refresh shapes return without work when neither `Cache` nor `Remote` is set. |

### The rest

| Member | Notes |
|---|---|
| `GetAllTags(topLevelOnly)` | Every AniDB tag in the local database. Local read, no network. |
| `ScheduleImagesForAnimeByID(id, onlyPosters, forceDownload, prioritize)` | Queues the image records, cross-references and downloads for an anime. |
| `PurgeAllUnusedAnime()` | Queues one `PurgeAniDBAnimeJob` per AniDB anime no longer linked to a shoko series. It schedules the work and returns; nothing is dropped by the time the call completes. |
| `PurgeAnimeByID(id, removeFromMylist)` / `SchedulePurgeOfAnimeByID(...)` | Inline and queued purge of one anime. `removeFromMylist` defaults to `true`, so a careless call removes the user's MyList entries too. |
| `AnidbHttpApiBaseUrlOverride`, `AnidbCdnBaseUrlOverride`, `AnidbTitleCacheUrlOverride` | Settable overrides, written straight to the server settings and saved. Setting one to `null`, an empty string, or the default value clears it. These exist for mirrors and for testing; a plugin changing them changes them for the whole server. |

---

## Rate limits and etiquette

AniDB is the one source in Shoko where getting this wrong has a lasting cost.
Its own wiki sets the UDP budget: **no more than one packet every two seconds**
short-term, and **no more than one every four seconds** sustained. HTTP has its
own, separate budget. Shoko enforces both centrally through `AniDbRateLimiter`
(one instance per transport, both configurable under `AniDb.UDPRateLimit` and
`AniDb.HTTPRateLimit`), so a plugin does not implement its own limiter, but it
can still queue up far more work than the budget can drain.

- **Go through the queue.** Jobs carry `[AniDBUdpRateLimited]` and
  `[AniDBHttpRateLimited]`, which hold a job back entirely while that transport
  is banned or the network is down, and `GetAniDBAnimeJob` sits in the
  `AniDB_HTTP` concurrency group so only one HTTP fetch runs at a time. A
  `Schedule…` call inherits all of that. A direct `await RefreshAnimeByID(…)`
  is still paced by the rate limiter, but it will block your own code for as
  long as that takes, and it throws rather than waiting when a ban is in
  effect.
- **Never loop a refresh over a library.** A thousand anime at the sustained
  UDP rate is over an hour of solid traffic, and the same loop against HTTP is
  how bans happen. Schedule the work and let the queue pace it, and bear in
  mind that every scheduled refresh jumps ahead of what is already waiting, so
  a thousand of them push the user's own imports to the back.
- **Check `IsAnidbHttpBanned` / `IsAnidbUdpBanned` before starting a sweep**,
  and subscribe to `BanOccurred` / `BanExpired` to stop and resume one already
  running.
- **Prefer the local cache and the title dump.** Neither costs an AniDB
  request.

---

## `IMylistService`

The AniDB MyList is the user's own list of files and episodes, and it is the
one part of AniDB a plugin can *write* to. Every MyList operation, immediate or
queued, goes through this service.

### Two tiers of entry

A **file entry** records one specific release the user has, keyed by file id
(fid) or by ED2K hash plus size. A **generic entry** records an episode the
user has watched with no file behind it, keyed by anime id, episode type and
episode number. Most methods come in overloads for each key shape, plus a
`ulong listID` (lid) overload for an entry you already hold.

`GetEntriesForVideoAsync(video, …)` bridges the two: a video with an AniDB
release comes back as its file entry, while a manually linked video comes back
as the generic entries of each episode it is linked to.

### `MylistFetchMode` decides what a call is allowed to do

Every method takes one, and it is a `[Flags]` enum:

| Flag | Effect |
|---|---|
| `Auto` | Use the configured default. This is what every method defaults to, and what the `FetchMode` property holds. |
| `None` | No cache lookups and no remote fetches. |
| `Cache` | Serve from the local cache when possible. |
| `Http` | Fetch the whole MyList over HTTP, refreshing the cache. |
| `Udp` | Fetch single entries over UDP. |
| `IgnoreTimeCheck` | Bypass the freshness gate on the remote fetch, and the schedule gate during a sync. |
| `Default` | `Http` plus `Cache` plus `Udp`. |

Only HTTP is time-gated by cache freshness; UDP never is. Dropping `Cache` from
a write's fetch mode is the way to force the command to AniDB even when the
cached entry already looks right, which is occasionally what you want after an
out-of-band change and usually just wasted requests.

`FetchMode` (the property) is the server-wide default the `Auto` value resolves
to. It is settable, and setting it to `Auto` throws `ArgumentOutOfRangeException`.
A plugin should pass the mode it wants per call rather than change the default
for everyone.

### `…Async` versus `Schedule…`

Every operation exists twice. `AddEntryAsync`, `UpdateEntryAsync`,
`RemoveEntryAsync`, `SyncAsync` and friends do the work now and return the
result. `ScheduleAddEntry`, `ScheduleUpdateEntry`, `ScheduleRemoveEntry`,
`ScheduleDisposeEntry` and `ScheduleSync` queue a job and return as soon as it
is queued, taking a `prioritize` flag instead of a cancellation token. Since
MyList traffic is UDP traffic, the queued form is the right default for bulk
work.

`ScheduleDisposeEntry` / `ScheduleDisposeVideo` are the "the local file is
gone" path, and take a `MylistDeleteType` deciding what that means on AniDB:
`Delete` removes the entry, `DeleteLocalOnly` leaves it alone, and
`MarkDeleted` / `MarkExternalStorage` / `MarkUnknown` / `MarkDisk` set the
storage state instead.

### Syncing, and planning a sync first

`SyncAsync` reconciles the whole local library against the MyList: adding
missing files, importing or exporting watched states, and removing entries for
files that are gone. Two narrower overloads confine it to a set of `IVideo` or
a set of `IShokoEpisode`. Nothing is ever removed by the video-scoped overload,
since an entry is only removed when its local file is gone and a video passed
in plainly is not.

**Only one sync runs at a time.** A second call returns `null` as soon as it
notices, rather than queueing behind the first.

`MylistSyncOptions` overrides the server settings for one run, and every field
is nullable, with `null` falling back to the setting. Two fields describe the
*call* rather than the server and have no setting behind them:

- **`PlanOnly`** works out everything the sync would do and returns it as
  `MylistSyncResult.Plan` without doing any of it. The MyList is still fetched,
  because the plan is derived from it, but nothing local is written and nothing
  is sent to AniDB.
- **`IncludeNoOperations`** adds what the sync looked at and found nothing to do
  for, as `NoOperation` steps. Off by default. Turning it on makes the plan as
  long as the MyList and the library put together, which is the point when
  auditing and dead weight otherwise.

```csharp
// 1. Work out what a sync would do, without doing any of it.
var preview = await mylistService.SyncAsync(new MylistSyncOptions { PlanOnly = true }, cancellationToken);
if (preview is null)
    return; // a sync was already running

// 2. Show it, narrow it, drop the steps this plugin does not want.
var narrowed = preview.Plan with
{
    Actions = [.. preview.Plan.Actions.Where(action => action.Kind is not MylistSyncActionKind.NoOperation)],
};

// 3. Carry out what is left.
var result = await mylistService.ApplySyncPlanAsync(narrowed, cancellationToken);
```

`ScheduleSync` refuses a `PlanOnly` run with `ArgumentException`, because a
queued job has nowhere to return a plan to.

`ApplySyncPlanAsync` checks the *whole* plan before running any of it, since a
plan need not have come from a sync at all. A step naming a MyList entry that
does not belong to the file or episode it names, or naming nothing its kind can
act on, fails the lot with a `GenericValidationException` reporting every bad
step keyed by its index in `Actions`. Past that check the steps are independent:
one that fails unexpectedly is logged and skipped rather than abandoning the
rest.

**A plan is a snapshot, not a set of instructions to re-derive.** Each
`MylistSyncAction` carries the values it was built with (`WatchedAt`, `State`
and `DeleteType`), and applying it writes those values rather than re-reading
current state. Replaying a stale plan therefore pushes stale data: a watched
date the user has since changed, or a state that no longer matches. Build a
plan and apply it in the same pass. `MylistSyncPlan.CreatedAt` is there so you
can check its age before deciding to apply it.

`MylistSyncTargets` picks which tiers to reconcile (`Videos`, `Episodes`, or
`All`), and `MylistWatchedEpisodeMode` decides how a locally watched episode is
recorded when the MyList only covers it by file entries.

---

## `IAnidbAvdumpService`

AVDump submits a file's hashes and media info to AniDB for manual entry. It is
an on-demand utility, unrelated to how unrecognised files are handled, and it
only runs when a user or a plugin asks for it.

```csharp
if (!avdumpService.IsAvdumpInstalled)
    avdumpService.UpdateAvdump();

avdumpService.AvdumpEvent += (_, e) => { /* progress, results, failures */ };
await avdumpService.ScheduleAvdumpVideos(video);
```

| Member | Notes |
|---|---|
| `IsAvdumpInstalled` | `true` implies `InstalledAvdumpVersion` is non-null. |
| `InstalledAvdumpVersion`, `AvailableAvdumpVersion` | What is installed, and what Shoko knows it could install. |
| `UpdateAvdump(force)` | Installs or updates the component. Returns whether it did. |
| `AvdumpVideos(…)` / `AvdumpVideoFiles(…)` | Run a session now, over `IVideo` or `IVideoFile`. |
| `ScheduleAvdumpVideos(…)` / `ScheduleAvdumpVideoFiles(…)` | Queue the session instead. The returned task completes when the *job is queued*, not when the dump finishes. |
| `AvdumpEvent` | The only way to get results out. All four dump methods report through it. |

Note the returned tasks: none of them carry the dump's outcome. Subscribe to
`AvdumpEvent` before starting a session, or you will not see what happened.

---

## Gotchas

- **`PurgeAnimeByID` and `SchedulePurgeOfAnimeByID` default `removeFromMylist`
  to `true`.** Purging local metadata will also reach into the user's MyList
  unless you say otherwise.
- **`RefreshAnimeByID` returns `null` for an anime that does not exist on
  AniDB**, while `RefreshAnime` returns the anime you passed in when the
  refresh yields nothing. Neither `null` means "failed".
- **`AnidbRefreshMethod.Auto` is not a flag.** Combining it with real flags
  does not do what it looks like; pick `Auto` or spell the flags out.
- **The URL overrides write to server settings and save immediately.** They are
  global, not scoped to your plugin.
- **Watch the transports separately.** HTTP and UDP have independent budgets,
  independent bans, and independent state on the service. Being clear on UDP
  says nothing about HTTP.
