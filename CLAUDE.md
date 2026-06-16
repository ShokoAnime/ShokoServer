# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Commands

```bash
dotnet build Shoko.Server.sln
dotnet test Shoko.Tests/Shoko.Tests.csproj --filter "FullyQualifiedName~ClassName.Method"
dotnet test Shoko.IntegrationTests/Shoko.IntegrationTests.csproj
```

Target framework: `.NET 10.0`. Configurations: `Debug`, `Release`, `ApiLogging`, `Benchmarks` (server + benchmarks only; tests use `Debug`/`Release`).

## Code Style

`.editorconfig` with ReSharper enforcement:
- Line length: 160 characters
- Modifier order: `private, protected, public, internal, sealed, new, override, virtual, abstract, static, extern, async, unsafe, volatile, readonly, required, file`
- **`var` preferred everywhere** — `csharp_style_var_elsewhere`, `csharp_style_var_for_built_in_types`, and `csharp_style_var_when_type_is_apparent` all set to `true` (enforced as warnings in `src/` paths)
- Braces on new lines (`csharp_new_line_before_open_brace = all`)
- Naming: `_camelCase` for instance fields, `_camelCase` for static fields, `PascalCase` for methods/classes/properties, `camelCase` for locals/parameters

## Commit Messages

We follow [Conventional Commits](https://www.conventionalcommits.org/), formatted as `type(scope): subject` (the scope is optional).

- **Subject** — present/imperative tense, lowercase, no trailing period (e.g. `add stream indices`, not `Added stream indices.`).
- **Body** — past tense, describing what the commit did. Optional; include it when the *why* isn't obvious from the subject. Markdown is allowed, but **without headers** — use bold text and bullet lists instead of `#` headings.
- **Code references** — wrap code in backticks in both the subject and body: methods (`` `FindReleaseForVideo` ``), types (`` `VideoLocal` ``), endpoints/paths (`` `/api/v3/Series` ``), settings, and file names. Use the bare identifier, not a prose description.
- Append `[skip ci]` to the subject for changes that should not trigger CI.

### Types

| Type | When to use |
|------|-------------|
| `feat` | Adds a new user-, API-, or plugin-facing capability (endpoint, DTO field, abstraction surface). |
| `fix` | Corrects broken or incorrect behavior. |
| `refactor` | Restructures code without changing external behavior — renames, splits, simplifies, removes. |
| `chore` | In-tree housekeeping with no behavior change — version bumps, import sorting, comment/doc-comment fixups. |
| `docs` | Documentation-only changes — XML docs, READMEs, API deprecation notices. |
| `repo` | Repository infrastructure and tooling — workflows, scripts, dependencies, devcontainer, `.gitignore`/`.dockerignore`. |
| `misc` | Small changes that don't fit cleanly elsewhere. Reach for this only when nothing else fits. |
| `revert` | Reverts a previous commit. |

### Precedence

When a commit qualifies for more than one type, pick the most structural one. Dominance order: `refactor` > `feat` > `fix` > `chore`/`repo`/`docs`/`misc`.

- A commit that both adds a feature **and** restructures or removes existing code is a `refactor`, not a `feat`.
- Introducing a new feature that requires dropping a previous feature is a `refactor` (the removal dominates).

### Disambiguation

- **`chore` vs `repo`** — `chore` is in-tree code housekeeping (imports, version bumps, comments); `repo` is the build/CI/tooling/deps that live around the code.
- **`refactor` vs `fix`** — if external behavior visibly changes for the better, it's `fix`; if behavior is identical, it's `refactor`.
- **`chore` vs `misc`** — prefer a precise type; `misc` is the last resort.
- **`docker` is a scope, not a type** (e.g. `repo(docker)`, `feat(docker)`).

### Scopes

Scopes are optional and free-form, but reuse the established ones where they apply (non-exhaustive): `abstractions`, `api`, `db`, `plugin`, `images`, `relocation`, `anidb`, `tmdb`, `search`, `scrobble`, `core`, `deps`, `workflows`, `scripts`, `docker`.

### Example

```
refactor: remove parallel mode and simplify release search pipeline

Collapsed the dual-path release search into a single sequential
pipeline. The parallel evaluator added contention without a measurable
throughput gain on real libraries.

- Removed the parallel branch and its bespoke locking
- Folded the remaining provider lookup into `FindReleaseForVideo`
```

## Architecture

### Project Layout

- **`Shoko.Abstractions`** — NuGet package for plugin authors. Defines the interface contract between the core and plugins (`IPlugin`, `IShokoSeries`, `IShokoEpisode`, `IVideo`, `IUser`, and all service/metadata/video/user interfaces). Only update this when the plugin contract itself needs to change.
- **`Shoko.Server`** — All implementation: API, database, repositories, services, scheduling, providers, models.
- **`Shoko.QueueProcessor`** — Custom job queue engine. Defines `IQueueScheduler`, `IQueueJob`, `RecurringJobRegistry`, `IJobChainBuilder`, `IVideoReleaseProviderJob<T>`, persistence (`QueuedJob`, `JobRepository` via EF Core), concurrency/acquisition attributes, and the orchestration stack (`QueueOrchestrator`, `WorkerPool`, `WorkerPoolManager`). Referenced as a local project, not a NuGet package.
- **`Shoko.CLI`** — Headless server entry point. Instantiates and manages `SystemService` directly.
- **`Shoko.TrayService`** — Cross-platform tray app (Avalonia) embedding the server. Runs on Windows, Linux, and macOS.
- **`Shoko.Tests`** — Unit tests.
- **`Shoko.IntegrationTests`** — Integration tests.
- **`Shoko.TestData`** — Shared test data.
- **`Shoko.Benchmarks`** — BenchmarkDotNet benchmarks.

### Startup Sequence

Entry points: `Shoko.CLI/Program.cs` (headless) or `Shoko.TrayService/Program.cs` (tray app). Both instantiate `new SystemService()` directly, which internally builds and starts the `IHost`.

`Program.cs` → `SystemService` constructor (NLog, `PluginManager`, `ConfigurationService`, `SettingsProvider`) → `SystemService.StartAsync()` (builds and starts `IHost` / ASP.NET Core on port 8111) → `SystemService.LateStart()` (DB migrations via `DatabaseFixes`, init queue scheduler via `IQueueScheduler`, UDP connection handler, file watchers).

**Note:** `LateStart()` is skipped during first-run setup mode (`InSetupMode == true`). It runs either on normal startup or when `CompleteSetup()` transitions out of setup mode.

Global service container is exposed via `ISystemService.StaticServices = _webHost.Services` for legacy code that predates DI,
and should not be used for new code unless DI is not an option and only as a last resort.

### API Pipeline

**Middleware order** (configured in `Shoko.Server/API/APIExtensions.cs`, `UseAPI()`):

1. Sentry exception handling (if not opted out)
2. `DeveloperExceptionPage` (DEBUG / `AlwaysUseDeveloperExceptions`)
3. Swagger UI (if enabled) — configurable path via `WebSettings.SwaggerUIPrefix`
4. Static files (if enabled) — WebUI served via `WebUiFileProvider` at configurable path (`WebSettings.WebUIPublicPath`, defaults to `/webui`)
5. `UseRouting`
6. `UseAuthentication` — custom "ShokoServer" scheme
7. `UseAuthorization` — policies: `"admin"` (IsAdmin == 1), `"init"` (setup user only)
8. `UseEndpoints` — SignalR hubs registered here: `/signalr/logging`, `/signalr/aggregate`
9. Plugin middleware registration
10. `UseCors` (any origin/method/header)
11. `UseMvc` (legacy, `EnableEndpointRouting = false`)

**Global action filters** (registered on all MVC controllers):
- `DatabaseBlockedFilter` — returns 400 if DB is blocked, exempted via `[DatabaseBlockedExempt]`
- `ServerNotRunningFilter` — returns 503 until server is started, exempted via `[InitFriendly]`

**Action constraints**:
- `RedirectConstraint` — redirects root `/` to WebUI public path if configured

**Authentication** (`Shoko.Server/API/Authentication/`):
- `CustomAuthHandler` extracts API key from: `apikey` header, `apikey` query param, `Bearer` token (SignalR), or `access_token` query param (SignalR)
- Validates against `AuthTokensRepository`; builds `ClaimsPrincipal` with user ID, role, device name
- During first-run setup, `InitUser` (synthetic admin) is used — no real auth required
- No cookie sessions; every request is authenticated by API key

**API versioning**: `v0` (version-less: auth + legacy Plex webhooks + index redirect), `v1` (legacy REST, off by default), `v2` (legacy REST, can be kill-switched), `v3` (current, all new endpoints). Version can be resolved from query string, `api-version` header, or custom `ShokoApiReader`. `ApiVersionControllerFeatureProvider` excludes disabled versions at startup via individual flags (`EnableAPIv1`, `EnableAPIv2`, `EnableAPIv3`, `EnableLegacyPlexAPI`, `EnableAuthAPI`).

**Serialization**: MVC uses `AddNewtonsoftJson()` (not `System.Text.Json`) with: `MaxDepth = 10`, `DefaultContractResolver`, `NullValueHandling.Include`, `DefaultValueHandling.Populate`. SignalR also uses `AddNewtonsoftJsonProtocol()`.

Plugin controllers are registered via `AddPluginControllers` during API setup.

### SignalR (Real-time Events)

Two hubs, both behind `[Authorize]`:

- **`LoggingHub`** (`/signalr/logging`) — streams buffered server logs to connecting clients, separate from the aggregate hub because it can become noisy, fast.
- **`AggregateHub`** (`/signalr/aggregate`) — subscription model; clients call `feed.join_single` / `feed.join_many` etc. to subscribe to event categories.

Event emitters bridge internal domain events to SignalR: `AnidbEventEmitter`, `AvdumpEventEmitter`, `ConfigurationEventEmitter`, `FileEventEmitter`, `ManagedFolderEventEmitter`, `MetadataEventEmitter`, `NetworkEventEmitter`, `QueueEventEmitter`, `ReleaseEventEmitter`, `UserDataEventEmitter`, `UserEventEmitter`.

### Model Layers and Separation

Three distinct model layers; **do not mix them**.

**1. Persistence models** (`Shoko.Server/Models/`)
NHibernate-mapped entities. Organized by source:
- `Shoko.Server.Models.Shoko` — core domain: `AnimeSeries`, `AnimeGroup`, `AnimeEpisode`, `VideoLocal`, `JMMUser`, `FilterPreset`, etc.
- `Shoko.Server.Models.AniDB` — AniDB metadata cache: `AniDB_Anime`, `AniDB_Episode`, `AniDB_Character`, `AniDB_Creator`, `AniDB_Tag`, etc.
- `Shoko.Server.Models.TMDB` — TMDB metadata cache: `TMDB_Show`, `TMDB_Movie`, `TMDB_Episode`, `TMDB_Image`, etc.
- `Shoko.Server.Models.CrossReference` — cross-reference tables linking providers (AniDB↔TMDB, AniDB↔MAL)
- `Shoko.Server.Models.Release` — release/video file associations
- `Shoko.Server.Models.Image` — image metadata
- `Shoko.Server.Models.Internal` — internal tracking entities
- `Shoko.Server.Models.Legacy` — legacy entities scheduled for removal once APIv1 is finally removed or before if they can be mocked using other methods/models

NHibernate mappings live in `Shoko.Server/Mappings/` as `*Map.cs` files. Schemas should be maintained to match, as they will be migrated to Entity Framework Code-First in a future version.

**2. API response DTOs** (`Shoko.Server/API/v*/Models/`)
Never persisted; built from persistence models in controllers/services.
- `v1/Models/` — legacy `CL_*` contract classes (50+ files), kept for backward compatibility
- `v3/Models/Shoko/` — modern response models (`Series`, `Episode`, `Group`, `File`, `User`, …) extending `BaseModel`
- `v3/Models/AniDB/` and `v3/Models/TMDB/` — provider-specific response shapes
- `v3/Models/Common/` — shared types (`Images`, `Rating`, `Tag`, `Title`, etc.)

**3. Abstractions interfaces** (`Shoko.Abstractions/`)
`IShokoSeries`, `IShokoEpisode`, `IVideo`, `IUser`, etc. — implemented by persistence models, consumed by plugins and services. Plugin code should depend only on these, never on concrete `Shoko.Server` types.

### Repository Pattern

Two variants in `Shoko.Server/Repositories/`:
- **`Cached/`** — `BaseCachedRepository<T, S>` loads all rows at startup into a `PocoCache` (from `NutzCode.InMemoryIndex`). Reads are `ReaderWriterLockSlim`-protected. Each repository builds typed indexes via `PopulateIndexes()` (e.g., `_animeIDs = Cache.CreateIndex(a => a.AnimeID)`). All writes go to DB then invalidate/update the in-memory cache. Use for hot data.
- **`Direct/`** — no cache; hits DB on every call. Use for infrequently accessed or large data.
- `BaseDirectRepository` is the base class.

Always prefer a cached repository over a direct one when both exist for the same entity.

**Access pattern**: Repositories are accessed via the `RepoFactory` static class (e.g., `RepoFactory.AnimeSeries.GetByID(id)`). `RepoFactory` is DI-registered but exposes static fields for convenience — this is a legacy pattern similar to `ISystemService.StaticServices`. This exists for compatibility where DI is unavailable, but DI should be used if possible.

### Scheduling

The scheduling system lives in `Shoko.QueueProcessor` — a database-backed job queue (SQLite/MySQL/SQL Server via EF Core `QueueDbContext`) with an O(1) in-memory deduplication index. Job definitions remain in `Shoko.Server/Scheduling/Jobs/`.

**Entry point — `IQueueScheduler`** (`Shoko.QueueProcessor/Abstractions/IQueueScheduler.cs`):
- `Enqueue<T>()` — enqueue with dedup; no-op if already waiting or executing.
- `EnqueueImmediate<T>()` — max-priority enqueue; returns a `Task` that completes when the job finishes.
- `RunAfterCurrent<T>()` — registers a job to run immediately after the currently-executing job. Falls back to `Enqueue` with `prioritize: true` if called outside a worker context.
- `CreateJobChain()` — returns an `IJobChainBuilder` for sequential chains.

**Job chains — `IJobChainBuilder`** (`Shoko.QueueProcessor/Abstractions/IJobChainBuilder.cs`):
Build with `.Then<T>().Then<T>()...` and submit with `.Enqueue()` (queue entry[0] normally) or `.EnqueueAfterCurrent()` (entry[0] after the current job, rest as a chain).

**Recurring jobs — `RecurringJobRegistry`** (`Shoko.QueueProcessor/Scheduling/RecurringJobRegistry.cs`):
Timer-based `IHostedService`. Call `Register<T>(interval)` from DI (plugin `Load` or `RegisterServices`). Registrations before `StartAsync` are armed on host boot; registrations after are armed immediately. Job types must first be registered via `AddQueueJobsFromAssembly`.

**Concurrency attributes** (`Shoko.QueueProcessor/Concurrency/`):
- `[LimitConcurrency(default, max?)]` — pool-level slot cap.
- `[DisallowConcurrentExecution]` — at most one instance running at a time.
- `[DisallowConcurrencyGroup("name")]` — mutual exclusion across jobs sharing a group name.

**Acquisition filter attributes** — block dispatch until the condition is met:
- `[DatabaseRequired]` — waits until the DB is initialized.
- `[NetworkRequired]` — waits until network connectivity is confirmed.
- `[AniDBUdpRateLimited]` — respects AniDB UDP rate limits.
- `[AniDBHttpRateLimited]` — respects AniDB HTTP rate limits.

**`IJobFactory`** (`Shoko.QueueProcessor/JobFactory.cs`): DI-resolved single-shot execution via `Execute<T>()`. Used internally by the worker and by tests or services that need to run a job inline.

`QueueStateEventHandler` bridges job lifecycle events (added/started/completed) to `QueueEventEmitter` → SignalR clients.

The queue system lives in the QueueProcessor project, but the Shoko-specific code, like jobs or more advanced acquisition filters, is defined in Shoko.Server/Scheduling.

### Plugin System

`PluginManager` scans the `/plugins/` directory, loads assemblies, finds `IPlugin` implementations via reflection, and registers their services via `RegisterPlugins(IServiceCollection)`. `InitPlugins()` instantiates the plugins after the service container is available. `CorePlugin` is the built-in plugin that ships with the server.

Plugins can also implement `IPluginApplicationRegistration` to register custom middleware via `RegisterServices(IApplicationBuilder, ApplicationPaths)` — invoked during `UseAPI()` after SignalR but before CORS.

Plugin controllers are registered via `AddPluginControllers` during API setup.

### Configuration System

**No `appsettings.json`** exists in the repo. Configuration is code-based:
- **`ServerSettings`** — primary settings class, persisted to `settings-server.json` via `[StorageLocation]` attribute
- **`ConfigurationProvider<T>`** — generic provider using `INewtonsoftJsonConfiguration` for JSON serialization
- **`SettingsProvider`** — singleton accessor (`ISettingsProvider.Instance`) for runtime settings access
- `appsettings.json` is configured as an **optional** overlay in the host builder but is not shipped

### Testing

- **Framework**: xUnit 2.7.0 with `Xunit.DependencyInjection` 9.1.0 for DI in tests
- **Mocking**: Moq 4.20.70
- **Coverage**: coverlet 6.0.2
- **Test SDK**: Microsoft.NET.Test.Sdk 17.9.0
- Unit tests in `Shoko.Tests/`, integration tests in `Shoko.IntegrationTests/`

### Database Migrations

All schema migrations and data fixups are in `Shoko.Server/Databases/DatabaseFixes.cs`. Append new migrations; never modify existing ones. `Versions` class tracks the applied migration level. Supported backends: SQLite (default), MySQL/MariaDB, SQL Server — selected via `DatabaseFactory`.

## Domain Model Relationships

### File → Location

**`VideoLocal`** is the canonical record for a unique file, identified by its ED2K hash + file size. It holds hashes, `MediaInfo`, import date, and AniDB MyList ID. It does not store a path.

**Note:** `VideoLocal.MediaInfo` is serialized using **MessagePack** via a custom NHibernate type (`MessagePackConverter<MediaContainer>`), not JSON.

**Note:** `FilterPreset.Expression` and `FilterPreset.SortingExpression` use a custom NHibernate type (`FilterExpressionConverter`) for JSON serialization.

**`VideoLocal_Place`** stores where a `VideoLocal` physically lives: a `ManagedFolderID` + `RelativePath`. One `VideoLocal` can have multiple places (the same file duplicated across folders). The absolute path is computed at runtime as `folder.Path + place.RelativePath`.

**`ShokoManagedFolder`** (formerly `ImportFolder`) is a root directory Shoko monitors. Each folder has `IsWatched`, `IsDropSource`, and `IsDropDestination` flags used by the file relocation system.

```
ShokoManagedFolder (1) ──< VideoLocal_Place >── (1) VideoLocal
                                                      │
                                              VideoLocal_HashDigest (CRC32/MD5/SHA1)
```

### File → Episode

**`StoredReleaseInfo`** is the source of truth for a file's episode mapping. It caches the full response from a release provider for a given ED2K hash + file size, including:
- Precise episode percentage ranges
- Release group, video/audio codec, language, and quality information
- Cross-references to anime/episodes

A single file can map to multiple episodes (e.g., a combined OVA file). Multiple files can map to the same episode (alternative releases).

**`CrossRef_File_Episode`** is a backwards-compatible join table kept in sync with `StoredReleaseInfo`. It stores a simplified view of the same mapping, keyed by ED2K hash + file size (not VideoLocalID):
- `Percentage` — what fraction of the episode this file covers (100 for a single-episode file, 50 for a 2-part release)
- `EpisodeOrder` — which part this file is in a multi-file episode
- `IsManuallyLinked` — indicates the user was involved in creating this link

Because `StoredReleaseInfo` captures precise percentage ranges rather than a single percentage value, it should be treated as the authoritative mapping.

```
VideoLocal (hash+size) ──< CrossRef_File_Episode >── AniDB_Episode
        │                       │
   StoredReleaseInfo ───────────┘
```

### Episode → Series → Group

**`AniDB_Episode`** is the raw AniDB cache (episode number, type, air date, synopsis, rating). It has no Shoko-specific data.

**`AnimeEpisode`** wraps one `AniDB_Episode` and adds Shoko state: hidden flag, title override, and the FK to `AnimeSeries`. All user watch data is stored in `AnimeEpisode_User`.

**`AniDB_Anime`** is the raw AniDB cache for a series (titles, synopsis, ratings, episode counts, external IDs for streaming services). One `AnimeSeries` maps to exactly one `AniDB_Anime` via `AniDB_ID`.

**`AnimeSeries`** is Shoko's local wrapper around an AniDB anime. Adds name/description overrides, language preferences, TMDB auto-match flags, and missing episode counts. All user ratings live in `AnimeSeries_User`.

**`AnimeGroup`** is a container for series, supporting arbitrary nesting (groups within groups via `AnimeGroupParentID`). Groups can be auto-named from their main series or manually named. `AllSeries` and `AllChildren` are recursive traversals.

```
AnimeGroup (self-referential parent ──< children)
  └──< AnimeSeries (1:1 AniDB_Anime)
         └──< AnimeEpisode (1:1 AniDB_Episode)
                └──< CrossRef_File_Episode >── VideoLocal
```

### Series/Episode → TMDB

AniDB and TMDB entities are connected through cross-reference tables, not direct FKs:

- `CrossRef_AniDB_TMDB_Show` — `AnimeSeries` ↔ `TMDB_Show`
- `CrossRef_AniDB_TMDB_Movie` — `AnimeSeries` or `AnimeEpisode` ↔ `TMDB_Movie` (OVAs/movies often link at episode level)
- `CrossRef_AniDB_TMDB_Episode` — `AnimeEpisode` ↔ `TMDB_Episode`

One anime can match multiple TMDB shows (e.g., split-cour series on TMDB) and one TMDB show can match multiple anime. TMDB models (`TMDB_Show`, `TMDB_Movie`, `TMDB_Episode`, `TMDB_Season`, `TMDB_Image`) are read-only caches of TMDB API data, structured identically to the TMDB response schema.

## Import Pipeline

### Job Chain

When a file appears, jobs execute in sequence via `IJobChainBuilder` / `RunAfterCurrent`:

```
File appears on disk
        │
        ▼
ScanFolderJob  (Shoko.Server/Scheduling/Jobs/Shoko/ScanFolderJob.cs)
  Walks managed folder, creates VideoLocal + VideoLocal_Place stubs for new files
        │
        ▼
HashFileJob  (Shoko.Server/Scheduling/Jobs/Shoko/HashFileJob.cs)
  Computes ED2K (primary), MD5, SHA1, CRC32 via IVideoHashingService
  Stores hashes, populates VideoLocal.Hash
        │
        ▼  [VideoReleaseService builds a chain via IJobChainBuilder]
AnidbProcessFileJob  (Shoko.Server/Scheduling/Jobs/Shoko/AnidbProcessFileJob.cs)
  Implements IVideoReleaseProviderJob<AnidbReleaseProvider>
  Queries AniDB UDP for episode mapping; creates CrossRef_File_Episode + StoredReleaseInfo
  Adds file to AniDB MyList (unless skipped)
─── or, for providers without a dedicated job class ───
ProcessReleaseProviderJob  (Shoko.Server/Scheduling/Jobs/Shoko/ProcessReleaseProviderJob.cs)
  Generic fallback; identified by ProviderID (Guid)
        │
        ▼
FinalizeReleaseSearchJob  (Shoko.Server/Scheduling/Jobs/Shoko/FinalizeReleaseSearchJob.cs)
  Always the last entry in every provider chain
  Fires IVideoReleaseService.SearchCompleted; triggers relocation if configured
        │  [on new AnimeID — enqueued by provider jobs]
        ▼
GetAniDBAnimeJob  (Shoko.Server/Scheduling/Jobs/AniDB/GetAniDBAnimeJob.cs)
  Fetches full AniDB_Anime + all AniDB_Episode records via AniDB HTTP API
  Creates AnimeSeries + AnimeGroup if they don't exist (CreateSeriesEntry=true)
        │  [unless SkipTmdbUpdate]
        ▼
SearchTmdbJob  (Shoko.Server/Scheduling/Jobs/TMDB/SearchTmdbJob.cs)
  Auto-searches TMDB for matching show/movie by title + episode count
  Creates CrossRef_AniDB_TMDB_Show / CrossRef_AniDB_TMDB_Movie
        │
        ▼
UpdateTmdbShowJob / UpdateTmdbMovieJob  (Shoko.Server/Scheduling/Jobs/TMDB/)
  Fetches TMDB_Show/Movie/Episode/Season/Image records
  Fetches titles + overviews in all configured languages
        │
        ▼
Image download jobs  (DownloadImageJob)
  Downloads poster/backdrop/thumbnail files to local image cache
```

### Orchestration Pattern

Jobs do not use a central orchestrator. Each job enqueues its successor via `IQueueScheduler.RunAfterCurrent<T>()` or `IJobChainBuilder`. The provider job chain is built by `VideoReleaseService` using `CreateJobChain()`: one entry per enabled `IReleaseInfoProvider` (using the provider's dedicated `IVideoReleaseProviderJob<TProvider>` class if registered, otherwise `ProcessReleaseProviderJob`), with `FinalizeReleaseSearchJob` appended as the terminal step. Provider jobs read the `AnimeID` from the release info and enqueue `GetAniDBAnimeJob` when a new anime is encountered.

**`ImportJob`** (`Shoko.Server/Scheduling/Jobs/Actions/ImportJob.cs`) is a periodic sweep that catches anything the live pipeline missed: it calls `IVideoService.ScheduleScanForManagedFolders()` to rescan all watched folders.

**`ScanForMissingReleaseInfoJob`** (`Shoko.Server/Scheduling/Jobs/Actions/ScanForMissingReleaseInfoJob.cs`) is a recurring job (registered via `RecurringJobRegistry`) that finds `StoredReleaseInfo` records with unknown source or missing audio/subtitle languages and re-queues the appropriate provider job on each provider's backoff schedule (`GetRescanDelay()`).

### Intermediate Cache Models

Several models exist solely to avoid redundant I/O or external API calls. Jobs check these before making outbound requests.

**`FileNameHash`** (`Models/Shoko/FileNameHash.cs`) — maps `FileName + FileSize → ED2K hash`. Written by `VideoHashingService.SaveFileNameHash()` and `VideoRelocationService` after a file is successfully hashed. Read by `AnidbReleaseProvider` as a last-resort local lookup when checking for creditless/variant files before going to AniDB.

**`VideoLocal_HashDigest`** (`Models/Shoko/VideoLocal_HashDigest.cs`) — stores all computed hash types (ED2K, CRC32, MD5, SHA1) for a `VideoLocal` as `Type + Value` rows. Written by `VideoHashingService` during `HashFileJob`. Read when displaying or cross-referencing file hashes without recomputing.

**`StoredReleaseInfo`** (`Models/Release/StoredReleaseInfo.cs`) — caches the full release provider response: ED2K + FileSize, provider ID, release URI, source (BluRay/Web/etc.), codec flags (`IsCensored`, `IsCreditless`, `IsChaptered`), version, and cross-references to anime/episodes. Written by `IVideoReleaseService.FindReleaseForVideo()` inside provider jobs. Provider jobs call `GetCurrentReleaseForVideo()` first — if a `StoredReleaseInfo` already exists for the hash, the lookup is skipped entirely. Queried by the API via `GetByEd2kAndFileSize()`, `GetByReleaseURI()`, `GetByAnidbEpisodeID()`.

**`StoredReleaseInfo_MatchAttempt`** (`Models/Release/StoredReleaseInfo_MatchAttempt.cs`) — tracks per-provider match attempts for a file: `ProviderName`, `ProviderID`, `AttemptCount`, `AttemptStartedAt`, `AttemptEndedAt`, `EmbeddedAttemptProviderNames`. Written by provider jobs at the start of each attempt. Read by `ScanForMissingReleaseInfoJob` to apply per-provider backoff logic and skip providers that have already found a result.

**`AniDB_AnimeUpdate`** (`Models/AniDB/AniDB_AnimeUpdate.cs`) — one row per `AnimeID`, storing only `UpdatedAt`. Written by `RequestGetAnime.UpdateAccessTime()` after every successful AniDB HTTP response. Read by the same method to decide whether the local `AniDB_Anime` record is stale enough to warrant a new fetch. `GetAniDBAnimeJob` respects `IgnoreTimeCheck` to force a refresh past this gate.

**`AniDB_GroupStatus`** (`Models/AniDB/AniDB_GroupStatus.cs`) — caches AniDB GROUPSTATUS UDP responses: release group name, completion state, episode range, rating. Written by `GetAniDBReleaseGroupStatusJob` after a UDP `RequestReleaseGroupStatus` call. `GetAniDBReleaseGroupStatusJob.ShouldSkip()` bypasses the fetch entirely if the anime ended more than 50 days ago.

**`AniDB_NotifyQueue`** + **`AniDB_Message`** — `AniDB_NotifyQueue` (`Models/AniDB/`) is a staging table for raw AniDB notification IDs (type + ID) written by `GetAniDBNotifyJob`. `AniDB_Message` stores the fully fetched message body (sender, title, body, `FileMoved`/`FileMoveHandled` flags). `AcknowledgeAniDBNotifyJob` and `ProcessFileMovedMessageJob` drain these two tables in sequence.

**`ScheduledUpdate`** (`Models/Internal/ScheduledUpdate.cs`) — tracks `LastUpdate` timestamps for periodic background tasks (one row per `UpdateType`). Written after each scheduled job completes. Read at job start to determine whether enough time has elapsed to run again.

### Unrecognized Files

If no release provider returns a match, the provider chain completes without creating a `StoredReleaseInfo` record and the file is considered unrecognized. The file can be linked to one or more episodes via the API or by plugins.

**AVDump** (`AvdumpFileJob`) is an on-demand utility that submits a file's media info and hashes to AniDB for manual entry. It is unrelated to unrecognized file handling and only runs on explicit user/plugin request.

### Concurrency Limits

| Job | Default Concurrency (Max Concurrency) | Reason |
|-----|---------------------------------------|--------|
| `HashFileJob` | 2 | I/O bound |
| `MediaInfoJob` | 2 | I/O bound |
| `AnidbProcessFileJob` | 4 | AniDB UDP rate limit |
| `GetAniDBAnimeJob` | group-limited (`AniDB_HTTP`) | AniDB HTTP bulkhead |
| `AVDumpFilesJob` | 1 (16) | AVDump resource limits |
| `SearchTmdbJob` | 8 (24) | TMDB allows higher throughput |
| `UpdateTmdbShowJob` | 1 (12) | TMDB allows higher throughput |
| `UpdateTmdbMovieJob` | 1 (12) | TMDB allows higher throughput |
| `DownloadImageJob` | 4 | Image download throughput |
| `ValidateAllImagesJob` | 1 (1) | Sequential validation |
