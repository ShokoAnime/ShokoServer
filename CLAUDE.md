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
- **`Shoko.QueueProcessor`** — Custom job queue engine. Defines `IQueueScheduler`, `IQueueJob`, `RecurringJobRegistry`, `IJobChainBuilder`, `IVideoReleaseProviderJob<T>`, persistence (`QueuedJob`, `JobRepository` via EF Core), concurrency/acquisition attributes, and the orchestration stack (`QueueOrchestrator`, `WorkerPool`, `WorkerPoolManager`). The server references it as a local project; it is also published to NuGet for plugins.
- **`Shoko.CLI`** — Headless server entry point. Instantiates and manages `SystemService` directly.
- **`Shoko.TrayService`** — Cross-platform tray app (Avalonia) embedding the server. Runs on Windows, Linux, and macOS.
- **`Shoko.Plugin.WebAOM`** — Bundled first-party plugin holding the WebAOM renamer, the default relocation provider. `BundledPlugins.targets`, imported by `Shoko.CLI` and `Shoko.TrayService`, builds every bundled plugin and copies it to `plugins/<project name>/` in their build and publish output.
- **`Shoko.Plugin.Tmdb`** — Bundled first-party plugin serving the `tmdb` source: TMDB client, rate limiter, mapping onto the shared metadata stores, and `TmdbMetadataProvider`. Its configuration is `tmdb.json` (`TmdbConfiguration`), and its built-in API key is stamped into `Constants.ApiKey` by `.github/workflows/ReplaceTmdbApiKey.ps1`. The core holds no TMDB code beyond its database and settings migrations, `MetadataSource.TMDB` and the APIv3 TMDB routes and models, which read the shared stores.
- **`Shoko.Tests`** — Unit tests.
- **`Shoko.QueueProcessor.Tests`** — Tests for the job queue.
- **`Shoko.IntegrationTests`** — Integration tests.
- **`Shoko.TestData`** — Shared test data.
- **`Shoko.Benchmarks`** — BenchmarkDotNet benchmarks.
- **`Shoko.BuildTools`** / **`Shoko.BuildTools.Targets`** — build tool and MSBuild props/targets for plugin builds (assembly metadata injection, plugin manifests).

### Startup Sequence

Entry points: `Shoko.CLI/Program.cs` (headless) or `Shoko.TrayService/Program.cs` (tray app). Both instantiate `new SystemService()` directly, which internally builds and starts the `IHost`.

`Program.cs` → `SystemService` constructor (NLog, `PluginManager`, `ConfigurationService`, `SettingsProvider`) → `SystemService.StartAsync()` (builds and starts `IHost` / ASP.NET Core on port 8111) → `SystemService.LateStart()` (database schema patches and `DatabaseFixes` data fixups, plugin database migrations, init queue scheduler via `IQueueScheduler`, UDP connection handler, file watchers).

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
6. `ServerNotRunningMiddleware` — returns 503 until the server is started, exempted via `[InitFriendly]`
7. `UseAuthentication` — custom "ShokoServer" scheme
8. `ActorContextMiddleware` — makes the request's API token the current actor (`IActorContext`) until the request ends
9. `UseAuthorization` — policies: `"admin"` (IsAdmin == 1), `"init"` (setup user only)
10. `UseEndpoints` — `MapControllers`, plus the SignalR hubs when `EnableSignalR` is on: `/signalr/logging`, `/signalr/aggregate`
11. Plugin middleware registration
12. `UseCors` (any origin/method/header)
13. `UseMvc` (legacy, `EnableEndpointRouting = false`)

**Global action filters** (registered on all MVC controllers):
- `DatabaseBlockedFilter` — returns 400 if DB is blocked, exempted via `[DatabaseBlockedExempt]`
- `MetadataProviderUnavailableAttribute` (exception filter) — a `MetadataProviderUnavailableException` answers 502 with `Retry-After`; a `MetadataProviderNotConfiguredException` (the TMDB plugin without an API key among them) answers 503

**Action constraints**:
- `RedirectConstraint` — redirects root `/` to WebUI public path if configured

**Authentication** (`Shoko.Server/API/Authentication/`):
- `CustomAuthHandler` extracts API key from: `apikey` header, `Bearer` token, `apikey` query param, or `access_token` query param (SignalR)
- Validates against `AuthTokensRepository`; builds `ClaimsPrincipal` with user ID, role, device name
- During first-run setup, `InitUser` (synthetic admin) is used — no real auth required
- No cookie sessions; every request is authenticated by API key

