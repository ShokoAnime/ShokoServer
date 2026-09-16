# AniList Services

This folder defines the three services a plugin uses to reach AniList: search,
metadata, and linking.

**These are services you consume, not contracts you implement.** There is no
AniList provider interface and nothing here is discovered by reflection. Core
registers all three as singletons during startup, so a plugin takes what it
needs in a constructor and DI hands it the live instance:

```csharp
public class MyJob(IAnilistSearchService searchService, IAnilistMetadataService metadataService)
{
    // ...
}
```

| Interface | What it is for |
|---|---|
| `IAnilistSearchService` | Searching AniList, and the automatic match used when linking a series. |
| `IAnilistMetadataService` | Fetching and refreshing AniList anime, purging them, and the rate limiter's pause state. |
| `IAnilistLinkingService` | Creating and removing the AniDB ↔ AniList links, at anime and episode level. |

---

## Read the cache before you call anything

An AniList anime that has been fetched once lives in the local database, and
reading it costs no request. Everything here is a local read:

```csharp
IReadOnlyList<IAnilistAnime> anime = shokoSeries.AnilistAnime;
IReadOnlyList<IAnilistAnimeCrossReference> xrefs = shokoSeries.AnilistAnimeCrossReferences;

IReadOnlyList<IAnilistEpisode> episodes = shokoEpisode.AnilistEpisodes;
IReadOnlyList<IAnilistEpisodeCrossReference> episodeXrefs = shokoEpisode.AnilistEpisodeCrossReferences;
```

Note that `IMetadataService.ProviderName` has **no AniList member**, so
`GetSeriesByProviderID(…, ProviderName.AniList)` does not exist. Reach AniList
entities through the shoko series or episode that links to them, as above.

Call the services below when the cached copy is missing or stale, or when you
need AniList itself to answer something.

---

## `IAnilistSearchService`

```csharp
var (page, totalCount) = await searchService.SearchAnime(new AnilistSearchOptions
{
    Query = "cowboy bebop",
    Season = YearlySeason.Spring,
    SeasonYear = 1998,
    Page = 1,
    PageSize = 25,
});
```

`SearchAnime` goes to AniList's GraphQL API and returns one page plus the total
count. Every filter on `AnilistSearchOptions` maps onto a filter AniList
applies server-side, so narrowing the search costs no extra requests:

| Field | Notes |
|---|---|
| `Query` | Required. The title to search for. |
| `IncludeRestricted` | Include adult titles. Off by default. |
| `Year` | Only anime that *started airing* in this year. |
| `Season` + `SeasonYear` | AniList's own seasonal grouping, which can differ from the start date for a late-December premiere. Use these two together rather than `Year` when you mean "the Spring 1998 season". |
| `Types` | Restrict to a set of `AnimeType`. |
| `Page` | 1-based. Defaults to 1. |
| `PageSize` | Defaults to 6. AniList caps it at 50. |

`SearchForAutoMatch(IAnidbAnime)` is the other entry point: it takes an AniDB
anime and returns the ranked `IAnilistAutoSearchResult` candidates Shoko's own
auto-linker would consider, each carrying its `MatchRating`. It is what
`SearchAnilistForMatchJob` calls, and it is the right starting point for a
plugin that wants to propose links rather than invent its own matching.

---

## `IAnilistMetadataService`

### Fetching and refreshing

| Member | Behaviour |
|---|---|
| `UpdateAnime(options)` | Does the fetch inline and awaits it. Returns whether the anime was updated. |
| `ScheduleUpdateOfAnime(options)` | Queues the update job and returns. |
| `UpdateAllAnime(force, downloadImages)` | Despite the name, this **queues** one update job per existing AniDB ↔ AniList cross-reference. It returns once they are all queued, not once they have run. |
| `ScheduleDownloadAllAnimeImages(id, forceDownload)` | Queues just the images for one anime. |

`AnilistAnimeUpdateOptions`:

| Field | Notes |
|---|---|
| `AnimeId` | Required. The AniList anime ID. A `0` is a no-op. |
| `ForceRefresh` | Refresh even though the record was updated recently. |
| `DownloadImages` | Download images after the update. |
| `DownloadCharactersAndStaff` | `null` follows the server setting. |
| `QuickRefresh` | Fetch only what the linking UI needs: the anime and its episodes, skipping characters, staff, images and the automatic episode matching. A quick-fetched anime is deliberately left marked as *not yet refreshed*, so the next normal update still does the full job. |

### Purging

`PurgeAnime(id)` drops one anime locally. `SchedulePurgeOfAnime(id)` queues the
same. `PurgeAllUnusedAnime(olderThan)` finds every AniList anime no longer
referenced by a cross-reference and queues a purge job for each, optionally
limited to those whose `LastUpdatedAt` predates `olderThan`.

### Matching

`ScheduleSearchForMatch(anidbId, force)` queues an auto-match search for one
AniDB anime. `ScanForMatches()` sweeps every series and queues a search for
each one still missing a match. `ScanForMatches` returns immediately and does
nothing at all when `Anilist.AutoLink` is off in the server settings, and it
skips any series whose auto-matching the user has disabled.

### Rate limit state

```csharp
var status = metadataService.GetPauseStatus();
if (status.IsPaused)
    _logger.LogInformation("AniList paused for another {Time}", status.RemainingPauseTime);
```

`AnilistRateLimitPauseStatus` is a consistent snapshot of three things:
`IsPaused`, `RemainingPauseTime` (`null` when not paused), and
`RemainingRequests`, the server-side quota AniList last reported, which is
`null` before the first response of the session.

