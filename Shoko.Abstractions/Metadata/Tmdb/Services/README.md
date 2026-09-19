# TMDB Services

This folder defines the three services a plugin uses to reach The Movie
Database: search, metadata, and linking.

**These are services you consume, not contracts you implement.** There is no
TMDB provider interface and nothing here is discovered by reflection. Core
registers all three as singletons during startup, so a plugin takes what it
needs in a constructor and DI hands it the live instance:

```csharp
public class MyJob(ITmdbSearchService searchService, ITmdbMetadataService metadataService)
{
    // ...
}
```

| Interface | What it is for |
|---|---|
| `ITmdbSearchService` | Searching TMDB for shows and movies, and the automatic match used when linking a series. |
| `ITmdbMetadataService` | Fetching and refreshing shows and movies, genres, purging, and the rate limiter's pause state. |
| `ITmdbLinkingService` | Creating and removing the AniDB ↔ TMDB links, at anime, episode and movie level. |

---

## Read the cache before you call anything

TMDB entities that have been fetched once are read-only local caches, shaped
like the TMDB response schema, and reading them costs no request:

```csharp
IReadOnlyList<ITmdbShow> shows = shokoSeries.TmdbShows;
IReadOnlyList<ITmdbSeason> seasons = shokoSeries.TmdbSeasons;
IReadOnlyList<ITmdbMovie> movies = shokoSeries.TmdbMovies;

IReadOnlyList<ITmdbEpisode> episodes = shokoEpisode.TmdbEpisodes;
IReadOnlyList<ITmdbMovie> episodeMovies = shokoEpisode.TmdbMovies;

// And the cross-references themselves, when the mapping matters as much as
// the entity: TmdbShowCrossReferences, TmdbSeasonCrossReferences,
// TmdbEpisodeCrossReferences, TmdbMovieCrossReferences.
```

By ID, through `IMetadataService`:

```csharp
ISeries? show = metadataService.GetSeriesByProviderID(tmdbShowID, IMetadataService.ProviderName.TMDB);
IMovie? movie = metadataService.GetMovieByProviderID(tmdbMovieID, IMetadataService.ProviderName.TMDB);
```

`ProviderName` is nested inside `IMetadataService`, so it is spelled
`IMetadataService.ProviderName.TMDB` unless you have a `using static` for the
interface. It resolves shows on the series methods and movies on the movie
methods; there is no single call that covers both.

Call the services below when the cached copy is missing or stale, or when you
need TMDB itself to answer something.

---

## The shape of a TMDB link

One anime can match several TMDB shows (a split-cour series on TMDB), and one
TMDB show can match several anime. Movies link at the **episode** level rather
than the series level, because an OVA or a film is usually one episode of an
AniDB anime. That asymmetry runs through the whole linking service:

| Link | Reached by |
|---|---|
| `CrossRef_AniDB_TMDB_Show` | `AddShowLink(anidbAnimeId, tmdbShowId, …)` |
| `CrossRef_AniDB_TMDB_Movie` | `AddMovieLinkForEpisode(anidbEpisodeId, tmdbMovieId, …)` |
| `CrossRef_AniDB_TMDB_Episode` | `SetEpisodeLink(…)`, or the auto-matcher |

---

## `ITmdbSearchService`

```csharp
var (shows, totalShows) = await searchService.SearchShows("cowboy bebop", includeRestricted: false, year: 1998, page: 1, pageSize: 20);
var (movies, totalMovies) = await searchService.SearchMovies("cowboy bebop");
```

Both go to TMDB and return one page plus the total count. `page` is 1-based,
`pageSize` defaults to 6, and `year` filters on the release date for movies and
the first air date for shows. `includeRestricted` opts adult titles in; it is
off by default.

`SearchForAutoMatch(IAnidbAnime)` returns the ranked `ITmdbAutoSearchResult`
candidates Shoko's own auto-linker would consider. Each result carries a
`MatchRating`, and `IsMovie` says which half of the result to read: `TmdbMovie`
plus `AnidbEpisode` for a movie, `TmdbShow` for a show. Both TMDB properties
are nullable, so branch on `IsMovie` rather than null-checking one of them.

---

## `ITmdbMetadataService`

### Fetching and refreshing

| Member | Behaviour |
|---|---|
| `UpdateShow(options)` / `UpdateMovie(options)` | Does the fetch inline and awaits it. Returns whether anything was updated. |
| `ScheduleUpdateOfShow(options)` / `ScheduleUpdateOfMovie(options)` | Queues the update job and returns. Called from inside a job, the new job runs straight after that one, ahead of everything already waiting; called from anywhere else, it goes to the front of the queue. |
| `UpdateAllShows(force, downloadImages)` / `UpdateAllMovies(force, saveImages)` | Despite the names, these **queue** one job per existing cross-reference. They return once the jobs are queued, not once they have run. |
| `ScheduleDownloadAllShowImages(id, force)` / `ScheduleDownloadAllMovieImages(id, force)` | Queues just the images for one entity. |