**API versioning**: `v0` (version-less: auth + legacy Plex webhooks + index redirect), `v2` (legacy REST, including the `/Stream` routes, can be kill-switched), `v3` (current, all new endpoints). APIv1 has been removed. Version can be resolved from query string, `api-version` header, or custom `ShokoApiReader`. `ApiVersionControllerFeatureProvider` excludes disabled controllers at startup via individual flags (`EnableAPIv2`, `EnableAPIv3`, `EnableIndexRedirect`, `EnableLegacyPlexAPI`). The default API version (`1.0`) applies to every unversioned controller, in plugins and in the core alike, so do not change it.

**Serialization**: MVC uses `AddNewtonsoftJson()` (not `System.Text.Json`) with: `MaxDepth = 10`, `ApiContractResolver` (`Shoko.Server/API/Resolvers`), `NullValueHandling.Include`, `DefaultValueHandling.Populate`. SignalR also uses `AddNewtonsoftJsonProtocol()`, with the same resolver.

Plugin controllers are registered via `AddPluginControllers` during API setup.

### SignalR (Real-time Events)

Two hubs, only mapped when `EnableSignalR` is on:

- **`LoggingHub`** (`/signalr/logging`, admins only) — streams buffered server logs to connecting clients, separate from the aggregate hub because it can become noisy, fast.
- **`AggregateHub`** (`/signalr/aggregate`, any authenticated user) — subscription only; clients call `feed.join_single` / `feed.join_many` etc. to subscribe to event categories, and cannot call into a feed.

Event emitters (`Shoko.Server/API/SignalR/Aggregate/`) bridge internal domain events to SignalR: `AiringEventEmitter`, `AnidbEventEmitter`, `AvdumpEventEmitter`, `ConfigurationEventEmitter`, `FileEventEmitter`, `GroupEventEmitter`, `ManagedFolderEventEmitter`, `MetadataEventEmitter`, `NetworkEventEmitter`, `PluginEventEmitter`, `QueueEventEmitter`, `ReleaseEventEmitter`, `RestartEventEmitter`, `UserDataEventEmitter`, `UserEventEmitter`. An emitter can refuse a user by overriding `CanConnect`; `RestartEventEmitter` (the `restart` feed, carrying `ISystemService.RestartReasons`) takes admins only.

The feed contract lives in `Shoko.Abstractions/Web/SignalR/` (`IEventEmitter`, base class `EventEmitter`), so plugins add feeds of their own to the aggregate hub. Every emitter declares its feed's `Name`, matched ignoring case; `EventEmitterRegistry` keeps the first of two emitters sharing a name and logs a warning. By convention a plugin names its feeds after itself.

`ActorHubFilter`, registered globally, sets the current actor for every hub method call, connect and disconnect, plugin hubs included.

### Model Layers and Separation

Three distinct model layers; **do not mix them**.

