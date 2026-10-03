# Writing a Shoko Plugin

This is the contract between Shoko Server and the plugins that extend it.
Everything in this package is an interface, a model or an enum: the server
implements them, your plugin consumes them, and neither side needs a reference
to the other's concrete types.

## Where to go next

This file covers what every plugin has to get right. The pages below cover one
contract or service each.

### Contracts a plugin implements

- [Relocation providers](Video/Relocation/README.md), deciding where a video
  file should live and what it should be called
- [Hash providers](Video/Hashing/README.md), computing digests of a video file
- [Release info providers](Video/Release/README.md), identifying which episodes
  a video file contains
- [Airing schedule providers](Metadata/Airing/README.md), tracking when episodes
  air, including delays and simulcasts
- [Managed folder ignore rules](Video/README.md), keeping paths out of a scan
- [Video stream transforms and playback observers](Video/Streaming/README.md),
  reshaping a stream on its way out, or reacting to what was served
- [Metadata resolvers](Metadata/Providers/README.md#resolving-your-own-kinds),
  serving entries of kinds the server holds nothing for, which also lets
  images and airing schedules attach to them
- [Metadata image contributors](Metadata/Providers/README.md#adding-images-to-other-sources-entries),
  adding images from a source of the plugin's own to entries of other sources,
  AniDB's included
- [Resource resolvers](Metadata/Resources/README.md), contributing external
  links to an entity
- [Metadata providers](Metadata/Providers/README.md), fetching series, episodes,
  movies and their images from a source the core does not serve, in jobs the
  core runs and into stores the core keeps
- [Executable actions](Actions/Services/README.md), exposing a named unit of
  work a user or client can invoke
- [Scheduled actions](ScheduledActions/Services/README.md), work that runs on
  its own on triggers the admin sets
- [Feeds on the aggregate hub](Web/SignalR/README.md), sending live events to
  clients on the server's own SignalR connection

### Metadata sources a plugin reads

The core serves AniDB itself. Any other source, TMDb and AniList included,
comes from a plugin's metadata provider, is kept in the core's metadata
stores, and is read through the general metadata services below. TMDb comes
from the bundled TMDb plugin.

- [AniDB](Metadata/Anidb/Services/README.md), metadata, MyList and AVDump

### Server services a plugin calls

- [Video services](Video/Services/README.md), to find files, hash them, match
  them to episodes and move them
- [Executable actions](Actions/Services/README.md), to list and invoke named
  units of work, your own or another plugin's
- [Scheduled actions](ScheduledActions/Services/README.md), to list them, set
  their triggers, and run or cancel them
- [Metadata](Metadata/README.md), the overview of the metadata contract:
  sources and identifiers, entries, titles, orderings, stores, and what one
  entity says about another through relations, suggestions and cross-references
- [Metadata services](Metadata/Services/README.md), to look up series, episodes
  and groups, manage grouping, order them, and work with images
- [Metadata stores](Metadata/Providers/README.md#storing-your-data), in
  `Metadata/Storage/`, to keep a source's entries and its links to AniDB
- [User and user data](User/Services/README.md), for watch state, ratings and
  user tags
- [Configuration](Config/Services/README.md), declared as a plain C# class with
  no `appsettings.json`
- [Logging](Logging/Services/README.md), for writing log lines and reading them
  back
- [System lifecycle](Core/Services/README.md), for what state the server is in
- [Connectivity](Connectivity/Services/README.md), for whether the machine can
  reach the internet
- [Filtering](Filtering/Services/README.md), for evaluating filters and presets
  over a collection
- [Web themes](Web/Services/README.md)
- [The plugin namespace itself](Plugin/README.md), covering the plugin and
  package managers

---

## The shape of a plugin

A plugin is one assembly that references `Shoko.Abstractions` and contains
exactly one public class implementing `IPlugin`. That class carries the
plugin's identity (a stable `Guid`, a name, a description) and nothing else
you need at runtime.

Reference it as a package, and keep its assemblies out of your output:

```xml
<PackageReference Include="Shoko.Abstractions" Version="..." ExcludeAssets="runtime" />
```

`ExcludeAssets="runtime"` is load bearing: a plugin that ships its own copy
resolves `IPlugin` against that copy, the type check never matches, and the
plugin is skipped without an error. A `ProjectReference` to the server's
projects needs `Private="false"` as well, since `ExcludeAssets` alone does not
stop it copying to the output.

```csharp
public class Plugin : IPlugin, IPluginServiceRegistration, IPluginApplicationRegistration
{
    public Guid ID { get; private init; } = new("2f4b7a3e-9c1d-4f6a-8b2e-5d3c7a1f9e0b");

    public string Name { get; private set; } = "My Plugin";

    public string? Description { get; private set; } = "What it does, in a sentence.";

    public static void RegisterServices(IServiceCollection serviceCollection, IApplicationPaths applicationPaths) { }

    public static void RegisterServices(IApplicationBuilder application, IApplicationPaths applicationPaths) { }
}
```

Both registration interfaces are optional and declare `RegisterServices` as
`abstract static`, so you implement them as `public static` methods.

**Everything the server discovers has to be `public`.** Type scanning runs off
`Assembly.GetExportedTypes()`, so an `internal` provider, resolver,
configuration class or `IPlugin` implementation is invisible, with no warning.
For an internal configuration class, `ConfigurationProvider<T>` still resolves,
and its first `Load()` throws a `NullReferenceException`.

Only the first of each is used. A second `IPlugin` or
`IPluginServiceRegistration` in the same assembly is ignored with a warning; a
second `IPluginApplicationRegistration` is ignored silently.

---

## `IPlugin` needs a public parameterless constructor

**Put no constructor dependencies on the class that implements `IPlugin`.**
Not an `ILogger`, not a service, not a `ConfigurationProvider<T>`. A plugin
whose only constructor takes arguments is never loaded at all.

The server builds your plugin class twice:

1. **Discovery**, during the plugin scan, before any container exists. Each
   candidate DLL is loaded into a throwaway `AssemblyLoadContext` and the plugin
   class is built with `Activator.CreateInstance`, only to read its `ID`,
   `Name`, `Description` and embedded image names. That needs a public
   parameterless constructor.
2. **Initialization**, after the host is built, with
   `ActivatorUtilities.CreateInstance`, which *would* inject.

Step 1 gates step 2, so a constructor with parameters throws
`MissingMethodException` in discovery, which logs

```
Failed to check assembly {Name} for valid IPlugin and IPluginServiceRegistration implementations; {DllPath}
```

and the plugin does not appear in the plugin list at all, not even as one that
failed to load. Check this first when a plugin does not show up.

---

## Where dependencies go instead

### `IPluginServiceRegistration.RegisterServices(IServiceCollection, IApplicationPaths)`

Called while the container is still being *assembled*, in load order: a plugin
is called after the plugins it depends on, and the configured plugin priority
breaks the remaining ties.

Register everything your plugin needs here: your own services, HTTP clients,
rate limiters, caches, hosted services. Also touch the static class holding the
`MetadataSource` and `MetadataEntityType` values your plugin owns, so they are
registered before anything parses one; see
[`Metadata/Providers/README.md`](Metadata/Providers/README.md#registering-the-sources-you-own).

```csharp
public static void RegisterServices(IServiceCollection serviceCollection, IApplicationPaths applicationPaths)
{
    serviceCollection.AddSingleton<MyRateLimiter>();
    serviceCollection.AddHostedService<MyBackgroundWork>();
    serviceCollection.AddHttpClient<MyApiClient>(client =>
    {
        client.BaseAddress = new Uri("https://example.test/");
    });
}
```

Nothing can be resolved yet. When a registration needs to read something at
construction time, register a factory (`AddSingleton<T>(sp => …)`); it runs
once the container is live.

`IApplicationPaths` gives you the directories the server uses; never compute
them yourself. Once the container is live, inject `PluginPaths<MyPlugin>` for
your own configuration, database and cache folders. A database of your own is
registered here with `AddPluginDbContext`; see
[the plugin namespace](Plugin/README.md#a-database-of-your-own).

### `IPlugin.Setup(IServiceProvider)` and `IPlugin.Ready()`, for the plugin class itself

This is where the class implementing `IPlugin` gets its services. Both have
empty defaults.

```csharp
public class Plugin : IPlugin
{
    private ConfigurationProvider<MyConfiguration>? _configurationProvider;

    public void Setup(IServiceProvider serviceProvider)
        => _configurationProvider = serviceProvider.GetRequiredService<ConfigurationProvider<MyConfiguration>>();

    public IReadOnlyList<PluginFeature> GetFeatures()
        => _configurationProvider?.Load() is { IsComplete: true } ? [new() { Name = "my-feature" }] : [];
}
```

- **`Setup`** runs once, in load order, after every plugin has been
  initialized and before any database is opened. Take your services here and
  leave the work for later. It is the last chance to register a
  `MetadataSource` or `MetadataEntityType`: registration closes once every
  `Setup` has run, whether or not one threw.
- **`Ready`** runs once after *every* plugin's `Setup`, before the web host
  starts. Contributions to another plugin go in `Setup`; anything that has to
  see all of them, such as freezing a registry, goes in `Ready`.

A throw from either stops the server finishing its start-up; see
[The order a plugin is started in](#the-order-a-plugin-is-started-in).

### `IPluginApplicationRegistration.RegisterServices(IApplicationBuilder, IApplicationPaths)`

Called from `UseAPI()` while the HTTP pipeline is built: after
`UseAuthentication`, `UseAuthorization` and the SignalR hub endpoints, and
before `UseCors` and `UseMvc`. Register your own middleware here, and map your
own SignalR hubs; see
[Mapping a SignalR hub](Web/Services/README.md#mapping-a-signalr-hub). Endpoints
mapped here can be listed in your plugin's Swagger document; see
[Listing mapped endpoints in Swagger](Web/Services/README.md#listing-mapped-endpoints-in-swagger).

It hands you the built container as `application.ApplicationServices`, before
the hosted services boot, which makes it a place to register recurring queue
jobs (`RecurringJobRegistry`, from `Shoko.QueueProcessor`). Such a job runs on
a fixed interval the admin cannot change; work the admin should be able to
schedule is better a
[scheduled action](ScheduledActions/Services/README.md#triggers) with default
triggers.

```csharp
public static void RegisterServices(IApplicationBuilder application, IApplicationPaths applicationPaths)
{
    var services = application.ApplicationServices;
    var configuration = services.GetRequiredService<ConfigurationProvider<MyConfiguration>>().Load();
    var registry = services.GetRequiredService<RecurringJobRegistry>();
    registry.Register<MySweepJob>(interval: configuration.SweepInterval, runImmediately: false);
}
```

By the time it runs, your `IPlugin` instance and every discovered contract
implementation exist.

### `IHostedService` / `BackgroundService`, for DI plus lifecycle

A timer, a warm-up, an event subscription to undo, or anything that must stop
cleanly on shutdown belongs in a hosted service registered in
`RegisterServices`, not on the `IPlugin` class. It gets full constructor
injection and `StartAsync`/`StopAsync` around the host's lifetime.

Pick a hosted service when the work owns in-memory state or must run on the
wall clock (core's `EpisodeAiringNotificationService` is an example). Pick a
queue job when the work is a discrete unit that should queue, retry,
deduplicate and show in the queue UI.

---

## The order a plugin is started in

| Step | What runs | What is safe here |
|---|---|---|
| 1 | Discovery builds your `IPlugin` class with its parameterless constructor | Nothing but identity: there is no container yet |
| 2 | `IPluginServiceRegistration.RegisterServices(IServiceCollection, …)` | Registering services, and your own metadata sources and kinds; nothing can be resolved |
| 3 | Initialization builds your `IPlugin` class a second time, then every contract implementation you export (providers, resolvers, rules, transforms, observers) | Constructor injection works, but the database is not open: cached repositories are still empty, and some services refuse calls this early (the hashing service throws "Providers have not been added yet") |
| 4 | `IPlugin.Setup`, for every plugin, then `IPlugin.Ready`, for every plugin | Resolving services; still no server database, and a plugin's own database throws. Registration of metadata sources and kinds closes after `Setup` |
| 5 | The web host starts: the request pipeline is built, which calls `IPluginApplicationRegistration.RegisterServices(IApplicationBuilder, …)`, and every hosted service's `StartAsync` runs | Middleware and recurring jobs; still no database |
| 6 | The database is opened and brought up to date, then the pending migrations of every plugin's own database run, and the server raises `AboutToStart` and then `Started` (see [`Core/Services/README.md`](Core/Services/README.md)) | Everything, a plugin's own database included |

In setup mode, step 6 waits until the first-time setup is completed.
Registration still closes in step 4, so a source is never registered later,
whichever mode the server starts in.

**If `Setup` or `Ready` throws,** every other plugin still gets its call, then
the server records the failure and stops before step 6. A failed plugin
database migration stops it the same way, in step 6 before `AboutToStart`. The
web host still starts, so the server stays reachable and the Web UI names what
failed; step 5 therefore runs for *every* active plugin, and a hosted service
or middleware must not assume `Setup` succeeded.

---

## Contracts the server discovers for you

Eleven contracts are found by reflection rather than through DI. For each,
`PluginManager.GetExports<T>()` takes every exported type implementing `T`,
calls `ActivatorUtilities.GetServiceOrCreateInstance` on the **concrete** type,
and hands the result to the owning service, which holds it for the life of the
process. Implementing the contract is the whole of it.

| Contract | Owning service | Folder |
|---|---|---|
| [`IReleaseInfoProvider`](Video/Release/IReleaseInfoProvider.cs) | `IVideoReleaseService` | [`Video/Release/`](Video/Release/README.md) |
| [`IHashProvider`](Video/Hashing/IHashProvider.cs) | `IVideoHashingService` | `Video/Hashing/` |
| [`IRelocationProvider`](Video/Relocation/IRelocationProvider.cs) | `IVideoRelocationService` | `Video/Relocation/` |
| [`IManagedFolderIgnoreRule`](Video/IManagedFolderIgnoreRule.cs) | `IVideoService` | `Video/` |
| [`IVideoStreamTransform`](Video/Streaming/IVideoStreamTransform.cs) | `IVideoStreamPipelineService` | [`Video/Streaming/`](Video/Streaming/README.md) |
| [`IPlaybackObserver`](Video/Streaming/IPlaybackObserver.cs) | `IVideoStreamPipelineService` | [`Video/Streaming/`](Video/Streaming/README.md) |
| [`IAiringScheduleProvider`](Metadata/Airing/IAiringScheduleProvider.cs) | `IAiringScheduleService` | [`Metadata/Airing/`](Metadata/Airing/README.md) |
| [`IMetadataResolver`](Metadata/Providers/IMetadataResolver.cs) | `IMetadataService` | [`Metadata/Providers/`](Metadata/Providers/README.md#resolving-your-own-kinds) |
| [`IResourceResolver`](Metadata/Providers/IResourceResolver.cs) | `IMetadataService` | [`Metadata/Providers/`](Metadata/Resources/README.md) |
| [`IMetadataProvider`](Metadata/Providers/IMetadataProvider.cs) | `IMetadataProviderManager` | [`Metadata/Providers/`](Metadata/Providers/README.md) |
| [`IMetadataImageContributor`](Metadata/Providers/IMetadataImageContributor.cs) | `IMetadataImageContributorManager` | [`Metadata/Providers/`](Metadata/Providers/README.md#adding-images-to-other-sources-entries) |

A type that throws while being constructed is logged and skipped, and the rest
still load.

### The three-branch registration rule

**1. Register nothing at all.** The default, and what most implementations
want.

```csharp
// Nothing. The server finds the type, constructs it with constructor
// injection, and keeps that instance.
```

Constructor parameters resolve from the container as usual, and the held
instance is long lived, so it may keep its own caches or timer.

It is one instance *per contract*, though. An unregistered class implementing
two contracts is built twice, with two sets of state, and an `IPlugin` class
that also implements a contract is held as a separate object that never had
`Setup` called. Keep contract implementations on their own classes, or register
the concrete type as a singleton (branch 2).

**2. Register the concrete type as a singleton,** only when your own code
resolves it: a queue job, a controller, a hosted service of yours that calls
into it.

```csharp
services.AddSingleton<MyProvider>();
```

The singleton lifetime is what makes your code and the server share *one*
object; a transient registration is as bad as none.

**3. Never register it under the interface.**

```csharp
services.AddSingleton<IReleaseInfoProvider, MyProvider>(); // don't
```

- **It pollutes the container for everyone.** Resolving a single `T` returns
  the *last* registration, so anything calling
  `GetRequiredService<IReleaseInfoProvider>()` gets whichever plugin loaded last.
- **The server never reads it,** because `GetExports<T>` asks for the
  *concrete* type, **so a second instance is built.** Singleton state (rate
  limiters, caches, warn-once flags) splits between the two, and a service that
  checks a call against the instance it holds rejects the one from DI.

---

## Plugin identity metadata

A plugin built without embedded identity metadata logs, on every startup:

```
Plugin does not have embedded identity metadata. ({DllName}, {Version})
Install Shoko.BuildTools (`dotnet tool install --global Shoko.BuildTools`) and
rebuild with `shoko-build`, or add the `Shoko.BuildTools.Targets` NuGet package
to your project for automatic metadata injection.
```

The targets package injects the metadata at build time:

```xml
<PackageReference Include="Shoko.BuildTools.Targets" Version="..." PrivateAssets="All" />
```

The metadata is a set of `AssemblyMetadataAttribute` entries (`PackageID`,
`PackageName`, `PackageOverview`, `PackageDependencies`, `RepositoryUrl`,
`PackageProjectUrl`, `PackageTags`, `RuntimeIdentifier`, `ReleaseChannel`,
`ReleaseTag`, `ReleaseDate`, `SourceRevision`) read during discovery.

Without it the plugin still loads, using the `ID`, `Name` and `Description` of
its `IPlugin`, but it cannot declare dependencies on other plugins, which are
read from `PackageDependencies` only.

**`PackageID`, once present, wins and must agree with `IPlugin.ID`.** A plugin
whose embedded `PackageID` differs from the `ID` its `IPlugin` returns is
skipped with a warning. So is any plugin claiming the core plugin's ID.

---

## Configuration

Any exported class implementing `IConfiguration` is discovered and handed to
`IConfigurationService`; read and write it through an injected
`ConfigurationProvider<TConfig>`, with nothing to register. The settings page
is generated from the class and its attributes. See
[Configuration](Config/Services/README.md) for the attributes, the marker
interfaces, secrets and the `[Required]` trap.

---

## Other things wired up for you

- **Queue jobs.** `IQueueJob`, `IQueueScheduler`, `IJobChainBuilder`,
  `RecurringJobRegistry` and the concurrency and acquisition attributes (such as
  `[DatabaseRequired]`) live in the separate **`Shoko.QueueProcessor`** package,
  so a plugin with jobs references it as well as `Shoko.Abstractions`, with
  `ExcludeAssets="runtime;native"` (it brings EF Core and SQLite, whose native
  libraries `runtime` alone does not exclude). Every plugin assembly's jobs are
  registered for you; write the `IQueueJob` and enqueue it. Both packages move
  in lock step with the server: a stable server pairs with the same stable
  version of each, a daily build with the latest prerelease.
- **Controllers.** Each enabled plugin assembly is an MVC application part, so
  its controllers are routed and gated like any other. Until the server has
  finished starting (and in setup mode, and after a failed start) every
  endpoint answers `503` unless marked `[InitFriendly]`; while the database is
  blocked, every action answers `400` unless marked `[DatabaseBlockedExempt]`
  (both in `Shoko.Abstractions.Web.Attributes`). Plugin middleware sits behind
  the same `503` gate. Put everything a plugin serves under one namespace of its
  own; see [Routes a plugin serves](Web/Services/README.md#routes-a-plugin-serves).
- **Executable and scheduled actions.** Exported types implementing
  `IExecutableAction` or `IScheduledAction` are registered as transient and
  resolved fresh per run. See the [actions](Actions/Services/README.md) and
  [scheduled actions](ScheduledActions/Services/README.md) pages for what fails
  startup.
- **Pages and features.** `IPlugin.GetPages()` advertises `PluginPage` links
  and `GetFeatures()` advertises `PluginFeature` entries; see
  [the plugin namespace](Plugin/README.md#pages).

---

## Where plugins live, and what stops one loading

The server scans two directories, in this order:

1. `{ApplicationPath}/plugins`, next to the server executable, for plugins that
   ship with the install.
2. `{DataPath}/plugins` (`IApplicationPaths.PluginsPath`), the user's own
   plugin directory.

In both, a plugin is either a loose `*.dll` or a subdirectory containing one. A
subdirectory carrying a `*.deps.json` resolves its own dependencies. Marker
files sit beside the DLL or in its directory: `.remove` deletes it on the next
startup, `.pinned` keeps that copy preferred when several versions of the same
plugin ID are installed. Which plugins are enabled, and in what order they
load, lives in the server settings under `Plugins.EnabledPlugins` and
`Plugins.Priority`.

A DLL is skipped, or loaded as an inert entry showing why, when:

- it does not reference `Shoko.Abstractions` at all (skipped silently, this is
  how the server ignores ordinary dependency DLLs);
- its assembly name does not match its file name;
- it references the retired `Shoko.Plugin.Abstractions` namespace;
- it references a **newer** `Shoko.Abstractions` than the server implements;
- its `RuntimeIdentifier` is neither `any` nor the server's own;
- its embedded plugin dependencies are unparseable, or a required dependency it
  declares is missing, disabled, out of version range, or itself refused;
- it takes part in, or sits behind, a dependency cycle;
- its dependencies cannot be resolved, so `GetExportedTypes()` throws.
