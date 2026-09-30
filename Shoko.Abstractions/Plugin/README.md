# The `Shoko.Abstractions.Plugin` Namespace

The plugin system's own types: `IPlugin`, the two static registration hooks,
the paths and database a plugin gets, the services that describe and manage
installed plugins, and the package and repository models. How a plugin is found,
constructed and started is in the [plugin overview](../README.md).

| Type | Your plugin |
|---|---|
| `IPlugin` | **Implements.** Exactly one class per plugin assembly. |
| `IPluginServiceRegistration` | **Implements**, optionally, on any one exported class, usually the plugin class. Static abstract. |
| `IPluginApplicationRegistration` | **Implements**, optionally, on any one exported class, usually the plugin class. Static abstract. |
| `IApplicationPaths` | **Consumes.** Injected, or handed to the two registration hooks. |
| `PluginPaths<TPlugin>` | **Consumes.** Injected, for your own configuration, database and cache folders. |
| `AddPluginDbContext<TPlugin, TContext>` | **Calls**, optionally, from `RegisterServices`, for a database of your own. |
| `PluginDbContextOptions<TContext>` | **Configures**, in the overload that names your MySQL and SQL Server contexts. |
| `IPluginManager` | **Consumes.** Core-provided singleton. |
| `IPluginPackageManager` | **Consumes.** Core-provided singleton. |
| `IPluginDependencyResolver` | **Consumes.** Core-provided singleton. |
| `Models/`, `Events/`, `Enums/`, `Exceptions/` | Data passed in both directions. |

---

## `IPlugin`

```csharp
public class MyPlugin : IPlugin
{
    // Stable for the life of the plugin. It keys configuration, settings and
    // dependency declarations, so generate it once and never change it.
    public Guid ID { get; } = new("1a2b3c4d-0000-0000-0000-000000000000");

    public string Name => "My Plugin";

    public string Description => "Does a useful thing.";

    // Absolute resource names, assembly name included. Both optional.
    public string? EmbeddedThumbnailResourceName => "MyPlugin.assets.Thumbnail.png";

    public string? EmbeddedIconResourceName => "MyPlugin.assets.Icon.png";

    public IReadOnlyList<PluginPage> GetPages() =>
    [
        new() { Name = "My Plugin", Url = "/webui/my-plugin" },
    ];

    public IReadOnlyList<PluginFeature> GetFeatures() =>
    [
        new()
        {
            Name = "my-sync",
            Version = new(1, 2, 0),
            Visibility = PluginFeatureVisibility.Authenticated,
            Metadata = JObject.FromObject(new { supportsBackfill = true }),
        },
    ];
}
```

`ID` and `Name` are the only required members; the rest have defaults. The
class needs a public parameterless constructor and takes its services in
`Setup(IServiceProvider)`; see the [plugin overview](../README.md).

### Pages

A `PluginPage` is a link the Web UI offers the user: a `Name`, a relative or
absolute `Url`, and `CanEmbed` (default `true`) which decides whether the page
is shown in an iframe or opened in a new window. Set it to `false` for anything
that sets frame-busting headers or needs its own top-level origin.

The list is de-duplicated by `Name` and then by `Url`, first one wins, so two
pages sharing either lose one of them. Each surviving page becomes a
`LocalPluginPage` whose `ID` is a deterministic UUIDv5 of the URL within your
plugin's ID: stable across restarts, and it changes when you change the URL.

### Features

A `PluginFeature` tells clients what the server can do, so a client can turn UI
on without sniffing for endpoints. `GetFeatures()` is called every time a client
asks, so leave out a feature the current configuration cannot deliver.

- **`Name`** must be lowercase kebab-case and at most 64 characters, so it can
  go straight into a URL. It is unique per plugin, and identity is the pair of
  your plugin ID and this name.
- **`Version`** defaults to `1.0.0`. Bump the major on a breaking change,
  including a change to the shape of `Metadata`, and the minor on an addition.
  Only major, minor and patch are advertised.
- **`Visibility`** is `Anonymous`, `Authenticated` (the default) or `Admin`, and
  each level includes the ones below it.