**1. Persistence models** (`Shoko.Server/Models/`)
NHibernate-mapped entities. Organized by source:
- `Shoko.Server.Models.Shoko` — core domain: `AnimeSeries`, `AnimeGroup`, `AnimeEpisode`, the `*_User` data, `VideoLocal` and its places/hashes, `JMMUser`, `FilterPreset`, `CustomTag`, and the image store (`ShokoImage`, `ShokoImage_Entity`)
- `Shoko.Server.Models.AniDB` — AniDB metadata cache: `AniDB_Anime`, `AniDB_Episode`, `AniDB_Character`, `AniDB_Creator`, `AniDB_Tag`, etc.
- `Shoko.Server.Models.Airing` — airing schedules: `AiringChannel`, `AiringSchedule`, `EpisodeAiring`, `AiringScheduleSweepState`
- `Shoko.Server.Models.CrossReference` — cross-reference tables linking providers (AniDB↔MAL, and AniDB↔any plugin source, TMDB's included, through the shared `CrossRef_AniDB_Metadata_*` tables) and files/tags (`CrossRef_File_Episode`, `CrossRef_CustomTag`)
- `Shoko.Server.Models.Release` — release info for video files (`StoredReleaseInfo`, match attempts, release candidates and overrides)
- `Shoko.Server.Models.Internal` — internal tracking entities (`AuthTokens`, `ScheduledUpdate`, `Versions`)
- `Shoko.Server.Models.Metadata` — the rows behind the plugin metadata stores: series, seasons, episodes, movies, collections, people, credits, tags, studios, networks, relations, suggestions, the titles and overviews of every entry of any source (`Metadata_Title`, `Metadata_Overview`, held in memory by `TextCache`), orderings and last-refresh times

The `Embedded` sub-namespaces hold unmapped types: values stored inside a column (content ratings, release cross-references) or views built from other rows at runtime (seasons, cast and crew, studios).

NHibernate mappings live in `Shoko.Server/Mappings/` as `*Map.cs` files. Schemas should be maintained to match, as they will be migrated to Entity Framework Code-First in a future version.

**2. API response DTOs** (`Shoko.Server/API/v*/Models/`)
Never persisted; built from persistence models in controllers/services.
- `v2/Models/` — legacy APIv2 response shapes; `v2/Models/legacy/` holds the few `CL_*` contract classes APIv2 still accepts or returns
- `v3/Models/Shoko/` — modern response models (`Series`, `Episode`, `Group`, `File`, `User`, …) extending `BaseModel`
- `v3/Models/AniDB/` and `v3/Models/TMDB/` — provider-specific response shapes; the TMDB ones, with the `CrossRef_AniDB_TMDB_*` link views in `v3/Models/TMDB/CrossReferences/`, are built on the shared stores through `v3/Helpers/TmdbCompatibility.cs`
- `v3/Models/Common/` — shared types (`Images`, `Rating`, `Tag`, `Title`, etc.)
- Feature folders for the rest of v3: `Action`, `Airing`, `Auth`, `Configuration`, `Hashing`, `ImageManagement`, `Logging`, `Mylist`, `Ordering`, `Plugin`, `Release`, `Relocation`, `Streaming`, `TextManagement`

**3. Abstractions interfaces** (`Shoko.Abstractions/`)
`IShokoSeries`, `IShokoEpisode`, `IVideo`, `IUser`, etc. — implemented by persistence models, consumed by plugins and services. Plugin code should depend only on these, never on concrete `Shoko.Server` types.

### Repository Pattern

Two variants in `Shoko.Server/Repositories/`:
- **`Cached/`** — `BaseCachedRepository<T, S>` loads all rows at startup into a `PocoCache` (vendored in `Shoko.Server/Utilities/PocoCache.cs`, namespace `Shoko.Server.Utilities`). Reads are `ReaderWriterLockSlim`-protected. Each repository builds typed indexes via `PopulateIndexes()` (e.g., `_animeIDs = Cache.CreateIndex(a => a.AnimeID)`). All writes go to DB then invalidate/update the in-memory cache. Use for hot data.
- **`Direct/`** — `BaseDirectRepository<T, S>`, no cache; hits DB on every call. Use for infrequently accessed or large data.

Always prefer a cached repository over a direct one when both exist for the same entity.

Repositories use primary constructors. Side effects around saves and deletes (cascading deletes, validation, local ID assignment, follow-up jobs) go in overrides of the base class's `protected virtual` hooks: `OnBeginSave`, `OnSaveWithOpenTransaction`, `OnEndSave`, `OnBeginDelete`, `OnDeleteWithOpenTransaction` and `OnEndDelete`.

**Access pattern**: Repositories are accessed via the `RepoFactory` static class (e.g., `RepoFactory.AnimeSeries.GetByID(id)`). `RepoFactory` is DI-registered but exposes static fields for convenience — this is a legacy pattern similar to `ISystemService.StaticServices`. This exists for compatibility where DI is unavailable, but DI should be used if possible.

**Plugin storage**: plugins do not get repositories in the core's database. What only a plugin has goes in a database of its own: `services.AddPluginDbContext<TPlugin, TContext>(name)` (abstractions, no EF Core reference) registers an EF Core context that `PluginDatabaseRegistrar` puts on SQLite in WAL mode at `data/<plugin-id>/<name>.db3`. The overload taking `PluginDbContextOptions<TContext>` names derived contexts with MySQL and SQL Server migrations; while the core runs on that server, `PluginDbContextFactory` puts the database in the core's own database through the core's connection settings, every table and constraint prefixed `p<plugin hash>_<name hash>_` (`PluginTableNaming`, applied to the model and to each migration's operations), and a plugin without that server's migrations keeps its SQLite file. `PluginDatabaseMigrator` applies the migrations in `SystemService.LateStart()`, right after the core database's schema steps and data fixes (so after the first-run setup picked the backend, which is read from the settings only then), copying first (the SQLite file, or a SQL dump of the plugin's MySQL tables; SQL Server relies on per-migration transactions), and a failed migration is a start-up failure. Until then `PluginDatabaseGate` keeps every plugin context closed: the factory throws `InvalidOperationException` rather than wait, so `Setup`, `Ready` and hosted services' `StartAsync` cannot use one. Uninstalling with `purgeData` drops the plugin's prefixed tables on the next start. `PluginPaths<TPlugin>` (open generic singleton) gives a plugin its own `configuration`, `data` and `cache` folders, by the rules in `PluginPathRules`. The data every source shares goes through the stores in `Shoko.Abstractions/Metadata/Storage/` (next to the `Metadata*Data` records they take, the link store among them; the link contracts and data stay in `Shoko.Abstractions/Metadata/CrossReferences/`), and a few services in `Shoko.Abstractions/Metadata/Services/`:
- `IMetadataCrossReferenceStore` — links between AniDB entries and any source's entries, in the shared `CrossRef_AniDB_Metadata_*` tables.
- `IMetadataSeriesStore` (series with their seasons and episodes), `IMetadataMovieStore` and `IMetadataCollectionStore` — a plugin source's own entities, written by its provider during a refresh and read back whole through `IMetadataService` by `MetadataGuid`. A plugin's `IMetadataResolver` (declaring a `MetadataEntityScope` of source and kind pairs, never a core or reserved source) is asked before the stores for the pairs it took; the first in plugin load order wins each pair.
- `IMetadataImageContributor` — adds images from a plugin's own source to entries of any source, core ones included, declared as a `MetadataEntityScope`. One `DownloadContributedImagesJob<TContributor>` per contributor is queued whenever the core refreshes an entry's images; admins turn pairs off through `IMetadataImageContributorManager` (kept in `MetadataServiceSettings`).
- `IMetadataPeopleStore`, `IMetadataTagStore`, `IMetadataStudioStore`, `IMetadataRelationStore`, `IMetadataSuggestionStore` — typed stores for data every source shares, keyed by `MetadataGuid` (source, entity type, source ID).
- `IMetadataOrderingService` (in `Services/`) — alternate orderings of any series: global ones under a plugin's own source, users' local ones under `user`, plus the preferred ordering and hidden episodes.
- `IMetadataTextManager` (in `Services/`) — keeps, chooses and gathers the titles and overviews of any entry (`ChoosePreferredTitle`, `ChoosePreferredOverview`): providers write their own entries' texts, plugins may add theirs to any entry, and users pick, add, disable and remove texts.

### Scheduling

The scheduling system lives in `Shoko.QueueProcessor` — a database-backed job queue (SQLite/MySQL/SQL Server via EF Core `QueueDbContext`) with an O(1) in-memory deduplication index. Job definitions remain in `Shoko.Server/Scheduling/Jobs/`.

**Entry point — `IQueueScheduler`** (`Shoko.QueueProcessor/Abstractions/IQueueScheduler.cs`):
- `Enqueue<T>()` — enqueue with dedup; no-op if already waiting or executing.
- `EnqueueImmediate<T>()` — max-priority enqueue; returns a `Task` that completes when the job finishes.
- `RunAfterCurrent<T>()` — registers a job to run immediately after the currently-executing job. Falls back to `Enqueue` with `prioritize: true` if called outside a worker context.
- `CreateJobChain()` — returns an `IJobChainBuilder` for sequential chains.

**Job chains — `IJobChainBuilder`** (`Shoko.QueueProcessor/Abstractions/IJobChainBuilder.cs`):
Build with `.Then<T>().Then<T>()...` and submit with `.Enqueue()` (queue entry[0] normally) or `.EnqueueAfterCurrent()` (entry[0] after the current job, rest as a chain).

**Scheduled actions — `ScheduledActionService`** (`Shoko.Server/Services/ScheduledActionService.cs`, plugin surface `IScheduledActionService`; contract `IScheduledAction` in `Shoko.Abstractions/ScheduledActions/`):
Every recurring job of the core runs as a scheduled action (`IScheduledAction`, apart from `IExecutableAction`: global, no caller, admins only) with `DefaultTriggers` (interval, daily, weekly or monthly at a time in the server's time zone, at start-up, or when the queue is cleared), found in plugin assemblies by `ScheduledActionRegistry`; the admin lists, invokes (`POST …/{id}`, `IScheduledActionService.InvokeAsync`), cancels and re-triggers them under `/api/v3/Action/Scheduled` (`…/{id}/Triggers`, `…/{id}/Cancel`). A scheduled action takes no parameters; a run with options is an executable action's job. The triggers and last runs live in the `ScheduledAction` table, keyed by the same UUIDv5 an executable action's ID is derived with. A trigger firing queues a `ScheduledActionJob` (one per action, dedup, cancellable, with the job's progress); the next run counts from the last one, and a run missed while the server was down runs once at start-up. Core scheduled actions whose work is one queue job derive from `QueueJobScheduledAction<TJob>`, so a run queues that job directly. Scheduling starts with the server (`ISystemService.Started`).

**The current actor across the queue:** `IJobActorAccessor` stores the queuing caller's user ID and device on `QueuedJob` (`ActorUserId`, `ActorDeviceName`) and restores the token around the job's execution; chains and `RunAfterCurrent` inherit it. Start long-lived timers, loops and threads inside `DetachedFlow.Suppress()` so they never run as the caller who happened to start them; fire-and-forget work started for a user and meant to stay theirs wraps its delegate in `ActorContext.Carry`.

**Recurring jobs — `RecurringJobRegistry`** (`Shoko.QueueProcessor/Scheduling/RecurringJobRegistry.cs`):
Timer-based `IHostedService` on a fixed interval the admin cannot change; the core no longer uses it, plugins still may. Resolve it from DI and call `Register<T>(interval)` (from a plugin's `Setup`, for example). Registrations before `StartAsync` are armed on host boot; registrations after are armed immediately. Job types must first be registered via `AddQueueJobsFromAssembly`.

**Concurrency attributes** (`Shoko.QueueProcessor/Concurrency/`):
- `[LimitConcurrency(default, max?)]` — pool-level slot cap.
- `[DisallowConcurrentExecution]` — at most one instance running at a time.
- `[DisallowConcurrencyGroup("name")]` — mutual exclusion across jobs sharing a group name.
- `[LongRunning]` — exempts the job from the watchdog timeout.
- `[RetryPolicy]` — per-type override of the retry backoff (max attempts, base delay, delay cap).

**Acquisition filter attributes** — block dispatch until the condition is met:
- `[DatabaseRequired]` — waits until the DB is initialized.
- `[NetworkRequired]` — waits until network connectivity is confirmed.
- `[AniDBUdpRateLimited]` — respects AniDB UDP rate limits.
- `[AniDBHttpRateLimited]` — respects AniDB HTTP rate limits.
- The jobs of a metadata provider that implements `IPausableMetadataProvider` are held back while it reports itself paused (`MetadataProviderPausedAcquisitionFilter`); the TMDB plugin pauses this way for a 429 or its 5XX circuit breaker.

**`IJobFactory`** (`Shoko.QueueProcessor/JobFactory.cs`): DI-resolved single-shot execution via `Execute<T>()`. Used internally by the worker and by tests or services that need to run a job inline.

`QueueStateEventHandler` (`Shoko.QueueProcessor/Events/`) bridges job lifecycle events (added/started/completed) to `QueueEventEmitter` → SignalR clients.

The queue system lives in the QueueProcessor project, but the Shoko-specific code, like jobs or more advanced acquisition filters, is defined in Shoko.Server/Scheduling.

### Plugin System

`PluginManager` scans the `/plugins/` directory, loads assemblies, finds `IPlugin` implementations via reflection, and registers their services via `RegisterPlugins(IServiceCollection)`. `InitPlugins()` instantiates the plugins after the service container is available. `StartPlugins()` then runs every plugin's `Setup` and, when they all succeeded, every `Ready`. The plugin databases are migrated later, in `LateStart()`, right after the core database. `CorePlugin` is the built-in plugin that ships with the server. The bundled plugins (`ShokoBundledPlugin` items in `BundledPlugins.targets`) load from `<ApplicationPath>/plugins/` as system plugins, enabled by default and not uninstallable.

Plugins can also implement `IPluginApplicationRegistration` to register custom middleware via `RegisterServices(IApplicationBuilder, IApplicationPaths)` — invoked during `UseAPI()` after `UseEndpoints` but before CORS.

Plugin controllers are registered via `AddPluginControllers` during API setup. Plugins add SignalR feeds with `services.AddEventEmitter<TFeed>()`.

### Configuration System

**No `appsettings.json`** exists in the repo. Configuration is code-based:
- **`ServerSettings`** — primary settings class, persisted to `settings-server.json` via `[StorageLocation]` attribute
- **`ConfigurationProvider<T>`** — generic provider using `INewtonsoftJsonConfiguration` for JSON serialization
- **`SettingsProvider`** — singleton accessor (`ISettingsProvider.Instance`) for runtime settings access
- `appsettings.json` is configured as an **optional** overlay in the host builder but is not shipped
- **Settings migrations** — versioned JSON transforms on the persisted settings file, in `Shoko.Server/Settings/SettingsMigrations.cs`. Each entry in the `_migrations` dictionary runs once (on upgrade past its key) via `MigrateSettings`; append new migrations, never modify existing ones.

### Testing

- **Framework**: xUnit v3 (`xunit.v3`) — there is no `Xunit.DependencyInjection`; test classes take
  their dependencies through fixtures or build them directly
- **Mocking**: Moq
- **Coverage**: coverlet
- **Test SDK**: Microsoft.NET.Test.Sdk

**Where a test belongs**

| Project | Scope |
|---------|-------|
| `Shoko.Tests` | Unit tests. No database, no network, no DI container. |
| `Shoko.QueueProcessor.Tests` | The EF Core job queue, against in-memory SQLite. |
| `Shoko.IntegrationTests` | Full server bootstrap against a real database, run in CI over SQLite, MySQL and SQL Server (selected by `DB_TYPE`). |
| `Shoko.TestData` | Shared JSON fixtures consumed by tests and benchmarks. |

Prefer the cheapest option that can actually exercise the behaviour: plain unit tests first, then the
cache-backed repositories described below, and a real database only when persistence itself is the
subject.

**Testing code that reads `RepoFactory`**

Domain models resolve their navigation properties through the `RepoFactory` statics, which normally
forces a database. `Shoko.Tests/Infrastructure/` avoids that:

- `CachedRepo.Build<TRepo, TKey, TEntity>(keySelector, entities)` returns a **real** repository whose
  rows live in an in-memory `PocoCache`. Read paths, including each repository's own indexes, run
  exactly as in production. `Save`/`Delete` are not supported — mock those instead.
- `RepoFactoryScope` installs repositories into the `RepoFactory` statics and restores them on
  dispose. Its `With<TRepo, TKey, TEntity>(...)` overload builds and installs in one step.

Those statics are process-global, so every test using `RepoFactoryScope` must be annotated
`[Collection(nameof(RepoFactoryCollection))]`, which serialises them while the rest of the suite
keeps running in parallel.

Note that `ISystemService.StaticServices` is **write-once per process** — it throws on a second
assignment. Nothing in `Shoko.Tests` sets it, and new tests should keep it that way.

### Database Migrations

Supported backends: SQLite (default), MySQL/MariaDB, SQL Server, selected via `DatabaseFactory`. Each backend keeps its own schema steps in the `_patchCommands` list of `Shoko.Server/Databases/SQLite.cs`, `MySQL.cs` and `SQLServer.cs`; data fixups live in `Shoko.Server/Databases/DatabaseFixes.cs`. Every step is a `DatabaseCommand(version, revision, …)`, and the applied ones are recorded in the `Versions` table.

- Append new steps; never modify existing ones.
- One SQL statement per step. Two drops are two revisions, never `"…; …"` in one string.
- One schema version per release: add revisions to the version the release already opened rather than bumping it again.
- A schema change done in code is a `private static Tuple<bool, string?> Step(object connection)` step, which runs in order with the other schema steps. A `void` (`Action`) step is a post-database fix: it only runs after every schema step, with the new NHibernate mappings already in use, so keep those for data fixups.
- Upgrades are only supported from v5. A pre-v5 data fixup may be retired by turning its step into a bare `new(version, revision)` (a no-op that keeps the slot), but a step that drops tables is never retired.

## Domain Model Relationships

### File → Location

**`VideoLocal`** is the canonical record for a unique file, identified by its ED2K hash + file size. It holds hashes, `MediaInfo` and the import date. It does not store a path.

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

**`AnimeSeries`** is Shoko's local wrapper around an AniDB anime. Adds name/description overrides, language preferences, per-source auto-link flags, and missing episode counts. All user ratings live in `AnimeSeries_User`.

**`AnimeGroup`** is a container for series, supporting arbitrary nesting (groups within groups via `AnimeGroupParentID`). Groups can be auto-named from their main series or manually named. `AllSeries` and `AllChildren` are recursive traversals.

```
AnimeGroup (self-referential parent ──< children)
  └──< AnimeSeries (1:1 AniDB_Anime)
         └──< AnimeEpisode (1:1 AniDB_Episode)
                └──< CrossRef_File_Episode >── VideoLocal
```

### Series/Episode → plugin sources

AniDB entities are connected to other sources through cross-reference tables, not direct FKs. Every plugin source, TMDB and AniList included, shares one set of tables, keyed by `MetadataSource` and the source's own ID as a string, and written through `IMetadataCrossReferenceStore` (the APIv3 `CrossRef_AniDB_TMDB_Show`/`_Movie`/`_Episode` classes are read-only views over them):

- `CrossRef_AniDB_Metadata_Series` — `AniDB_Anime` ↔ a provider's series
- `CrossRef_AniDB_Metadata_Movie` — `AniDB_Anime` ↔ a provider's movie, kept against the AniDB episode standing for it
- `CrossRef_AniDB_Metadata_Episode` — `AniDB_Episode` ↔ a provider's episode

One anime can match multiple TMDB shows (e.g., split-cour series on TMDB) and one TMDB show can match multiple anime. A provider's series, seasons, episodes, movies, collections, people, studios, networks, tags and texts live in the shared `Metadata_*` tables, written through the stores; the TMDB plugin keeps nothing else. A plugin source may keep models of its own in its own database (`AddPluginDbContext`). Images from every source are stored as `ShokoImage` rows, linked to their entities through `ShokoImage_Entity`.

## Import Pipeline

### Job Chain

When a file appears, jobs execute in sequence via `IJobChainBuilder` / `RunAfterCurrent`:

```
File appears on disk
        │
        ▼
ScanFolderJob  (Shoko.Server/Scheduling/Jobs/Shoko/ScanFolderJob.cs)
  Walks managed folder, drops the records of files that are gone, and queues hashing for new or changed files
        │
        ▼
HashFileJob  (Shoko.Server/Scheduling/Jobs/Shoko/HashFileJob.cs)
  Computes ED2K (primary), MD5, SHA1, CRC32 via IVideoHashingService
  Only now saves the VideoLocal and its VideoLocal_Place: a VideoLocal never exists without its ED2K
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
        │  [unless SkipSupplementaryUpdate]
        ▼
SupplementaryMetadataScheduler.ScheduleForAnime  (Shoko.Server/Services/SupplementaryMetadataScheduler.cs)
  Asks every registered metadata provider for the anime (also called when a release links a file)
        │  [per metadata provider, the TMDB plugin's among them]
        ▼
SearchMetadataJob<TProvider>  (Shoko.Server/Scheduling/Jobs/Metadata/SearchMetadataJob.cs)
  For a source the anime is not linked on: asks the auto-linker's FindAutoLinks for its scored
  candidates (TMDB searches by title, dates and episode count), then MetadataLinkingService
  refuses what may not be linked, logs every candidate and links the rest (a person's search
  of one anime replaces its links on that source once something new is written)
        │
        ▼
RefreshMetadataJob<TProvider>  (Shoko.Server/Scheduling/Jobs/Metadata/RefreshMetadataJob.cs)
  One job per linked series, movie or collection, refreshed through the provider
  (writing the shared Metadata_* stores, titles and overviews included); an entry
  linked from several anime is queued once, and a series refresh queues the
  episode matching of every anime linked to it
        │
        ▼
DownloadMetadataImagesJob<TProvider>  (Shoko.Server/Scheduling/Jobs/Metadata/DownloadMetadataImagesJob.cs)
  Links the provider's image candidates and queues DownloadImageJob for the wanted ones
```

### Orchestration Pattern

Jobs do not use a central orchestrator. Each job enqueues its successor via `IQueueScheduler.RunAfterCurrent<T>()` or `IJobChainBuilder`. The provider job chain is built by `VideoReleaseService` using `CreateJobChain()`: one entry per enabled `IReleaseInfoProvider` (using the provider's dedicated `IVideoReleaseProviderJob<TProvider>` class if registered, otherwise `ProcessReleaseProviderJob`), with `FinalizeReleaseSearchJob` appended as the terminal step. Provider jobs read the `AnimeID` from the release info and enqueue `GetAniDBAnimeJob` when a new anime is encountered.

**Import sweep.** What the live pipeline missed is caught by scheduled actions of their own, run by hand or on the triggers an admin sets. The legacy `RunImport` routes queue these in the old import's order through `LegacyScheduledActions.InvokeImport` (`Shoko.Server/API/LegacyScheduledActions.cs`), logging any that fails and going on: "Hash Unhashed Files", "Scan Managed Folders" (`IVideoService.ScheduleScanForManagedFolders()`: drop sources in full, the rest for new files), "Search for Metadata Matches" (every source that auto-links), "Purge Expired Orphaned Metadata", "Download All Images", "Check for Previously Ignored Files" and "Check AniDB File Updates" (release searches for files without an episode, then the anime those files need). "Import New Files" scans for new files only.

**`ScanForMissingReleaseInfoJob`** (`Shoko.Server/Scheduling/Jobs/Actions/ScanForMissingReleaseInfoJob.cs`) runs daily as the `ScanForMissingReleaseInfoAction` scheduled action; it finds `StoredReleaseInfo` records with unknown source or missing audio/subtitle languages and re-queues the appropriate provider job on each provider's backoff schedule (`GetRescanDelay()`).

**`RefreshAnimeAiringSoonAction`** (`Shoko.Server/Actions/AniDB/RefreshAnimeAiringSoonAction.cs`, "Refresh Anime Airing Soon") runs every 24 hours by default (one hour at the least); it reads `IAiringScheduleService.GetAiringsInRange` for the next `AiringSoonWindowHours` (`AiringScheduleServiceSettings`, 24 by default; date-only AniDB entries only with `AiringSoonIncludeDateOnly`, counted when their UTC day overlaps the window), takes each airing's own AniDB anime and every anime linked to its series, and queues a forced online `GetAniDBAnimeJob` (`IgnoreTimeCheck`, never `IgnoreHttpBans`) for each one in the collection last updated from AniDB at least `MinimumHoursToRedownloadAnimeInfo` hours ago (one at the least), so the supplementary cascade and episode matching follow. A run while AniDB's HTTP API bans us is skipped.

### Intermediate Cache Models

Several models exist solely to avoid redundant I/O or external API calls. Jobs check these before making outbound requests.

**`FileNameHash`** (`Models/Shoko/FileNameHash.cs`) — maps `FileName + FileSize → ED2K hash`. Written by `VideoHashingService.SaveFileNameHash()` and `VideoRelocationService` after a file is successfully hashed. Read by `AnidbReleaseProvider` as a last-resort local lookup when checking for creditless/variant files before going to AniDB.

**`VideoLocal_HashDigest`** (`Models/Shoko/VideoLocal_HashDigest.cs`) — stores all computed hash types (ED2K, CRC32, MD5, SHA1) for a `VideoLocal` as `Type + Value` rows. Written by `VideoHashingService` during `HashFileJob`. Read when displaying or cross-referencing file hashes without recomputing.

**`StoredReleaseInfo`** (`Models/Release/StoredReleaseInfo.cs`) — caches the full release provider response: ED2K + FileSize, provider ID, release URI, source (BluRay/Web/etc.), codec flags (`IsCensored`, `IsCreditless`, `IsChaptered`), version, and cross-references to anime/episodes. Written by `IVideoReleaseService.FindReleaseForVideo()` inside provider jobs. Provider jobs call `GetCurrentReleaseForVideo()` first — if a `StoredReleaseInfo` already exists for the hash, the lookup is skipped entirely. Queried by the API via `GetByEd2kAndFileSize()`, `GetByReleaseURI()`, `GetByAnidbEpisodeID()`.

**`StoredReleaseInfo_MatchAttempt`** (`Models/Release/StoredReleaseInfo_MatchAttempt.cs`) — tracks per-provider match attempts for a file: `ProviderName`, `ProviderID`, `AttemptCount`, `AttemptStartedAt`, `AttemptEndedAt`, `EmbeddedAttemptProviderNames`. Written by provider jobs at the start of each attempt. Read by `ScanForMissingReleaseInfoJob` to apply per-provider backoff logic and skip providers that have already found a result.

**`AniDB_AnimeUpdate`** (`Models/AniDB/AniDB_AnimeUpdate.cs`) — one row per `AnimeID`, storing only `UpdatedAt`. Written by `RequestGetAnime.UpdateAccessTime()` after every successful AniDB HTTP response. Read by the same method, and by `RefreshAnimeAiringSoonAction`, to decide whether the local `AniDB_Anime` record is stale enough to warrant a new fetch. `GetAniDBAnimeJob` respects `IgnoreTimeCheck` to force a refresh past this gate.

**`AniDB_GroupStatus`** (`Models/AniDB/AniDB_GroupStatus.cs`) — caches AniDB GROUPSTATUS UDP responses: release group name, completion state, episode range, rating. Written by `GetAniDBReleaseGroupStatusJob` after a UDP `RequestReleaseGroupStatus` call. `GetAniDBReleaseGroupStatusJob.ShouldSkip()` bypasses the fetch entirely if the anime ended more than 50 days ago.

**`AniDB_NotifyQueue`** + **`AniDB_Message`** — `AniDB_NotifyQueue` (`Models/AniDB/`) is a staging table for raw AniDB notification IDs (type + ID) written by `GetAniDBNotifyJob`. `AniDB_Message` stores the fully fetched message body (sender, title, body, `FileMoved`/`FileMoveHandled` flags). `AcknowledgeAniDBNotifyJob` and `ProcessFileMovedMessageJob` drain these two tables in sequence.

**`ScheduledUpdate`** (`Models/Internal/ScheduledUpdate.cs`) — tracks `LastUpdate` timestamps for periodic background tasks (one row per `UpdateType`). Written after each scheduled job completes. Read at job start to determine whether enough time has elapsed to run again.

### Unrecognized Files

If no release provider returns a match, the provider chain completes without creating a `StoredReleaseInfo` record and the file is considered unrecognized. The file can be linked to one or more episodes via the API or by plugins.

**AVDump** (`AVDumpFilesJob`) is an on-demand utility that submits a file's media info and hashes to AniDB for manual entry. It is unrelated to unrecognized file handling and only runs on explicit user/plugin request.

### Concurrency Limits

| Job | Default Concurrency (Max Concurrency) | Reason |
|-----|---------------------------------------|--------|
| `HashFileJob` | 2 | I/O bound |
| `MediaInfoJob` | 2 | I/O bound |
| `AnidbProcessFileJob` | 4, and group-limited (`AniDB_UDP`) | AniDB UDP rate limit |
| `GetAniDBAnimeJob` | group-limited (`AniDB_HTTP`) | AniDB HTTP bulkhead |
| `AVDumpFilesJob` | 1 (16) | AVDump resource limits |
| `RefreshMetadataJob<T>`, `SearchMetadataJob<T>`, `DownloadMetadataImagesJob<T>` | The provider's `MaxConcurrentJobs` (TMDB: 4) | One pool per provider job type |
| `DownloadContributedImagesJob<T>` | The contributor's `MaxConcurrentJobs` (default 2) | One pool per image contributor |
| `DownloadImageJob` | 4 | Image download throughput |
| `ValidateAllImagesJob` | 1 (1) | Sequential validation |
