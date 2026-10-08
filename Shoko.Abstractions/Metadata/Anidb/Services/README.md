# AniDB Services

Three services a plugin consumes to talk to AniDB. Nothing here is
implemented by a plugin or discovered by reflection; the core registers them,
so take them through a constructor:

```csharp
public class MyJob(IAnidbService anidbService, IMylistService mylistService)
{
    // ...
}
```

| Interface | What it is for |
|---|---|
| `IAnidbService` | Ban state, the local title search, the cached anime list, start season overrides, refreshing an anime, tags, images, purging. |
| `IMylistService` | Everything to do with the user's AniDB MyList: read, add, update, remove, sync. |
| `IAnidbAvdumpService` | Driving AVDump over local files. The same object as `IAnidbService`. |

---

## Read the cache first

Almost everything AniDB knows about an anime is already in the local database,
and none of this touches the network:

```csharp
// From a shoko series, straight to the AniDB record behind it.
IAnidbAnime anime = shokoSeries.AnidbAnime;

// Or by AniDB anime ID, through IMetadataService.
var series = metadataService.GetSeries(new MetadataGuid(MetadataSource.AniDB, MetadataEntityType.Series, anidbAnimeID.ToString()));

// Episodes come along the same way.
IAnidbEpisode anidbEpisode = shokoEpisode.AnidbEpisode;
```

`IAnidbAnime.Resources` carries the external IDs AniDB tracks for an anime as
`Resource` entries, which is how a plugin keyed on another site's ID finds it:

```csharp
// AniDB exposes a Syoboi title id as a CrossReference resource named "syoboi",
// with the URL https://cal.syoboi.jp/tid/{id}/time
var syoboi = anime.Resources
    .FirstOrDefault(r => r.Type == ResourceType.CrossReference && r.Name == "syoboi");
```

The list holds every resource AniDB lists, in AniDB's order: other databases
(`"allcinema"`, `"Anison"`, `"bangumi"`, `".lain"`, `"AnimeNewsNetwork"`,
`"VNDB"`, `"MyAnimeList"`, `"IMDb"`, `"TMDB"`, `"Douban"`) as
`CrossReference`, official sites as `Website`, Wikipedia and Baidu Baike as
`Metadata`, and streaming services as `Streaming`. A type Shoko does not know
comes as `Other`. `IAnidbEpisode.Resources` does the same for an episode. Read
the bare ID from `ID` rather than parsing the URL.

