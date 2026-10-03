# Shoko TMDb Metadata

The bundled first-party plugin that serves the `tmdb` source: TMDb's shows,
seasons, episodes, movies and collections, and the people, companies and
networks behind them. It ships with the server in `plugins/Shoko.Plugin.Tmdb/`,
is enabled by default and cannot be uninstalled.

## What it does

`TmdbMetadataProvider` is an ordinary metadata provider. The core runs the
refresh, search, auto-link, image and entity jobs and calls in; the plugin
asks TMDb and writes into the core's stores, and the core reads everything
back from them.

- **Series and movies.** A refresh stores the show with its seasons and
  episodes, or the movie, with their titles, overviews, credits, genres and
  keywords, content ratings, studios, networks, suggestions and episode
  groups (as global orderings).
- **Collections, people, companies and networks.** Stored on refresh, and
  refreshed one at a time through the entity provider when a credit or link
  names one the stores lack.
- **Genres.** Kept as tags by TMDb's IDs (`tmdb://tag/genre/16`). A genre
  joining two with `&`, such as `Action & Adventure`, is one tag per part
  (`tmdb://tag/genre/10759/1`, `…/2`), so filters match `Action` and
  `Adventure` alike.
- **Images.** Candidates in TMDb's order. The poster, backdrop, profile or
  logo TMDb names on an entry is its pinned default.
- **Linking.** `FindAutoLinks` searches by title, dates and episode count and
  hands back every candidate scored; the core reviews and links them.
  `MatchEpisodes` lines up AniDB's episodes with the show's.
- **Site URLs.** Pages on `www.themoviedb.org` for every kind with one. TMDb
  has no character pages.
- **Purging images.** The "Purge Unused TMDb Images" action removes every
  TMDb image nothing links to.

## Configuration

`TmdbConfiguration`, stored in `tmdb.json` in the plugin's configuration
folder and edited through the configuration API like any plugin's.

| Setting | Environment variable |
|---|---|
| `UserApiKey`, your own API key, masked on the way out | `TMDB_API_KEY` |
| `IncrementalChangesWindowDays`, `0` to `14` | `TMDB_CHANGES_WINDOW_DAYS` |
| `RateLimit.MaxRequestsPerWindow` | `TMDB_RATE_LIMIT_MAX_REQUESTS_PER_WINDOW` |
| `RateLimit.WindowDurationMs` | `TMDB_RATE_LIMIT_WINDOW_DURATION_MS` |

The rest switch what a refresh downloads (all titles, overviews and content
ratings, crew and cast, collections, episode groups, networks), whether other
anime's links are weighed when linking, and how many candidates an
auto-search scores.

## API key

Official builds carry a key of their own: CI stamps it into
`Constants.ApiKey` from the `TMDB_API` secret with
`.github/workflows/ReplaceTmdbApiKey.ps1`. A configured `UserApiKey` takes
precedence. Without either, the provider says it is not configured, and the
core answers its routes with `503`.

## Rate limiting

Every request goes through the plugin's own sliding-window rate limiter and a
bulkhead. While TMDb limits the rate or answers with server errors, the
provider reports itself paused through `IPausableMetadataProvider`, and the
core holds its jobs back until it resumes.

## Images

The plugin registers the image template URL for `tmdb` on every start:
TMDb's image server in the original size, or the server
`TMDB_IMAGE_CDN_URL` names. A template set in the server's image settings
takes precedence.
