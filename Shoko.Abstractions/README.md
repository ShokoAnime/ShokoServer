# Writing a Shoko Plugin

This is the contract between Shoko Server and the plugins that extend it.
Everything in this package is an interface, a model or an enum: the server
implements them, your plugin consumes them, and neither side needs a reference
to the other's concrete types.

## Where to go next

This file covers what every plugin has to get right, whatever it does. The
pages below cover what is special about one contract or service, and link back
here rather than repeating any of it.

### Contracts a plugin implements

Relocation is the oldest of these. The 4.x plugin system was built around
renaming and moving files, so most plugins predating 5.x are relocation
providers.

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
- [Image cross-reference resolvers](Metadata/Image/CrossReferences/README.md),
  attaching images to entities the server has no case for
- [Resource resolvers](Metadata/Resources/README.md), contributing external
  links to an entity
- [Supplementary metadata providers](Metadata/Services/README.md), reacting when
  a series is added or refreshed
- [Executable actions](Actions/Services/README.md), exposing a named unit of
  work a user or client can invoke

### Metadata sources a plugin reads

- [AniDB](Metadata/Anidb/Services/README.md), metadata, MyList and AVDump
- [AniList](Metadata/Anilist/Services/README.md), search, metadata and linking
- [TMDB](Metadata/Tmdb/Services/README.md), search, metadata and linking

### Server services a plugin calls

- [Video services](Video/Services/README.md), to find files, hash them, match
  them to episodes and move them
- [Executable actions](Actions/Services/README.md), to list and invoke named
  units of work, your own or another plugin's
- [Relations and suggestions](Metadata/README.md), reading what one entity says
  about another, and what a provider's users suggest from it
- [Metadata services](Metadata/Services/README.md), to look up series, episodes
  and groups, manage grouping, and work with images
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

`ExcludeAssets="runtime"` is load bearing. The server already has these
assemblies, and a plugin that ships its own copy resolves `IPlugin` against
that copy instead, so the type check never matches and the plugin is skipped
without an error. That failure has happened, and it looks like the plugin is
simply absent rather than broken. If you reference the server's projects
directly during development, a `ProjectReference` needs `Private="false"` as
well, since `ExcludeAssets="runtime"` alone does not stop a project reference
copying to the output directory.

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

Both registration interfaces are optional, and both declare their
`RegisterServices` as `abstract static`, so you implement them as `public
static` methods on the plugin class. The server invokes them by reflection;
it never needs an instance to do it.

**Everything the server discovers has to be `public`.** Type scanning runs off
`Assembly.GetExportedTypes()`, so an `internal` provider, resolver,
configuration class or `IPlugin` implementation is invisible, with no warning
to say so. For a configuration class that is worse than invisible:
`ConfigurationProvider<T>` still resolves for it, and the first `Load()` throws
a `NullReferenceException` because the service has no record of the type.

Only the first of each is used. A second `IPlugin` or
`IPluginServiceRegistration` in the same assembly is ignored with a warning; a
second `IPluginApplicationRegistration` is ignored silently.

---

## `IPlugin` needs a public parameterless constructor

**Put no constructor dependencies on the class that implements `IPlugin`.**
Not an `ILogger`, not a service, not a `ConfigurationProvider<T>`. A plugin
whose only constructor takes arguments is never loaded at all.

The server builds your plugin class twice, and only the second of the two
injects:

1. **Discovery**, in `PluginManager.LoadInternalPluginInfo`. Each candidate DLL
   is loaded into a throwaway collectible `AssemblyLoadContext` and the plugin
   type is built with `Activator.CreateInstance(pluginImpl[0])`, purely to read
   `ID`, `Name`, `Description`, `EmbeddedThumbnailResourceName` and
   `EmbeddedIconResourceName` off it. That overload requires a public
   parameterless constructor. There is no container
   to inject from at this point: discovery happens during `ScanForPlugins()`,
   before the web host, and therefore before the service collection, exists.
2. **Initialization**, in `PluginManager.InitPlugins`, which runs after the host
   is built. Here the instance that actually lives for the process is built with
   `ActivatorUtilities.CreateInstance(ISystemService.StaticServices, pluginType)`,
   which *would* inject constructor dependencies.

Because step 1 runs first and gates step 2, the injection in step 2 is
unreachable for a plugin that needs it. The `Activator.CreateInstance` call
throws `MissingMethodException`, the surrounding handler logs