`TmdbShowUpdateOptions`:

| Field | Notes |
|---|---|
| `ShowId` | Required. A `0` is a no-op. |
| `ForceRefresh` | Refresh even though the record was updated recently. |
| `DownloadImages` | Download images after the update. |
| `DownloadCrewAndCast`, `DownloadAlternateOrdering`, `DownloadNetworks` | `null` follows the server setting. |
| `QuickRefresh` | Skip some of the work. |

`TmdbMovieUpdateOptions` is the same idea with `MovieId`, `ForceRefresh`,
`DownloadImages`, `DownloadCrewAndCast` and `DownloadCollections`.

### Genres

```csharp
IReadOnlyDictionary<int, string> showGenres = await metadataService.GetShowGenres();
IReadOnlyDictionary<int, string> movieGenres = await metadataService.GetMovieGenres();
```

Both map TMDB's genre IDs to their names. Each is fetched from TMDB once and
memoised for the life of the process, so calling them in a loop is free after
the first hit. They are not persisted, so the first call after a restart does
make a request.

### Purging

`PurgeShow(id)` and `PurgeMovie(id)` drop one entity locally, with
`SchedulePurgeOfShow` / `SchedulePurgeOfMovie` queueing the same.
`PurgeAllUnusedShows(olderThan)` and `PurgeAllUnusedMovies(olderThan)` find
everything no longer referenced by a cross-reference and queue a purge for
each, optionally limited to entities whose `LastUpdatedAt` predates
`olderThan`. `PurgeAllMovieCollections()` clears the cached collections.

### Matching

`ScheduleSearchForMatch(anidbId, force)` queues an auto-match search for one
AniDB anime. `ScanForMatches()` sweeps every series and queues a search for
each one still missing a match. `ScanForMatches` returns immediately and does
nothing at all when `TMDB.AutoLink` is off in the server settings, and it skips
any series whose auto-matching the user has disabled.

### Rate limit state

```csharp
var status = metadataService.GetPauseStatus();
if (status.IsPaused)
    _logger.LogInformation("TMDB paused for another {Time}", status.RemainingPauseTime);
```

`TmdbRateLimitPauseStatus` is a consistent snapshot of the 5XX circuit
breaker: `IsPaused`, and `RemainingPauseTime`, which is `null` when not paused.

---

## `ITmdbLinkingService`

### Show links

| Member | Notes |
|---|---|
| `AddShowLink(anidbAnimeId, tmdbShowId, additiveLink, matchRating)` | Creates or updates the link, then runs the episode auto-matcher and saves what it finds. |
| `RemoveShowLink(anidbAnimeId, tmdbShowId, purge)` | Removes one link. `purge` **unconditionally** queues a purge of the TMDB show itself. |
| `RemoveAllShowLinksForAnime(animeId, purge)` | Every show link for one AniDB anime. |
| `RemoveAllShowLinksForShow(showId)` | Every AniDB link to one TMDB show. |

**`purge: true` does not check whether anything else still links to the show.**
It is a bare "also purge" flag: the link is deleted, and then
`PurgeTmdbShowJob` is queued for that show ID regardless of how many other
AniDB anime still point at it. Because one TMDB show can legitimately be
linked from several anime (the split-cour case described above),
`RemoveShowLink(animeA, show, purge: true)` destroys the cached show out from
under animeB, whose own cross-reference row survives while the data it refers
to does not. Pass `purge: true` only when you already know you are removing the
last link, and prefer `purge: false` whenever you are unlinking one anime among
several. The same applies to `RemoveAllShowLinksForAnime(animeId, purge)`,
which forwards the flag to each link it removes.

### Movie links

| Member | Notes |
|---|---|
| `AddMovieLinkForEpisode(anidbEpisodeId, tmdbMovieId, additiveLink, matchRating)` | Links an AniDB **episode** to a TMDB movie. |
| `RemoveMovieLinkForEpisode(anidbEpisodeId, tmdbMovieId, purge)` | Removes one link. |
| `RemoveAllMovieLinksForEpisode(anidbEpisodeId, purge)` / `RemoveAllMovieLinksForAnime(anidbAnimeId, purge)` | Everything for one episode, or for a whole anime. |
| `RemoveAllMovieLinksForMovie(tmdbMovieId)` | Every AniDB link to one TMDB movie. |

### Episode links

