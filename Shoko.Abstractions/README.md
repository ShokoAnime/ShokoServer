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
to say so.

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
   `ID`, `Name`, `Description` and `EmbeddedThumbnailResourceName` off it. That
   overload requires a public parameterless constructor. There is no container
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

### `IPluginApplicationRegistration.RegisterServices(IApplicationBuilder, IApplicationPaths)`

Called from `UseAPI()` while the HTTP pipeline is built: after
`UseAuthentication`, `UseAuthorization` and the SignalR hub endpoints, and
before `UseCors` and `UseMvc`. Register your own middleware here.

It is also the first point at which a plugin holds a *built* container, as
`application.ApplicationServices`, and it runs before the hosted services boot.
That combination is what makes it the place to register recurring queue jobs,
which is what the shipping airing plugins do:

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
render that page under the provider.

---

## Other things wired up for you

- **Queue jobs.** Every plugin assembly is scanned with
  `AddQueueJobsFromAssembly` during `RegisterPlugins`. You do not call it
  yourself; write the `IQueueJob` and enqueue it. See the scheduling section of
  the repository's `CLAUDE.md` for `IQueueScheduler`, `IJobChainBuilder`,
  `RecurringJobRegistry` and the concurrency and acquisition attributes.
- **Controllers.** Each enabled plugin assembly is added as an MVC application
  part, so a controller in your plugin is routed like any other.
- **Executable actions.** Exported types implementing `IExecutableAction` are
  registered as transient, and resolved fresh from DI per execution.
- **Pages and features.** `IPlugin.GetPages()` advertises `PluginPage` entries
  (a name and a URL, embeddable by default) to clients, and `GetFeatures()`
  advertises `PluginFeature` entries. `GetFeatures()` is called every time a
  client asks, so a plugin can leave out a feature its current configuration
  cannot deliver.

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