```
Failed to check assembly {Name} for valid IPlugin and IPluginServiceRegistration implementations; {DllPath}
```

and returns nothing, so the plugin does not appear in the plugin list at all,
not even as an entry that failed to load. The symptom is a plugin that is
silently absent, which is why this is the first thing to check when one does
not show up.

---

## Where dependencies go instead

### `IPluginServiceRegistration.RegisterServices(IServiceCollection, IApplicationPaths)`

Called from `PluginManager.RegisterPlugins` while the container is still being
*assembled*, from the server's `Startup.ConfigureServices`. Plugins are called
in load order, which is a topological sort: a plugin that declares a dependency
on another is called after it, and the configured plugin priority breaks the
remaining ties.

This is where everything your plugin needs goes: your own services, HTTP
clients, rate limiters, caches, hosted services.

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

The container does not exist yet, so there is nothing to resolve here. Register
factories (`AddSingleton<T>(sp => …)`, `AddHttpClient<T>((sp, client) => …)`)
when a registration needs to read something at construction time; the factory
runs later, when the container is live.

`IApplicationPaths` gives you the directories the server uses: `DataPath`,
`PluginsPath`, `ConfigurationsPath`, `ImagesPath`, `LogsPath`,
`StreamCachePath`, `WebPath`, `ThemesPath`. It is the supported way to find
them. Do not compute them yourself.

### `IPlugin.Setup(IServiceProvider)` and `IPlugin.Ready()`, for the plugin class itself

This is where the class that implements `IPlugin` gets its services, since it
cannot take them in its constructor. Both have empty defaults, so override only
what you need.

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

- **`Setup`** runs once, after every plugin has been initialized and before the
  database is opened. Take the services you need here and leave the work that
  uses them for later.
- **`Ready`** runs once after *every* plugin's `Setup`, and before the web host
  starts. `Setup` runs in load order, and a dependency always loads before the
  plugins that depend on it, so a plugin that collects what other plugins
  contribute has been set up before any of them. Contributions go in `Setup`;
  anything that has to see all of them, such as freezing a registry, goes in
  `Ready`.

