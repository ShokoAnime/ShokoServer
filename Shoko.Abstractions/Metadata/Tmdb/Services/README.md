# TMDB Services

Three services a plugin consumes to reach The Movie Database. Nothing here is
implemented by a plugin or discovered by reflection; the core registers them
as singletons, so take them through a constructor:

```csharp
public class MyJob(ITmdbSearchService searchService, ITmdbMetadataService metadataService)
{
    // ...
}
```

| Interface | What it is for |
|---|---|
| `ITmdbSearchService` | Searching TMDB for shows and movies, and the automatic match used when linking. |
| `ITmdbMetadataService` | Refreshing, purging and matching shows and movies, genres, and the rate limiter's pause state; passed on to `IMetadataRefreshService` and `IMetadataPurgeService`. |
| `ITmdbLinkingService` | Creating and removing AniDB ↔ TMDB links; passed on to `IMetadataLinkingService`. |

TMDB is an ordinary metadata provider, so the last two are shims for code that
names TMDB; new code can call the generic services with `MetadataSource.TMDB`.
How TMDB behaves as a provider is in
[`../../Providers/README.md`](../../Providers/README.md#tmdb).

---

## Read the cache first

TMDB entities fetched once are read-only local caches, and reading them costs
no request:

```csharp
IReadOnlyList<ITmdbShow> shows = shokoSeries.GetLinkedSeries<ITmdbShow>(MetadataSource.TMDB);
IReadOnlyList<ITmdbMovie> movies = shokoSeries.GetLinkedMovies<ITmdbMovie>(MetadataSource.TMDB);
IReadOnlyList<ITmdbEpisode> episodes = shokoEpisode.GetLinkedEpisodes<ITmdbEpisode>(MetadataSource.TMDB);

// By ID, through IMetadataService.
ISeries? show = metadataService.GetSeries(MetadataSource.TMDB, tmdbShowID);
```

The cross-references are there too, as the generic links
(`shokoSeries.GetSeriesCrossReferences<ITmdbShow>(MetadataSource.TMDB)` and
the rest),
as are TMDB's suggestions (`ITmdbShow.Suggestions`, `ITmdbMovie.Suggestions`;
see [relations and suggestions](../../README.md#reading-relations-and-suggestions)).
Call the services when the cached copy is missing or stale.

One anime can match several TMDB shows (a split-cour series) and one show
several anime. Movies link at the **episode** level, because an OVA or a film
is usually one episode of an AniDB anime: `AddShowLink` takes an anime,
`AddMovieLinkForEpisode` an AniDB episode, and `SetEpisodeLink` pairs
episodes.

---

## `ITmdbSearchService`

```csharp
var (shows, totalShows) = await searchService.SearchShows("cowboy bebop", includeRestricted: false, year: 1998, page: 1, pageSize: 20);
var (movies, totalMovies) = await searchService.SearchMovies("cowboy bebop");
```

Both ask TMDB and return one page (1-based, six by default) and the total.
`SearchForAutoMatch(IAnidbAnime)` returns the matches Shoko's auto-linker
would take (the ones it turns down only show in
`IMetadataLinkingService.PreviewAutoLink`). Branch on `IsMovie`: a movie
result fills `TmdbMovie` and `AnidbEpisode`, a show result `TmdbShow`.

---

## `ITmdbMetadataService`

| Member | Behaviour |
|---|---|
| `UpdateShow(options)` / `UpdateMovie(options)` | Runs the refresh job now and awaits it; returns `false` while TMDB is paused. |
| `ScheduleUpdateOfShow(options)` / `ScheduleUpdateOfMovie(options)` | Queues the refresh job; a forced one goes first. |
| `UpdateAllShows` / `UpdateAllMovies` | Despite the names, **queue** one refresh per linked show or movie and return. |
| `ScheduleDownloadAllShowImages` / `ScheduleDownloadAllMovieImages` | Queues the image job for one entity. |
| `PurgeShow`, `PurgeMovie`, `SchedulePurgeOf…` | Queue a forced purge, which removes every link to the entity first; a purged show takes its orderings along. |
| `PurgeAllUnusedShows`, `PurgeAllUnusedMovies`, `PurgeAllMovieCollections` | Queue the purges and return. |
| `ScheduleSearchForMatch(anidbId, force)` / `ScanForMatches()` | Queue the core's search for one anime, or for every series still missing a match. `ScanForMatches` does nothing while TMDB does not auto-link. |
| `GetShowGenres()` / `GetMovieGenres()` | TMDB's genre IDs and names, fetched once per process. |
| `GetPauseStatus()` | The 5XX circuit breaker's state (`IsPaused`, `RemainingPauseTime`). |

A refresh asked for here counts as requested, so it fetches a show or movie
whether or not anything links to it yet. One that is not forced skips what
was updated in the last hour and otherwise fetches only what TMDB reports as
changed; a forced one fetches everything again, people included.
`TmdbShowUpdateOptions` and `TmdbMovieUpdateOptions` carry the ID, the force
and image flags, and the cast, ordering, network and collection switches
(`null` follows the server setting).

---

## `ITmdbLinkingService`

Every member is passed on to `IMetadataLinkingService` for the `tmdb` source.
Neither queues a refresh; the caller decides.

| Member | Passed on as |
|---|---|
| `AddShowLink(anidbAnimeId, tmdbShowId, additiveLink, matchRating)` | `AddSeriesLink`, which also matches and saves the show's episodes. `additiveLink: false` replaces the anime's other show links and their episode links. |
| `RemoveShowLink`, `RemoveAllShowLinksForAnime` | `RemoveSeriesLink` / `RemoveLinksForAnime`, setting the anime's veto. |
| `RemoveAllShowLinksForShow(showId)` | `RemoveLinksTo` the show, without veto or purge. |
| `AddMovieLinkForEpisode(anidbEpisodeId, tmdbMovieId, additiveLink, matchRating)` | `AddMovieLink`, for the episode's anime. Nothing happens for an unknown episode. |
| `RemoveMovieLinkForEpisode`, `RemoveAllMovieLinksForEpisode`, `RemoveAllMovieLinksForAnime` | `RemoveMovieLink` / `RemoveLinksForEpisode` / `RemoveLinksForAnime`, with the veto. |
| `RemoveAllMovieLinksForMovie(tmdbMovieId)` | `RemoveLinksTo` the movie, without veto or purge. |
| `SetEpisodeLink(anidbEpisodeId, tmdbEpisodeId, additiveLink, index)` | `SetEpisodeLink`. `0` records a deliberately empty link; an unstored TMDB episode is refused with `false`. |
| `ResetAllEpisodeLinks(anidbAnimeId, allowAuto)` | `ResetEpisodeLinks`. |
| `MatchAnidbToTmdbEpisodes(…)` | `MatchEpisodes`; a **dry run** unless `saveToDatabase` is set. `useExistingOtherShows` is `considerOtherLinks`. |
| `RemoveAllLinks`, `ResetAutoLinkingState` | The library-wide forms, for every series at once. |

`purge: true` queues the purge job for the entity once unlinked, which leaves
it alone while another anime still links to it.

### Search and link

What the core's search job does with TMDB's matches, the refresh written out:

```csharp
public async Task Link(IAnidbAnime anime)
{
    foreach (var result in await searchService.SearchForAutoMatch(anime))
    {
        if (result.IsMovie)
        {
            // Movies link at the episode level.
            var movieID = new MetadataGuid(MetadataSource.TMDB, MetadataEntityType.Movie, result.TmdbMovie!.ID.ToString());
            await linkingService.AddMovieLink(new()
            {
                Source = MetadataSource.TMDB,
                EntityType = MetadataEntityType.Movie,
                ProviderID = movieID,
                AnidbAnimeID = anime.AnidbID,
                AnidbEpisodeID = result.AnidbEpisode!.AnidbID,
                MatchRating = result.MatchRating,
            });
            await refreshService.RefreshEntry(movieID);
        }
        else
        {
            var showID = new MetadataGuid(MetadataSource.TMDB, MetadataEntityType.Series, result.TmdbShow!.ID.ToString());
            await linkingService.AddSeriesLink(new()
            {
                Source = MetadataSource.TMDB,
                EntityType = MetadataEntityType.Series,
                ProviderID = showID,
                AnidbAnimeID = anime.AnidbID,
                MatchRating = result.MatchRating,
            });
            // Linking the show matched its episodes already.
            await refreshService.RefreshEntry(showID);
        }
    }
}
```

---

## Rate limits

`TmdbRateLimiter` belongs to the core: a local sliding window under TMDB's
roughly 40 requests a second, a backoff on 429 responses, and a 5XX circuit
breaker that pauses TMDB work after three server errors within ten seconds.
Each TMDB job type runs up to four at once.

- Prefer the `Schedule…` forms for bulk work: a queued job resumes once a
  pause lifts, while `UpdateShow` refuses to run during one.
- Never loop `UpdateShow` over a library; `UpdateAllShows` queues instead.
- Check `GetPauseStatus()` before starting a sweep of your own.

## Gotchas

- **Linking fetches nothing.** Queue a refresh of what you linked.
- **`AddShowLink` also matches and saves episodes**; it is not a bare insert.
- **Removing a link sets the anime's TMDB veto** when something was removed;
  `ResetAutoLinkingState(disabled: false)` lifts it for every series.
- **Adding and matching need TMDB enabled** for the kind, or throw
  `NotSupportedException` (400 from APIv3). Removing works either way.
- **`additiveLink` defaults differ** between the show and movie methods; pass
  it explicitly.
- **The chosen episode group and hidden episodes** are kept by
  `IMetadataOrderingService` for every source. `ITmdbShow.TmdbOrderings` lists
  only TMDB's own; `ISeries.Orderings` has them all.