---

## `IAnilistLinkingService`

### Anime links

```csharp
await linkingService.AddAnimeLink(anidbAnimeId, anilistAnimeId, additiveLink: true, matchRating: MatchRating.UserVerified);
```

An anime can carry several AniList links, which is why `additiveLink` exists:
`true` adds to what is there, `false` replaces the lot. Pass it explicitly
rather than relying on the default.

| Member | Notes |
|---|---|
| `AddAnimeLink(anidbAnimeId, anilistAnimeId, additiveLink, matchRating)` | Creates or updates the link. |
| `RemoveAnimeLink(anidbAnimeId, anilistAnimeId, purge)` | Removes one link. `purge` also drops the AniList anime when nothing links to it any more. |
| `RemoveAllAnimeLinksForAnidbAnime(anidbAnimeId, purge)` | Every AniList link for one AniDB anime. |
| `RemoveAllAnimeLinksForAnilistAnime(anilistAnimeId)` | Every AniDB link to one AniList anime. |
| `RemoveAllLinks()` | Every AniDB ↔ AniList link in the database. Destructive, and exactly as broad as it sounds. |
| `ResetAutoLinkingState(disabled)` | Re-enables (or disables) automatic linking for *all* series at once. |

### Episode links

| Member | Notes |
|---|---|
| `SetEpisodeLink(anidbEpisodeId, anilistEpisodeId, additiveLink, index)` | Links one episode to another. Pass `0` as the AniList episode ID to record an explicitly empty link. `index` orders multiple links. Returns whether the link was set. |
| `ResetAllEpisodeLinks(anidbAnimeId, allowAuto)` | Clears the episode links for an anime. `allowAuto` decides whether auto-matching may re-link them afterwards. |
| `MatchAnidbToAnilistEpisodes(anidbAnimeId, anilistAnimeId, useExisting, saveToDatabase, useExistingOtherAnime)` | Runs the episode auto-matcher and returns the cross-references it worked out. |

`MatchAnidbToAnilistEpisodes` is worth calling out: with `saveToDatabase: false`
(the default) it is a **dry run**, handing back what it would link without
writing anything, which is how a plugin previews a match before committing it.
`useExisting: true` preserves existing user-verified links rather than
overwriting them.

---

## Worked example: search, link, then fetch

Linking and fetching are separate steps, and in that order. `AddAnimeLink`
creates the cross-reference and runs the episode matcher against whatever is
already cached; it does **not** go to AniList for the anime. A freshly linked
anime Shoko has never seen therefore has nothing behind it until an update
runs. Core's own `SearchAnilistForMatchJob` does exactly this pairing:

```csharp
public async Task LinkAndFetch(IAnidbAnime anime, CancellationToken cancellationToken)
{
    // 1. Ask AniList for candidates, ranked the way core ranks them.
    var candidates = await searchService.SearchForAutoMatch(anime);
    if (candidates.FirstOrDefault() is not { } best)
        return;

    // 2. Record the link. Local only; nothing is fetched here.
    await linkingService.AddAnimeLink(anime.ID, best.AnilistAnime.ID, additiveLink: true, matchRating: best.MatchRating);

    // 3. Queue the fetch that actually fills the anime in.
    await metadataService.ScheduleUpdateOfAnime(new()
    {
        AnimeId = best.AnilistAnime.ID,
        DownloadImages = true,
    });
}
```

---

## Rate limits and etiquette

AniList is protected by `AnilistRateLimiter`, which core owns, so a plugin does
not implement its own. Three mechanisms stack:

- **A local sliding window** smooths Shoko's own request rate, configurable in
  the server settings.
- **A server-driven backoff** honours 429 responses and the per-response quota
  headers, which is where `AnilistRateLimitPauseStatus.RemainingRequests` comes
  from.
- **A 5XX circuit breaker** pauses every AniList job while the upstream is
  unhealthy. Unlike the TMDB breaker it trips on the *very first* server error,
  because AniList has been fragile and a client that keeps knocking during an
  outage makes it worse.

Practical consequences for a plugin:

- **Prefer the `Schedule…` forms.** A queued job re-queues and resumes on its
  own once a pause lifts, so nothing is lost by waiting. A direct
  `await UpdateAnime(…)` will simply sit there for the duration.
- **Never loop `UpdateAnime` over a library.** `UpdateAllAnime` exists, and it
  queues rather than blocks, for exactly this reason.
- **Check `GetPauseStatus()` before starting a sweep of your own**, and use
  `RemainingRequests` to back off before AniList has to tell you to.
- **Narrow searches with the options rather than by paging and filtering
  locally.** Every field maps to a server-side filter, so a narrower query is
  genuinely cheaper.

---

## Gotchas

- **`UpdateAllAnime` and `PurgeAllUnusedAnime` schedule; they do not do.** The
  returned task completes when the jobs are queued.
- **Linking does not fetch.** Pair `AddAnimeLink` with a
  `ScheduleUpdateOfAnime` unless you know the anime is already cached.
- **`QuickRefresh` leaves the anime marked unrefreshed.** That is deliberate,
  not a bug, so the full update still happens later. Do not use it as a cheap
  general-purpose fetch.
- **`RemoveAllLinks()` and `ResetAutoLinkingState(…)` are library-wide.**
  Neither takes a series.
- **Pass `additiveLink` explicitly.** An anime can legitimately carry several
  AniList links, and the wrong default silently replaces the user's work.