AniDB's similar anime are in the cache too, as `IAnidbAnime.Suggestions` (see
[relations and suggestions](../../README.md#reading-relations-and-suggestions)).
Call the services below when the cache is missing, stale or cannot answer.

---

## `IAnidbService`

### Ban state

AniDB bans misbehaving clients for hours. The core reports it through the
suspension service, as the "AniDB UDP" and "AniDB HTTP" suspension providers:
a ban is `Banned` (with its end, liftable by an admin), and on UDP an overload
backoff is `Overloaded`, an invalid session `SessionInvalid` and refused
credentials `AuthenticationFailed`. Read them through `ISuspensionService`
([Suspensions](../../../Connectivity/Services/README.md#suspensions)) and
subscribe to `SuspensionChanged` rather than polling.

The ban members below still work, read from the same state, but are obsolete:

| Member | Meaning |
|---|---|
| `IsAnidbHttpBanned` | The HTTP API is currently refusing us. |
| `IsAnidbUdpBanned` | The UDP API is currently refusing us. |
| `LastHttpBanEventArgs`, `LastUdpBanEventArgs` | The last ban event, whether or not it is still in effect. |
| `BanOccurred`, `BanExpired` | Events for both transports. |

`IsAnidbUdpReachable` is not obsolete: the UDP connection is alive and the
network is up. It is not a ban check; a ban can be in effect while it is
`true`.

### Searching titles is local

```csharp
IReadOnlyList<IAnidbAnimeSearchResult> results = anidbService.SearchAnime("cowboy bebop", fuzzy: true);
IAnidbAnimeSearchResult? exact = anidbService.SearchAnimeByID(23);
```

Both search the locally cached AniDB title dump, not AniDB, so they make no
request and cannot get you banned.

### Listing the cached anime

`GetCachedAnime(options)` lists the AniDB anime already in the local cache,
in the collection or not, which is what a season view is built from. Each
entry pairs the anime with its Shoko series, or `null` when it is not in the
collection:

```csharp
var fall = anidbService.GetCachedAnime(new AnidbAnimeListOptions
{
    Seasons = [(2026, YearlySeason.Fall)],
    Types = [AnimeType.TVSeries],
    InCollection = InclusionFilter.False,       // only anime without a Shoko series
    IncludeRestricted = InclusionFilter.False,  // leave out restricted anime
    User = user,                                // and what this user may not see
});
```

Every filter left unset lets everything through. An anime is in a season
by the rule `IWithYearlySeasons` describes, on the regular broadcast dates of
its episodes: it starts in the season of its first regular episode, with the
type's lead-in, an early premiere moving it to the next season and a batch
drop taking no lead-in, then is only in the calendar quarters holding one of
its dated regular episodes. Once it has an end date, only those up to the
fourth from the end count, so the last three of a finished run never carry it
into the next season. The stored normal airings of the regular episodes AniDB
gives no date, or does not list yet, count as dated episodes; estimates never
do. An anime without dated regular episodes stays in the season of its start
date.
AniDB sends no episode air dates before 1970, so an anime starting earlier
starts on its own date. Seasons after the one following the
season under way are yet to be decided and match nothing. The list comes by air date with a season filter, else by preferred
title, unless `OrderBy` says otherwise. `Filter` takes an `IFilter` from the
filtering system and keeps the anime of the Shoko series it passes for `User`,
so anime outside the collection are left out, while series without local files
stay unless the filter leaves them out. It is evaluated once per read, and a
filter with a sorting expression gives the list its own order in place of
`OrderBy`. `ChannelIDs` keeps the anime with a
stored airing on those channels in the season (its calendar quarter), or one
still to come from the season under way on; airings are only kept for the
airing schedule service's retention window, so old seasons hold fewer. `At`
sets the time the listing is as of, in UTC, which decides the season under
way (by the server's time zone) and what is still to come; unset, it is now.
The
next airing of each, as a season view shows it, comes from
`IAiringScheduleService.GetAiringsForSeries` over the whole list in one read
(see [`../../Airing/README.md`](../../Airing/README.md#querying-airings)).

`GetCachedAnimeSeasons(options, includeImages)` gives the seasons those anime
are in by the same rule, newest first, with a count each and the season under
way always listed and flagged, ignoring the season filter and the order. With
`includeImages`, each season also carries a `Poster` and a `Backdrop`, both
from one anime: among those starting in the season, the best by Bayesian
weighted rating that has a poster. When none has one, the anime carried over
from the latest season before it are ranked the same way, then the season
before those, and so on. The weighted rating is
`(v * R + m * C) / (v + m)`, with `R` and `v` the anime's AniDB rating and
votes, `C` the mean rating of the rated anime starting in the same season and
`m` the median of their votes, at least 50. Ties go to the earlier start, then the lower
AniDB ID, so a season yet to air shows its earliest starter.

### Start season overrides

The rule above places most anime where AniDB itself does, and the rest can be
set by hand. A start season override is kept by AniDB anime ID, so it holds for
anime outside the collection, for anime not in the local cache yet (it applies
once the anime is fetched), and through a series being removed and added again:

```csharp
AnidbStartSeasonOverride set = anidbService.SetStartSeasonOverride(17860, 2024, YearlySeason.Winter);
AnidbStartSeasonOverride? current = anidbService.GetStartSeasonOverride(17860);
IReadOnlyList<AnidbStartSeasonOverride> all = anidbService.GetStartSeasonOverrides();
bool removed = anidbService.RemoveStartSeasonOverride(17860);
```

Only the start is overridden. The anime starts in that season and is then in
every season the rule places it in after it: a later start drops the computed
seasons up to it, an earlier one keeps them all with nothing filled in
between, and an anime with no dates to go by is in the overridden season alone.
It applies everywhere the rule does: `IWithYearlySeasons` on the AniDB anime
and on its Shoko series and groups, the filters, `GetCachedAnime`'s season
filter, `GetCachedAnimeSeasons`, and `IAiringCalendarService`, whose
`SeasonAnimeEntry` flags it with `IsStartSeasonOverridden`.

`SetStartSeasonOverride` throws `ArgumentOutOfRangeException` for an AniDB
anime ID below 1, a year outside 1900 to 9999 or an undefined season, and
records the current actor's user as `UserID` (`null` for the system). Setting
the season already set changes nothing. `StartSeasonOverrideChanged` carries
the `Previous` and `Current` override (`null` when there was none, or none is
left) and the `Actor`, and is not raised for a set that changed nothing.

### Refreshing an anime

| Member | Behaviour |
|---|---|
| `RefreshAnimeByID(id, method, ct)` / `RefreshAnime(anime, method, ct)` | Does the work inline and awaits it. Returns the refreshed `IAnidbAnime` (`RefreshAnimeByID` returns `null` when the anime does not exist on AniDB). Throws `AnidbHttpBannedException`, carrying `ExpiresAt`, when a ban is in the way. |
| `ScheduleRefreshOfAnimeByID(id, method, prioritize)` / `ScheduleRefreshOfAnime(anime, method, prioritize)` | Queues the refresh job and returns. The queue's own AniDB acquisition filters and concurrency group then apply. Called from inside a job, the new job runs straight after that one, ahead of everything already waiting; called from anywhere else, it goes to the front of the queue. |

**Prefer the scheduled form** for anything that is not a direct response to
a user: a queued job waits for a ban to lift, while an inline call throws.

`AnidbRefreshMethod` is a `[Flags]` enum whose `Auto` default means "work it
out from the server settings". Setting anything explicitly opts out of that,
so spell out every flag you need:

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
| `SkipSupplementaryUpdate` | Skip asking the metadata providers afterwards. |
| `Default` | `Cache` plus `Remote` plus `DeferToRemoteIfUnsuccessful`. |
| `None` | Do nothing. Both refresh shapes return without work when neither `Cache` nor `Remote` is set. |

### The rest

| Member | Notes |
|---|---|
| `GetAllTags(topLevelOnly)` | Every AniDB tag in the local database. Local read, no network. |
| `ScheduleImagesForAnimeByID(id, onlyPosters, forceDownload, prioritize)` | Queues the image records, cross-references and downloads for an anime. |
| `PurgeAllUnusedAnime()` | Queues one `PurgeAniDBAnimeJob` per AniDB anime no longer linked to a Shoko series, and returns. |
| `PurgeAnimeByID(id, removeFromMylist)` / `SchedulePurgeOfAnimeByID(...)` | Inline and queued purge of one anime. `removeFromMylist` defaults to `true`, so a careless call removes the user's MyList entries too. |
| `AnidbHttpApiBaseUrlOverride`, `AnidbCdnBaseUrlOverride`, `AnidbTitleCacheUrlOverride` | Overrides for mirrors and testing, saved straight to the server settings for the whole server. `null`, empty or the default clears one. |

---

## Rate limits and etiquette

Getting this wrong with AniDB has a lasting cost. Its UDP budget is **one
packet every two seconds** short-term and **one every four seconds**
sustained, and HTTP has a separate one. `AniDbRateLimiter` enforces both
(`AniDb.UDPRateLimit`, `AniDb.HTTPRateLimit`), but a plugin can still queue
far more work than the budget drains.

- **Go through the queue.** `[AniDBUdpRateLimited]` and
  `[AniDBHttpRateLimited]` hold a job back while its transport is banned or the
  network is down, and `GetAniDBAnimeJob` runs one HTTP fetch at a time. A
  direct `await RefreshAnimeByID(…)` blocks your code for as long as pacing
  takes and throws during a ban.
- **Never loop a refresh over a library.** A thousand anime is over an hour of
  UDP traffic, and every scheduled refresh jumps ahead of what is waiting, so
  the user's own imports go to the back.
- **Check `IsAnidbHttpBanned` / `IsAnidbUdpBanned` before starting a sweep**,
  and subscribe to `BanOccurred` / `BanExpired` to stop and resume one already
  running.
- **Prefer the local cache and the title dump.** Neither costs an AniDB
  request.

---

## `IMylistService`

The MyList is the user's list of files and episodes, and the one part of
AniDB a plugin can *write* to.

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

Only HTTP is gated by cache freshness. Dropping `Cache` from a write's mode
forces the command to AniDB even when the cached entry looks right. The
`FetchMode` property is the server-wide default `Auto` resolves to (setting it
to `Auto` throws); pass a mode per call rather than change it for everyone.

### `…Async` versus `Schedule…`

Every operation exists twice: `AddEntryAsync`, `UpdateEntryAsync`,
`RemoveEntryAsync`, `SyncAsync` and friends do the work now;
`ScheduleAddEntry`, `ScheduleUpdateEntry`, `ScheduleRemoveEntry`,
`ScheduleDisposeEntry` and `ScheduleSync` queue it (with `prioritize` instead
of a cancellation token). MyList traffic is UDP, so queue bulk work.

`ScheduleDisposeEntry` / `ScheduleDisposeVideo` are the "the local file is
gone" path, and take a `MylistDeleteType` deciding what that means on AniDB:
`Delete` removes the entry, `DeleteLocalOnly` leaves it alone, and
`MarkDeleted` / `MarkExternalStorage` / `MarkUnknown` / `MarkDisk` set the
storage state instead.

### Syncing, and planning a sync first

`SyncAsync` reconciles the library against the MyList: adding missing files,
importing or exporting watched states, and removing entries whose files are
gone. Overloads narrow it to a set of `IVideo` (which never removes anything)
or `IShokoEpisode`. **Only one sync runs at a time**; a second call returns
`null`.

`MylistSyncOptions` overrides the server settings for one run (`null` falls
back to the setting), plus two fields about the call itself: **`PlanOnly`**
returns what the sync would do as `MylistSyncResult.Plan`, doing none of it
(the MyList is still fetched), and **`IncludeNoOperations`** adds the
`NoOperation` steps, useful when auditing.

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

`ApplySyncPlanAsync` validates the whole plan first, since it need not come
from a sync: a bad step fails the lot with a `GenericValidationException` keyed
by its index in `Actions`. After that, a step that fails is logged and skipped.

**A plan is a snapshot.** Each `MylistSyncAction` writes the values it was
built with (`WatchedAt`, `State`, `DeleteType`), so a stale plan pushes stale
data. Apply a plan in the same pass, or check `MylistSyncPlan.CreatedAt`.

`MylistSyncTargets` picks the tiers to reconcile, and
`MylistWatchedEpisodeMode` how a watched episode is recorded when the MyList
covers it only by file entries.

---

## `IAnidbAvdumpService`

AVDump submits a file's hashes and media info to AniDB for manual entry, on
request only.

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

No returned task carries the dump's outcome: subscribe to `AvdumpEvent`
before starting a session.

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
- **Watch the transports separately.** HTTP and UDP have independent budgets,
  bans and state; being clear on UDP says nothing about HTTP.