| Member | Notes |
|---|---|
| `SetEpisodeLink(anidbEpisodeId, tmdbEpisodeId, additiveLink, index)` | Links one episode to another. Pass `0` as the TMDB episode ID to record an explicitly empty link. `index` orders multiple links. Returns whether the link was set. |
| `ResetAllEpisodeLinks(anidbAnimeId, allowAuto)` | Clears the episode links for an anime. `allowAuto` decides whether auto-matching may re-link them afterwards. |
| `MatchAnidbToTmdbEpisodes(anidbAnimeId, tmdbShowId, tmdbSeasonId, useExisting, saveToDatabase, useExistingOtherShows)` | Runs the episode auto-matcher and returns the cross-references it worked out. |

`MatchAnidbToTmdbEpisodes` is worth calling out: with `saveToDatabase: false`
(the default) it is a **dry run**, handing back what it would link without
writing anything, which is how a plugin previews a match before committing it.
`useExisting: true` preserves existing user-verified links, and `tmdbSeasonId`
narrows the match to one season.

### Library-wide switches

`RemoveAllLinks(removeShowLinks, removeMovieLinks)` removes every AniDB ↔ TMDB
link of the selected kinds, across the whole database. `ResetAutoLinkingState(disabled)`
re-enables (or disables) automatic linking for *all* series at once. Neither
takes a series, and both are exactly as broad as they sound.

---

## Worked example: search, link, then fetch

Linking and fetching are separate steps, and in that order. `AddShowLink`
creates the cross-reference and runs the episode matcher against whatever is
already cached; it does **not** go to TMDB for the show. A freshly linked show
Shoko has never seen therefore has nothing behind it until an update runs.
Core's own `SearchTmdbJob` does exactly this pairing:

```csharp
public async Task LinkAndFetch(IAnidbAnime anime)
{
    foreach (var result in await searchService.SearchForAutoMatch(anime))
    {
        if (result.IsMovie)
        {
            // Movies link at the episode level.
            await linkingService.AddMovieLinkForEpisode(result.AnidbEpisode!.ID, result.TmdbMovie!.ID,
                additiveLink: true, matchRating: result.MatchRating);
            await metadataService.ScheduleUpdateOfMovie(new() { MovieId = result.TmdbMovie.ID, DownloadImages = true });
        }
        else
        {
            await linkingService.AddShowLink(anime.ID, result.TmdbShow!.ID,
                additiveLink: true, matchRating: result.MatchRating);
            await metadataService.ScheduleUpdateOfShow(new() { ShowId = result.TmdbShow.ID, DownloadImages = true });
        }
    }
}
```

---

## Rate limits and etiquette

TMDB is protected by `TmdbRateLimiter`, which core owns, so a plugin does not
implement its own. Three mechanisms stack:

- **A local sliding window** smooths Shoko's own request rate under the roughly
  40 requests per second TMDB enforces.
- **A server-driven backoff** honours 429 responses.
- **A 5XX circuit breaker** pauses TMDB work when three server errors land
  within ten seconds, and escalates if they keep coming.

TMDB is far more forgiving than AniDB: there are no bans to speak of, and the
concurrency table in `CLAUDE.md` reflects that, with `SearchTmdbJob` allowed 8
concurrent workers against `AnidbProcessFileJob`'s 4. That is not a licence to
hammer it.

- **Prefer the `Schedule…` forms** for bulk work. A queued job resumes on its
  own once a pause lifts; a direct `await UpdateShow(…)` sits there. Each one
  jumps ahead of what is already waiting, though, so a long loop of them
  pushes everything else back.
- **Never loop `UpdateShow` over a library.** `UpdateAllShows` exists, and it
  queues rather than blocks, for exactly this reason.
- **Check `GetPauseStatus()` before starting a sweep of your own.** If the
  breaker is open, the upstream is already unhappy.
- **Cache the genre dictionaries** in your own code if you use them across a
  process boundary; in-process they are already memoised.

---

## Gotchas

- **`UpdateAllShows`, `UpdateAllMovies` and the `PurgeAllUnused…` methods
  schedule; they do not do.** The returned task completes when the jobs are
  queued.
- **Linking does not fetch.** Pair `AddShowLink` or `AddMovieLinkForEpisode`
  with the matching `ScheduleUpdateOf…` unless you know the entity is cached.
- **`AddShowLink` writes more than the link.** It also runs the episode
  auto-matcher and saves its results, and resets the series titles and
  overview. It is not a bare insert.
- **Removing a link disables auto-matching for that series.** `RemoveShowLink`,
  `RemoveAllShowLinksForAnime` and their movie counterparts set the series'
  auto-matching to disabled, on the assumption that a user removing a match
  does not want it found again. Use `ResetAutoLinkingState(disabled: false)` to
  undo that, remembering it applies to every series.
- **`additiveLink` defaults differ between the show and movie methods.** Pass
  it explicitly: an anime can legitimately carry several show links, and the
  wrong default silently replaces the user's work.
- **Movies hang off episodes, shows hang off anime.** Reaching for an
  anime-level movie link, or an episode-level show link, means you are on the
  wrong method.