A throw from either stops the server finishing its start-up. See
[The order a plugin is started in](#the-order-a-plugin-is-started-in) for what
that leaves running.

Both only bind when your plugin is compiled against a `Shoko.Abstractions`
version that declares them. Compiled against an older one, a `Setup` method is an
ordinary method the server never calls.

### `IPluginApplicationRegistration.RegisterServices(IApplicationBuilder, IApplicationPaths)`

Called from `UseAPI()` while the HTTP pipeline is built: after
`UseAuthentication`, `UseAuthorization` and the SignalR hub endpoints, and
before `UseCors` and `UseMvc`. Register your own middleware here, and map your
own SignalR hubs; see
[Mapping a SignalR hub](Web/Services/README.md#mapping-a-signalr-hub).

It hands you the built container as `application.ApplicationServices`, and it
runs before the hosted services boot. That combination makes it a good place to
register recurring queue jobs, which is what the shipping airing plugins do
(`RecurringJobRegistry` comes from the `Shoko.QueueProcessor` package; see
[Other things wired up for you](#other-things-wired-up-for-you)):

```csharp
public static void RegisterServices(IApplicationBuilder application, IApplicationPaths applicationPaths)
{
    var services = application.ApplicationServices;
    var configuration = services.GetRequiredService<ConfigurationProvider<MyConfiguration>>().Load();
    var registry = services.GetRequiredService<RecurringJobRegistry>();
    registry.Register<MySweepJob>(interval: configuration.SweepInterval, runImmediately: false);
}
```

Only plugins that reached initialization get this call, so by the time it runs
your `IPlugin` instance and every discovered contract implementation already
exist.

### `IHostedService` / `BackgroundService`, for DI plus lifecycle

If your plugin needs a timer, a warm-up, an event subscription it must
unsubscribe from, or anything that has to stop cleanly on shutdown, the answer
is a hosted service registered in `RegisterServices`, not work on the `IPlugin`
class.

A hosted service gets full constructor injection, `StartAsync`/`StopAsync`
around the host's own lifetime, and can take `IHostApplicationLifetime` to hook
`ApplicationStarted`, `ApplicationStopping` and `ApplicationStopped`.

The worked example in core is
`Shoko.Server/Services/Airing/EpisodeAiringNotificationService.cs`, a
`BackgroundService` registered with `services.AddHostedService<…>()` in
`SystemService`. It takes an `ILogger<T>`, `IAiringScheduleService` and
`ISystemService` through its constructor, subscribes to service events there,
unsubscribes in `Dispose`, and runs its loop in `ExecuteAsync`. Its own doc
comment explains why it is a hosted service rather than a recurring queue job:
it owns in-memory state (a job is constructed per execution and has nowhere to
keep any), and its dispatch has to land on a wall-clock minute rather than
whenever the worker pool and the acquisition filters let a job through.

Pick the other way around when the work is a discrete unit that should queue,
retry, deduplicate and show up in the queue UI. That is a queue job, registered
as a recurring job from the `IApplicationBuilder` overload above.

---

## The order a plugin is started in

| Step | What runs | What is safe here |
|---|---|---|
| 1 | Discovery builds your `IPlugin` class with its parameterless constructor | Nothing but identity: there is no container yet |
| 2 | `IPluginServiceRegistration.RegisterServices(IServiceCollection, …)` | Registering services only; nothing can be resolved |
| 3 | Initialization builds your `IPlugin` class a second time, then every contract implementation you export (providers, resolvers, rules, transforms, observers) | Constructor injection works, but the database is not open: cached repositories are still empty, and some services refuse calls this early (the hashing service throws "Providers have not been added yet") |
| 4 | `IPlugin.Setup`, for every plugin, then `IPlugin.Ready`, for every plugin | Resolving services; still no database |
| 5 | The web host starts: the request pipeline is built, which calls `IPluginApplicationRegistration.RegisterServices(IApplicationBuilder, …)`, and every hosted service's `StartAsync` runs | Middleware and recurring jobs; still no database |
| 6 | The database is opened, and the server raises `AboutToStart` and then `Started` (see [`Core/Services/README.md`](Core/Services/README.md)) | Everything |

In setup mode, step 6 waits until the first-time setup is completed.

**If `Setup` or `Ready` throws,** every other plugin still gets its call, and
then the server records the failure and stops before step 6. The web host still
starts, so the server stays reachable and the Web UI names the plugins that
failed. That means step 5 still runs, for *every* active plugin, including one
whose `Setup` threw or never got as far as `Ready`. A hosted service or
middleware must not assume `Setup` succeeded.

---

## Contracts the server discovers for you

Eleven contracts are found by reflection rather than through DI. For each one,
`PluginManager.GetExports<T>()` takes every exported type that implements `T`,
calls `ActivatorUtilities.GetServiceOrCreateInstance` on the **concrete** type,
and hands the result to the owning service, which holds it for the life of the
process.

Implementing the contract is the whole of it; there is nothing to call, and no
way to hand a service an implementation yourself.

| Contract | Owning service | Folder |
|---|---|---|
| [`IReleaseInfoProvider`](Video/Release/IReleaseInfoProvider.cs) | `IVideoReleaseService` | [`Video/Release/`](Video/Release/README.md) |
| [`IHashProvider`](Video/Hashing/IHashProvider.cs) | `IVideoHashingService` | `Video/Hashing/` |
| [`IRelocationProvider`](Video/Relocation/IRelocationProvider.cs) | `IVideoRelocationService` | `Video/Relocation/` |
| [`IManagedFolderIgnoreRule`](Video/IManagedFolderIgnoreRule.cs) | `IVideoService` | `Video/` |
| [`IVideoStreamTransform`](Video/Streaming/IVideoStreamTransform.cs) | `IVideoStreamPipelineService` | [`Video/Streaming/`](Video/Streaming/README.md) |
| [`IPlaybackObserver`](Video/Streaming/IPlaybackObserver.cs) | `IVideoStreamPipelineService` | [`Video/Streaming/`](Video/Streaming/README.md) |
| [`IAiringScheduleProvider`](Metadata/Airing/IAiringScheduleProvider.cs) | `IAiringScheduleService` | [`Metadata/Airing/`](Metadata/Airing/README.md) |
| [`IAiringScheduleEntityResolver`](Metadata/Airing/IAiringScheduleEntityResolver.cs) | `IAiringScheduleService` | [`Metadata/Airing/`](Metadata/Airing/README.md) |
| [`IImageCrossReferenceResolver`](Metadata/Image/CrossReferences/IImageCrossReferenceResolver.cs) | `IImageManager` | `Metadata/Image/CrossReferences/` |
| [`IResourceResolver`](Metadata/Resources/IResourceResolver.cs) | `IMetadataService` | `Metadata/Resources/` |
| [`ISupplementaryMetadataProvider`](Metadata/Services/ISupplementaryMetadataProvider.cs) | `ISupplementaryMetadataService` | `Metadata/Services/` |

A type that throws while being constructed is logged and skipped, and the rest
still load.

### The three-branch registration rule

**1. Register nothing at all.** The default, and what most implementations
want.

```csharp
// Nothing. The server finds the type, constructs it with constructor
// injection, and keeps that instance.
```

`GetServiceOrCreateInstance` constructs an unregistered concrete type,
resolving its constructor parameters from the container as usual, so an
`HttpClient`, your own rate limiter or a `ConfigurationProvider<T>` all arrive
the normal way. The held instance is long lived, which covers an implementation
with its own caches or its own internal timer.

It is one instance *per contract*, though. An unregistered class that implements
two contracts, such as a transform that is also an observer, is built twice and
holds two separate sets of state. Your `IPlugin` class is never registered in DI
either, so if it also implements a contract, the instance the server holds for
that contract is a separate object that never had `Setup` called. Keep contract
implementations on their own classes, or register the concrete type as a
singleton (branch 2 below) so every contract resolves to the same object.

**2. Register the concrete type as a singleton,** only when your own code
resolves it: a queue job, a controller, a hosted service of yours that calls
into it.

```csharp
services.AddSingleton<MyProvider>();
```

The singleton lifetime is the entire point. It is what makes your code and the
server share *one* object. A transient registration is as bad as none here,
because `GetServiceOrCreateInstance` would hand the server its own instance and
your job another.

**3. Never register it under the interface.**

```csharp
services.AddSingleton<IReleaseInfoProvider, MyProvider>(); // don't
```

- **It pollutes the container for everyone.** Resolving a single `T` when
  several registrations exist returns the *last* one registered, so the winner
  is plugin load order: arbitrary, and liable to change when a user installs or
  removes some unrelated plugin. Anything calling
  `GetRequiredService<IReleaseInfoProvider>()` quietly gets your provider
  instead of its own.
- **The server never reads it,** because `GetExports<T>` asks the container for
  the *concrete* type.
- **So a second instance is constructed.** The concrete type is still
  unregistered, `GetServiceOrCreateInstance` builds a fresh one, and now there
  are two: the one in DI that nothing calls, and the one the server holds.
  Singleton state (rate limiters, caches, HTTP clients, warn-once flags) splits
  between them, and any service that checks a call against the instance it
  was handed at startup rejects the one you resolved out of DI.

---

## Plugin identity metadata

A plugin built without embedded identity metadata logs, on every startup:

```
Plugin does not have embedded identity metadata. ({DllName}, {Version})
Install Shoko.BuildTools (`dotnet tool install --global Shoko.BuildTools`) and
rebuild with `shoko-build`, or add the `Shoko.BuildTools.Targets` NuGet package
to your project for automatic metadata injection.
```

Do one of the two things it says. The targets package injects the metadata at
build time:

```xml
<PackageReference Include="Shoko.BuildTools.Targets" Version="..." PrivateAssets="All" />
```

The metadata is a set of
`AssemblyMetadataAttribute` entries (`PackageID`, `PackageName`,
`PackageOverview`, `PackageDependencies`, `RepositoryUrl`, `PackageProjectUrl`,
`PackageTags`, `RuntimeIdentifier`, `ReleaseChannel`, `ReleaseTag`,
`ReleaseDate`, `SourceRevision`) that the server reads during discovery, before
it builds anything.

Without it the plugin still loads, falling back to the `ID`, `Name` and
`Description` its `IPlugin` returns, but it cannot declare dependencies on other
plugins, because those are read from `PackageDependencies` and nowhere else.

**`PackageID`, once present, wins and must agree with `IPlugin.ID`.** A plugin
whose embedded `PackageID` differs from the `ID` its `IPlugin` returns is
skipped with a warning. So is any plugin claiming the core plugin's ID.

---

## Configuration

Configuration classes are discovered the same way types are: any exported class
implementing `IConfiguration` is registered with `IConfigurationService`. You do
not register them yourself.

To read and write one, inject `ConfigurationProvider<TConfig>`. The server
registers it as an open-generic singleton, so it resolves for any configuration
type without a registration of your own:

```csharp
public class MyApiClient(ConfigurationProvider<MyConfiguration> configurationProvider)
{
    private MyConfiguration Configuration => configurationProvider.Load();
}
```

`ConfigurationProvider<T>` exposes `Load`, `Save`, `Validate`, a `Saved` event
for reacting to a user changing the settings, and `ConfigurationInfo`.

Annotate the configuration class itself to shape what the Web UI renders:
`[Display]`, `[DefaultValue]`, `[Range]` and the attributes in
[`Config/Attributes/`](Config/Attributes) (`[Section]`, `[Select]`,
`[TextArea]`, `[CodeEditor]`, `[Badge]`, `[Visibility]`, `[RequiresRestart]`,
`[EnvironmentVariable]`, `[StorageLocation]`, and the custom-action
attributes). Marker interfaces, most of them in
[`Config/IConfiguration.cs`](Config/IConfiguration.cs), change how a
configuration is handled:

| Interface | Effect |
|---|---|
| `INewtonsoftJsonConfiguration` | Serialize with Newtonsoft.Json instead of `System.Text.Json` |
| `IHiddenConfiguration` | Hidden from any UI |
| `IHashProviderConfiguration`, `IReleaseInfoProviderConfiguration`, `IRelocationProviderConfiguration`, `IVideoStreamTransformConfiguration`, `IAiringScheduleProviderConfiguration` | Bind the configuration to a provider, so it is rendered on that provider's page rather than on its own |
| `IConfigurationWithMigrations` | `static ApplyMigrations(string, IApplicationPaths)` rewrites the stored JSON before it is deserialized |
| `IConfigurationWithNewFactory<T>` | `static New(…)` builds the initial instance when there is no stored file |
| `IConfigurationWithCustomValidation<T>` | `static Validate(…)` runs after JSON schema validation |

A provider declares its configuration type through the generic form of its own
contract, for example `IReleaseInfoProvider<TConfiguration>` or
`IAiringScheduleProvider<TConfiguration>`, which is what tells the Web UI to
render that page under the provider. The exception is
`ISupplementaryMetadataProvider<TConfiguration>`: its configuration marker is
not an `IConfiguration`, and nothing reads it yet, so the generic form binds no
page.

---

## Other things wired up for you

- **Queue jobs.** `IQueueJob`, `IQueueScheduler`, `IJobChainBuilder`,
  `RecurringJobRegistry` and the concurrency and acquisition attributes (such as
  `[DatabaseRequired]`) live in the separate **`Shoko.QueueProcessor`** package,
  so a plugin with jobs references it as well as `Shoko.Abstractions`. Use
  `ExcludeAssets="runtime;native"` on it: the server already ships the package,
  and it brings EF Core and SQLite, whose native libraries `runtime` alone does
  not exclude. Every plugin assembly is scanned with
  `AddQueueJobsFromAssembly` during service registration; you do not call it
  yourself, only write the `IQueueJob` and enqueue it. The two packages move in
  lock step with the server: a stable server ships both as the same stable
  version (`6.0.0`), so reference that pair, and a daily build pairs with the
  latest prerelease (alpha or beta) of each.
- **Controllers.** Each enabled plugin assembly is added as an MVC application
  part, so a controller in your plugin is routed like any other, and gated like
  any other. Until the server has finished starting (and in setup mode, and
  after a failed start) every endpoint answers `503` unless it is marked
  `[InitFriendly]`; while the database is blocked, every action answers `400`
  unless it is marked `[DatabaseBlockedExempt]`. Both attributes are in
  `Shoko.Abstractions.Web.Attributes`. Plugin middleware sits behind the same
  `503` gate, and has no endpoint to carry the attribute. Put everything a
  plugin serves under one namespace of its own; the paths are in
  [Routes a plugin serves](Web/Services/README.md#routes-a-plugin-serves).
- **Executable actions.** Exported types implementing `IExecutableAction` are
  registered as transient, and resolved fresh from DI per execution. See the
  [actions README](Actions/Services/README.md) for the two rules that fail
  startup.
- **Pages and features.** `IPlugin.GetPages()` advertises `PluginPage` entries
  (a name and a URL, embeddable by default) to clients, and `GetFeatures()`
  advertises `PluginFeature` entries. `GetFeatures()` is called every time a
  client asks, so a plugin can leave out a feature its current configuration
  cannot deliver. The plugin class reaches its configuration through `Setup`.

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

Building against the `Shoko.Abstractions` version you target, and shipping
identity metadata, keeps you clear of most of these.
