# The `Shoko.Abstractions.Plugin` Namespace

This folder is the plugin system talking about *itself*: the marker interface
every plugin implements, the two static registration hooks, the services that
describe and manage installed plugins, and the models behind plugin packages and
repositories.

It is the reference page for those types. The narrative version, covering how a
plugin is found and constructed, why `IPlugin` needs a parameterless
constructor, what `RegisterServices` is for, hosted services, and the rule for
registering the contracts the server discovers, lives one level up in the
[plugin overview](../README.md). This page links there rather than repeating it.

| Type | Your plugin |
|---|---|
| `IPlugin` | **Implements.** Exactly one class per plugin assembly. |
| `IPluginServiceRegistration` | **Implements**, optionally, on that same class. Static abstract. |
| `IPluginApplicationRegistration` | **Implements**, optionally, on that same class. Static abstract. |
| `IApplicationPaths` | **Consumes.** Injected, or handed to the two registration hooks. |
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

    // Absolute resource name, assembly name included. Optional.
    public string? EmbeddedThumbnailResourceName => "MyPlugin.assets.Thumbnail.png";

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

`ID` and `Name` are the only required members. `Description`,
`EmbeddedThumbnailResourceName`, `GetPages()` and `GetFeatures()` all have
default implementations. The class itself needs a public parameterless
constructor, because the plugin scan instantiates it before any container
exists; anything needing injected services belongs in a service of your own
instead. The [plugin overview](../README.md) covers that in full.

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

A `PluginFeature` is how a plugin tells clients what the server can do, so a
client can turn UI on without sniffing for endpoints. `GetFeatures()` is called
every time a client asks, so a feature whose configuration is incomplete should
simply not be returned that time.

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
or an `IApplicationBuilder` alongside `IApplicationPaths`. They are static
because the core calls them before any plugin instance exists. What belongs in
each, and how to register your own services, is covered in the
[plugin overview](../README.md).

---

## `IApplicationPaths`

Every directory the server uses, resolved for the current install. Inject it, or
take the one handed to `RegisterServices`. Build paths from these rather than
from `AppContext.BaseDirectory` or a hard-coded folder, because a user can move
the data directory and several of these follow settings.

| Property | Directory |
|---|---|
| `ApplicationPath` | The executable's parent directory. |
| `DataPath` | The data directory, the root of everything writable. |
| `ConfigurationsPath` | Configuration files. |
| `PluginsPath` | Installed plugins. |
| `ImagesPath` | The image cache. |
| `StreamCachePath` | Transcode and rendition cache. |
| `WebPath` | Web UI resources. |
| `ThemesPath` | Web UI themes. |
| `LogsPath` | Log files. |

`PackageThumbnailInfo.GetStream(IApplicationPaths)` is the one place these are
substituted into a stored string: a thumbnail path may contain `%PluginsPath%`
or `%ApplicationPaths%`, which that method expands before opening the file.

---

## `IPluginManager`

The registry of what is installed, loaded and exported. A plugin normally wants
it for two things: finding out about itself, and finding out whether some other
plugin is around.

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

`LocalPluginInfo` is the answer to all of them. The state flags are worth
reading carefully, because they mean four different things:

| Flag | Meaning |
|---|---|
| `IsInstalled` | Still on disk, i.e. not uninstalled during this session. |
| `IsEnabled` | Enabled for this session **or the next one**. |
| `IsActive` | Actually loaded right now. Implies `Plugin` and `PluginType` are non-null. |
| `IsPinned` | The version is pinned against automatic upgrades. |
| `CanLoad` | Loadable by this runtime: assemblies present, ABI compatible, dependencies satisfied. |
| `RestartPending` | `IsEnabled != IsActive`, so a user action is waiting on a restart. |
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

This is the machinery behind every discovered contract in the abstractions:
release providers, hash providers, airing schedule providers, relocation
providers, resource resolvers, playback observers and the rest. Which is also
the catch:

> **`GetExports<T>()` hands you a *new* instance for any type that is not
> registered in DI.** It is not a way to reach the instance the core is holding.
> If your own code and the core have to share one object, register the concrete
> type as a singleton, per
> [Contracts the server discovers for you](../README.md#contracts-the-server-discovers-for-you)
> in the plugin overview.

`GetService(Type)`, `GetService<T>()`, `GetRequiredService(Type)` and
`GetRequiredService<T>()` are a service-locator escape hatch over the root
container, for code that genuinely cannot take constructor injection. Prefer
injection everywhere else.

### Compatibility

`AbstractionVersion` is the ABI the running server exposes, `RuntimeIdentifier`
is its platform, and `IPluginManager.AnyRuntimeIdentifier` (`"any"`) is the
wildcard a platform-neutral package declares.
`IsAbiAndRuntimeCompatible(version, runtimeIdentifier)` answers whether a given
pair could be loaded here, which is what the package layer uses to filter
release archives.

### Lifecycle management, and what not to call

`EnablePlugin`, `DisablePlugin`, `PinPlugin`, `UnpinPlugin` and
`UninstallPlugin(pluginInfo, purgeConfiguration)` each return the updated
`LocalPluginInfo`. These are for a management UI, not for a plugin managing
itself. Three things about them:

- **They take effect from the next session.** Enabling does not load anything
  now, disabling does not unload anything now, and an uninstalled plugin stays
  active until restart. Watch `RestartPending`.
- **Enabling pins**, if the version is not the latest; disabling unpins.
- The built-in core plugin cannot be toggled or uninstalled.

`UninstallPlugin` surfaces an `IOException` when the plugin's files cannot be
removed from disk. `LoadFromPath(string)` loads plugin info from a path inside
the user plugin directory, and fails for anything outside it.

`ScanForPlugins()`, `RegisterPlugins(IServiceCollection)` and `InitPlugins()`
are the host's own startup sequence. A plugin never calls them; `InitPlugins()`
throws `InvalidOperationException` if it has already run.

`PluginInstalled` and `PluginUninstalled` are raised with a
`PluginInstallationEventArgs` carrying the `Plugin` and `OccurredAt`.

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
when disabling is safe. `GetUninstallCascade(plugin)` returns the full
transitive set of dependents, excluding the plugin itself.
`IsVersionSatisfied(versionRange, candidate)` is the range check on its own, so
your plugin can apply the same rules to something of its own.

Resolution only ever considers the currently installed and enabled plugins, so
the answers change as the user installs and removes things.

---

For writing the plugin itself, start at the [plugin overview](../README.md).