- **`Metadata`** is an optional `JObject` of parameters, advertised at the same
  visibility as the feature. Never put anything in the metadata of an
  `Anonymous` feature that an unauthenticated caller should not read.

Anything invalid is dropped rather than raised: a null or malformed name, a null
version, an undefined visibility, or a second feature reusing a name already
taken. `PluginFeature.IsValid(feature, out var error)` is public, so you can
check your own before returning them.

### The two registration hooks

`IPluginServiceRegistration` and `IPluginApplicationRegistration` each declare a
single `static abstract RegisterServices` method, taking an `IServiceCollection`
or an `IApplicationBuilder` alongside `IApplicationPaths`. Each is taken from
the first exported type implementing it. What belongs in each is in the
[plugin overview](../README.md#where-dependencies-go-instead).

---

## `IApplicationPaths`

Every directory the server uses, resolved for the current install. Inject it, or
take the one handed to `RegisterServices`, rather than building paths from
`AppContext.BaseDirectory`: the user can move the data directory.

| Property | Directory |
|---|---|
| `ApplicationPath` | The executable's parent directory. |
| `DataPath` | The data directory, the root of everything writable. |
| `ConfigurationsPath` | Configuration files, `configuration`. |
| `PluginsPath` | Installed plugins, `plugins`. |
| `DatabasePath` | Databases, `data`: the plugins' own, one folder per plugin ID. |
| `CachePath` | Caches anyone may empty at any time, `cache`, one folder per plugin ID. |
| `ImagesPath` | The image cache. |
| `StreamCachePath` | Transcode and rendition cache, `transcodes`. |
| `WebPath` | Web UI resources. |
| `ThemesPath` | Web UI themes, `themes`. |
| `LogsPath` | Log files, `logs`. |

The folder names are relative to `DataPath`. What goes where:

- **`plugins`** holds installed plugins and nothing else. An update or an
  uninstall replaces or removes a plugin's folder, so a plugin never writes
  anything of its own there.
- **`configuration/<plugin-id>`** holds a plugin's configuration files; the
  server writes them through `ConfigurationProvider<T>`.
- **`data/<plugin-id>`** holds what a plugin cannot fetch again: its databases
  and any other state. The server copies it with its own database backups.
- **`cache/<plugin-id>`** holds what a plugin can fetch or build again. Backups
  leave it out, and the user may empty it.

The core's own database stays wherever the database settings put it; `data` is
for the databases that are new. `PluginPaths<TPlugin>` below hands a plugin its
own three folders.

A `PackageImageInfo` path may contain `%PluginsPath%` or `%ApplicationPaths%`,
which `GetStream(IApplicationPaths)` expands.

### Thumbnail and icon

Both images are optional. The **thumbnail** is wide, shown where there is room;
the **icon** is square, shown beside the plugin's name. The server neither crops
nor scales them, so keep the icon readable at 16 pixels. A file on disk wins
over an embedded resource:

| | in a plugin directory | as a loose dll |
|---|---|---|
| thumbnail | `thumbnail.*` | `<dll name>.thumbnail.*` |
| icon | `icon.*` | `<dll name>.icon.*` |

An embedded resource, named by `EmbeddedThumbnailResourceName` or
`EmbeddedIconResourceName`, must be rooted in the plugin's assembly name, and is
written out beside the plugin the first time it is read. PNG, JPEG, WebP and SVG
are accepted.

They are served from `/api/v3/Plugin/{pluginID}/Thumbnail` and
`/api/v3/Plugin/{pluginID}/Icon`, and from the same two paths under a version.

---

## `PluginPaths<TPlugin>`

The directories one plugin uses. Inject `PluginPaths<MyPlugin>`; there is
nothing to register.

```csharp
public class MyCache(PluginPaths<MyPlugin> paths)
{
    public string ThumbnailFile(int id)
        => paths.GetCacheFile(Path.Join("thumbnails", $"{id}.jpg"));
}
```

It has the same property names as `IApplicationPaths`. Three are the plugin's
own, named by its ID and created the first time they are read:
`ConfigurationsPath` (`configuration/<plugin-id>`), `DatabasePath`
(`data/<plugin-id>`) and `CachePath` (`cache/<plugin-id>`). `ApplicationPath`,
`DataPath`, `WebPath`, `ImagesPath`, `StreamCachePath`, `ThemesPath` and
`LogsPath` are the server's. There is no `PluginsPath`: `InstallPath` is the
plugin's own install folder, or the folder holding its dll when it is a single
file, and a plugin only reads from it. `PluginID` is the plugin's ID.

`GetDatabaseFile(relativePath)` and `GetCacheFile(relativePath)` resolve a file
inside the plugin's database or cache folder, creating the folder the file sits
in, and refuse a rooted path or one that leaves the folder.

Resolving it for a type that is not a loaded plugin throws
`InvalidOperationException`.

---

## A database of your own

A plugin that keeps data of its own gets a database the server looks after: a
SQLite file under `DatabasePath`, or tables in the server's own MySQL or SQL
Server database when the plugin ships migrations for it. The server migrates
it right after its own database, backs it up, and removes it with the plugin
when the user asks.

### Registering it

Write an Entity Framework Core `DbContext` that takes
`DbContextOptions<TContext>`, and register it from `RegisterServices`:

```csharp
public class NotesContext(DbContextOptions<NotesContext> options) : DbContext(options)
{
    public DbSet<Note> Notes => Set<Note>();
}

public class Plugin : IPlugin, IPluginServiceRegistration
{
    public static void RegisterServices(IServiceCollection services, IApplicationPaths applicationPaths)
        => services.AddPluginDbContext<Plugin, NotesContext>("notes");
}
```

The name is the file name without its extension, unique within the plugin; a
new name is a new, empty database. The server configures the context: registered
like this it is SQLite in WAL mode, in `data/<plugin-id>/notes.db3`, whatever
the server itself runs on. Never call `UseSqlite` or another provider method.

Inject `IDbContextFactory<NotesContext>` into singletons and hosted services,
and create a context per unit of work. A scoped service can take
`NotesContext` itself. Neither hands out a context before the server has
migrated the database (see [when they run](#when-they-run-and-when-one-fails)).

### On the server's MySQL or SQL Server database

Migrations are generated for one provider: a SQLite migration carries SQLite
column types and annotations that MySQL and SQL Server refuse. So each server
gets a context of its own, derived from yours, with its own migrations, and
the registration names them:

```csharp
public class MySqlNotesContext(DbContextOptions<NotesContext> options) : NotesContext(options);

public class SqlServerNotesContext(DbContextOptions<NotesContext> options) : NotesContext(options);

services.AddPluginDbContext<Plugin, NotesContext>("notes", options => options
    .WithMySqlMigrations<MySqlNotesContext>()
    .WithSqlServerMigrations<SqlServerNotesContext>());
```

A derived context adds nothing to the model. It takes your context's options,
as above, or its own `DbContextOptions<MySqlNotesContext>` handed to a
`protected NotesContext(DbContextOptions options)` constructor. `NotesContext`
keeps the SQLite migrations. Name only the servers you have migrations for.

While the server runs on MySQL (MariaDB included) or SQL Server and you named a
context for it, the database lives in the server's own database, through its
connection. Every table, constraint and sequence, the migrations history
included, gets a prefix of the plugin and the database, `p<8 hex>_<6 hex>_`
(`p1a2b3c4d_9f8e7d_Notes`); index names stay as they are. Your code sees none
of it: the server renames the tables in the model and in each migration, and
`IDbContextFactory<NotesContext>` and `NotesContext` hand out the server's
context. Without a context for that server, the database stays in its SQLite
file, and the log says so.

Data is never moved between them: a database that follows the server onto MySQL
starts empty there, and a SQLite file left from before is only reported.

For the migrations of a server context:

- no `HasDefaultSchema` or `ToTable(name, schema)`; a migration that creates or
  drops a schema is refused, as it could reach the server's own;
- `migrationBuilder.Sql` runs as written, unprefixed, so leave table names out
  of it, or keep it to SQLite;
- what the generated MySQL migration sets on the database itself, its
  character set, is dropped: the database is the server's, not yours;
- a table name longer than the server allows (64 on MySQL, 128 on SQL Server)
  once prefixed keeps its start and ends in a hash, so keep them short.

### Packages

The server already loads Entity Framework Core and its SQLite, MySQL and SQL
Server providers (the queue uses them), and a plugin shares the server's
assemblies, so reference them for compiling only:

```xml
<PackageReference Include="Microsoft.EntityFrameworkCore.Sqlite" Version="10.0.12" ExcludeAssets="runtime;native" PrivateAssets="all" />
<!-- Only with migrations for the servers: -->
<PackageReference Include="Microting.EntityFrameworkCore.MySql" Version="10.0.11" ExcludeAssets="runtime" PrivateAssets="all" />
<PackageReference Include="Microsoft.EntityFrameworkCore.SqlServer" Version="10.0.12" ExcludeAssets="runtime;native" PrivateAssets="all" />
```

Use the server's versions: a plugin built against a newer one fails to load
its context. A plugin referencing `Shoko.QueueProcessor` (with
`ExcludeAssets="runtime;native"`) gets these through it and can skip them.

### Migrations

`dotnet ef` needs the assemblies your plugin leaves out, so give it a small
design project next to the plugin, which is never shipped:

```xml
<!-- design/MyPlugin.Design.csproj -->
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <OutputType>Exe</OutputType>
    <IsPackable>false</IsPackable>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="../plugin/MyPlugin.csproj" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.Sqlite" Version="10.0.12" />
    <PackageReference Include="Microting.EntityFrameworkCore.MySql" Version="10.0.11" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.SqlServer" Version="10.0.12" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.Design" Version="10.0.12" PrivateAssets="all" />
  </ItemGroup>
</Project>
```

```csharp
// design/DesignTimeFactory.cs, one factory per context
public class NotesContextFactory : IDesignTimeDbContextFactory<NotesContext>
{
    public NotesContext CreateDbContext(string[] args)
        => new(new DbContextOptionsBuilder<NotesContext>().UseSqlite("Data Source=design-time.db3").Options);
}

public class MySqlNotesContextFactory : IDesignTimeDbContextFactory<MySqlNotesContext>
{
    // No server is needed; the version only picks the SQL the migrations are written for.
    public MySqlNotesContext CreateDbContext(string[] args)
        => new(new DbContextOptionsBuilder<NotesContext>().UseMySql("Server=localhost", new MySqlServerVersion(new Version(8, 0))).Options);
}

public class SqlServerNotesContextFactory : IDesignTimeDbContextFactory<SqlServerNotesContext>
{
    public SqlServerNotesContext CreateDbContext(string[] args)
        => new(new DbContextOptionsBuilder<NotesContext>().UseSqlServer("Server=localhost").Options);
}

public static class Program
{
    public static void Main() { }
}
```

Then add each migration to the plugin project, once per context, each into
its own folder:

```sh
dotnet tool install --global dotnet-ef --version 10.0.12
dotnet ef migrations add CreateNotes --project plugin --startup-project design --context NotesContext -o Migrations/Sqlite
dotnet ef migrations add CreateNotes --project plugin --startup-project design --context MySqlNotesContext -o Migrations/MySql
dotnet ef migrations add CreateNotes --project plugin --startup-project design --context SqlServerNotesContext -o Migrations/SqlServer
```

A plugin referencing `Shoko.QueueProcessor` needs it in the design project as
well, as a plain reference without `ExcludeAssets`, or `dotnet ef` fails to
load the plugin's assembly:

```xml
<!-- The same version the plugin references. -->
<PackageReference Include="Shoko.QueueProcessor" Version="6.0.0" />
```

The migrations, designer files and model snapshots go in the plugin; the design
project only runs the tool. Never edit a migration a release has shipped; add
another, for every context.

### When they run, and when one fails

The server applies every plugin database's pending migrations in its late
start, right after its own database's schema steps and data fixes and before
`AboutToStart`, one migration at a time, in the order the plugins registered
their databases. After a first-run setup that is the backend the setup picked.
Each copy and migration shows as a start-up message, such as
`Migrating plugin database <plugin>/notes: 20260901000000_CreateNotes (1/2)...`.

Until then `IDbContextFactory<NotesContext>` and an injected `NotesContext`
throw `InvalidOperationException` rather than wait, since `IPlugin.Setup`,
`IPlugin.Ready` and every hosted service's `StartAsync` run before the
migrations, where a wait would never end. Use a database from `AboutToStart`
on: in an `AboutToStart` or `Started` handler, after `WaitForStartupAsync()`,
or from a queue job marked `[DatabaseRequired]`.

Before a database's first pending migration, the server copies it (the backup
folder follows the database backup setting) and keeps the newest three copies:

- SQLite: the file, to
  `DatabaseBackup/plugin-databases/<plugin-id>/<name>_<yyyyMMddHHmmss>.db3`;
- MySQL: the plugin database's tables, structure and rows, as a SQL dump that
  recreates them, to `…/<name>_<yyyyMMddHHmmss>.sql`, since MySQL cannot roll
  back a migration's schema changes;
- SQL Server: nothing. Each migration runs in a transaction, so a failed one
  leaves the tables as they were, and the server's own backups hold them.

A new database, with nothing in it yet, is not copied.

A failed migration stops the server's start-up and the databases stay closed.
The process stays up, every endpoint not marked `[InitFriendly]` answers `503`,
and the start-up message names the plugin, context, migration and error, and
says how to recover:

- SQLite: stop the server and copy the copy it names over the database file,
  then fix the plugin;
- MySQL: stop the server, drop the tables with the database's prefix, which
  the message names, and load the dump it names into the server's database;
- SQL Server: the failed migration was rolled back; restore a backup of the
  server's database to go further back;
- or, on any of them, disable the plugin and restart. The plugin API
  (`PATCH /api/v3/Plugin/{id}`, or `PUT` with `IsEnabled: false`) stays open
  while the start-up has failed, and so does `POST /api/v3/Init/Restart` where
  restarting is allowed. Without the API, set the plugin's dll name to `false`
  under `Plugins.EnabledPlugins` in `settings-server.json`, which is what the
  API writes.

The migrations that ran before the failing one stay applied.

### Backups and uninstalling

When the server backs up its own database before upgrading it, it copies every
folder under `data` next to that backup, as `<backup>.plugin-data`. Caches are
left out. On MySQL and SQL Server the plugin tables are in the server's
database, so its backup holds them already.

`IPluginManager.UninstallPlugin(pluginInfo, purgeData: true)`, or
`purgeData=true` on the uninstall endpoints (admins only), removes the plugin's
database and cache folders, and on MySQL and SQL Server drops every table with
its prefix. It happens on the next start, since the plugin is still running
until then, and tables that could not be dropped are tried again the start
after. The endpoints refuse it with `403` during setup or after a failed
start-up, when callers are let in without an API key. It is skipped while
another installed version still uses the data, and taken back if the plugin is
installed again before the restart.

---

## `IPluginManager`

The registry of what is installed, loaded and exported. A plugin mostly uses it
to learn about itself and whether some other plugin is around.

```csharp
public class MyService(IPluginManager pluginManager)
{
    public void LogWhoIAm()
    {
        if (pluginManager.GetPluginInfo<MyPlugin>() is { } me)
            logger.LogInformation("{Name} {Version}", me.Name, me.Version);

        // Optional interop: only light up if the other plugin is actually loaded.
        if (pluginManager.GetPluginInfo(OtherPluginIds.Something) is { IsActive: true })
            EnableTheIntegration();
    }
}
```

### Plugin info

`GetPluginInfos()` lists everything registered. `GetPluginInfos(Guid)` returns
every registered version of one plugin, while the singular lookups return the
active version (or the highest, when none is active):
`GetPluginInfo(Guid, Version?)`, `GetPluginInfo(IPlugin)`,
`GetPluginInfo<TPlugin>()`, `GetPluginInfo(Type)` and `GetPluginInfo(Assembly)`.

`LocalPluginInfo` is the answer to all of them. Its state flags:

| Flag | Meaning |
|---|---|
| `IsInstalled` | Still on disk, i.e. not uninstalled during this session. |
| `IsEnabled` | Enabled for this session **or the next one**. |
| `IsActive` | Actually loaded right now. Implies `Plugin` and `PluginType` are non-null. |
| `IsPinned` | The version is pinned against automatic upgrades. |
| `CanLoad` | Loadable by this runtime: assemblies present, ABI compatible, dependencies satisfied. When it is not, `CannotLoadReason` says why, if known. |
| `RestartPending` | A restart would change whether it is loaded: enabled, not loaded and loadable, or disabled but still loaded. An enabled plugin that cannot load is not waiting on a restart. The server-wide view is the single `PluginState` reason in `ISystemService.RestartReasons`, which follows the same rule. |
| `CanUninstall` | Removable by the user. |

It also carries `Version`, `Authors`, `RepositoryUrl`, `HomepageUrl`, `Tags`,
`Thumbnail`, `InstalledAt`/`UninstalledAt`, `LoadOrder` (dependencies always
ahead of their dependents), the `DLLs` and `Types` of the assembly, the declared
`Dependencies`, and the resolved `Plugin` instance with its `PluginType`,
`ServiceRegistrationType` and `ApplicationRegistrationType`. `GetPages()` and
`GetFeatures()` on it return the validated `LocalPluginPage` and
`LocalPluginFeature` forms.

### Types and exports

`GetTypes<T>()` returns every type across all loaded plugins assignable to `T`,
and the `IPlugin` overload narrows that to one plugin. `GetExports<T>()` and its
per-plugin overload do the same and then instantiate each type through
`ActivatorUtilities.GetServiceOrCreateInstance`, so constructor injection works
and a type that fails to construct is logged and skipped rather than throwing.
`GetExport<T>(Type)` does one type, and returns `null` when it is not assignable
to `T`.

**`GetExports<T>()` hands you a *new* instance for any type not registered in
DI**, not the one the core holds. To share one object with the core, register
the concrete type as a singleton; see
[Contracts the server discovers for you](../README.md#contracts-the-server-discovers-for-you).

`GetService` and `GetRequiredService` are a service-locator escape hatch over
the root container, for code that cannot take constructor injection.

### Compatibility

`AbstractionVersion` is the ABI the running server exposes, `RuntimeIdentifier`
is its platform, and `IPluginManager.AnyRuntimeIdentifier` (`"any"`) is the
wildcard a platform-neutral package declares.
`IsAbiAndRuntimeCompatible(version, runtimeIdentifier)` answers whether a given
pair could be loaded here, which is what the package layer uses to filter
release archives.

### Lifecycle management, and what not to call

`EnablePlugin`, `DisablePlugin`, `PinPlugin`, `UnpinPlugin` and
`UninstallPlugin(pluginInfo, purgeConfiguration, purgeData)` each return the
updated `LocalPluginInfo`. They are for a management UI, not a plugin managing
itself:

- **They take effect from the next session.** Enabling does not load anything
  now, disabling does not unload anything now, and an uninstalled plugin stays
  active until restart. Watch `RestartPending`, or the `PluginState` reason
  in `ISystemService.RestartReasons`, which stands while any plugin's next
  start would load a different version, or none.
- **Enabling pins**, if the version is not the latest; disabling unpins.
- The built-in core plugin cannot be toggled or uninstalled.

`UninstallPlugin` surfaces an `IOException` when the plugin's files cannot be
removed from disk. `LoadFromPath(string)` loads plugin info from a path inside
the user plugin directory, and fails for anything outside it.

`PluginInstalled` and `PluginUninstalled` are raised with a
`PluginInstallationEventArgs` carrying the `Plugin` and `OccurredAt`.
`PluginEnabled` and `PluginDisabled` are raised with a `PluginToggledEventArgs`
of the same shape, and only when the state changed: toggling a plugin to the
state it is already in raises nothing. Enabling one version disables the
others, so that call raises `PluginDisabled` for each version it turned off
before the `PluginEnabled` of the one asked for. All four are raised off the
calling thread, and carry `Actor`, the API token of whoever made the change,
or `null` for the system.

---

## `IPluginPackageManager`

Packages are the distribution layer: repositories serve manifests, a manifest
lists releases, and a release lists per-runtime archives.

```
PackageRepositoryInfo  a feed, with its own StaleTime and LastFetchedAt
   └─ PackageManifestInfo   one package: id, name, overview, authors, tags, thumbnail
        └─ PackageReleaseInfo   one version: channel, release notes, dependencies
             └─ PackageArchiveInfo   one build: runtime id, ABI version, url, checksum

PackageInfo  = repository + manifest + release + archive (+ the local plugin, if installed)
```

**Repositories.** `ListPackageRepositories()`,
`AddPackageRepository(PackageRepositoryData)`,
`RemovePackageRepository(PackageRepositoryInfo)`,
`SyncPackageRepository(repository, forceSync)` and
`SyncAllPackageRepositories(forceSync)`. A sync is skipped when the cached copy
is younger than the repository's `StaleTime` (falling back to
`DefaultRepositoryStaleTime`), which is what `forceSync` overrides.

**Discovery.** `GetAvailablePackageManifests(allowSync, forceSyncNow)` returns
the manifests, and `FilterPackageManifests(manifests, onlyCompatible, onlyLatest)`
flattens them into `PackageInfo` entries, by default dropping releases this
server could not load and keeping only the newest per package.
`GetLocalPackages()` and `GetInstalledPackages()` cover what is already here.

**Installing and updating.** `InstallPackage(packageInfo)` returns the resulting
`LocalPluginInfo`, or `null`. `GetAvailableUpdates(...)` reports
`PackageUpdateInfo` entries pairing `Current` with `Latest`, optionally
including inactive and pinned plugins. `CheckForUpdates` runs the check now,
`ScheduleCheckForUpdates` queues it, and both can force a sync and perform the
upgrade. `IsAutoSyncEnabled`, `IsAutoUpgradeEnabled`,
`DefaultRepositoryStaleTime` and `InactivePluginVersionRetention` are the
settings behind the automatic behaviour.

**Events.** `PackageInstallationStarted` / `Completed` / `Failed`, and
`RepositorySyncStarted` / `Completed` / `Failed`. The two `Started` args carry a
settable `Cancel` flag, so a handler can veto an install or a sync;
`RepositorySyncStartedEventArgs` also exposes `ForceSync` and a `StaleTime` that
rejects a negative value. The `Failed` args carry a `Reason` string and the
`Exception`, which is `InvalidChecksumException` (with `ExpectedChecksum` and
`ActualChecksum`) when a downloaded archive does not match its manifest.

---

## `IPluginDependencyResolver`

Plugin-to-plugin dependencies. A `PluginDependency` is a target `PluginID`, a
`VersionRange` constraint (`">=1.0.0"`, `"^2.0.0"`, `"1.5.0"`) and an
`IsOptional` flag; a missing optional dependency warns instead of blocking.

`ResolveDependencies` takes either a `LocalPluginInfo` or a raw dependency list
and returns a `DependencyResolutionResult`: `CanResolve` for the overall answer,
`Dependencies` for the per-entry `ResolvedDependency` (each with `IsResolved`,
the matching `Plugin`, and a human-readable `Message` when it failed), and
`MissingDependencies` for the required ones that came up short.

`ValidateEnable(plugin)` returns the same result shape for the enable check.
`ValidateDisable(plugin)` returns the enabled plugins that depend on it, empty
when disabling is safe. `GetDependents(plugin)` returns the plugins that depend
on it, and `GetUninstallCascade(plugin)` the full transitive set, excluding the
plugin itself. `IsVersionSatisfied(versionRange, candidate)` is the range check
on its own.

Resolution only ever considers the currently installed and enabled plugins, so
the answers change as the user installs and removes things.

---

For writing the plugin itself, start at the [plugin overview](../README.md).
