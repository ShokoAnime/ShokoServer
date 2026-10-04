# System Lifecycle

This folder holds the two services that describe the server itself rather than
anything in your library: `ISystemService` (what state the server is in, and
when it changes) and `ISystemUpdateService` (what versions exist).

Neither is an extension point. Both are DI singletons, so inject them:

```csharp
public class MyServerWatcher(ISystemService systemService, ISystemUpdateService updateService)
{
    public bool ServerIsUp => systemService.IsStarted;
}
```

The class implementing `IPlugin` cannot take them in its constructor (see
[the plugin overview](../../README.md#iplugin-needs-a-public-parameterless-constructor));
it takes them in `IPlugin.Setup(IServiceProvider)`.

---

## `ISystemService`

### The startup timeline, and where a plugin fits into it

`IPlugin.Setup` and `IPlugin.Ready` run before the web host starts, and the
database, then the plugins' own databases, open only after it has (the full
order is in
[the plugin overview](../../README.md#the-order-a-plugin-is-started-in)). These
events mark when the database is safe to use:

| Event | Fired | What is ready |
|---|---|---|
| `StartupMessageChanged` | Throughout startup | Nothing in particular. It carries the user-facing progress string. |
| `SetupRequired` | When the server boots into first-run setup instead of starting | Nothing. The server is waiting for the user. |
| `AboutToStart` | After the database, the plugin databases, relocation presets, the AniDB UDP handler and the file watchers are all up, and before `Started` | Everything. This is the hook to initialise against. |
| `SetupCompleted` | After `AboutToStart`, on the first run only | Same as `AboutToStart`. |
| `Started` | Immediately after, once `StartedAt` is stamped | Same. Scheduled actions start from it, so the runs set to fire at start-up (such as a library scan) are queued after it is raised; do not expect them to be waiting yet. |

`AboutToStart` is the one to use. Its `ServerAboutToStartEventArgs` carries the
`IServiceProvider`. Subscribe from a hosted service or from `IPlugin.Setup`;
both run before the event fires.

```csharp
// Registered from your plugin's RegisterServices with
// services.AddHostedService<MyStartupWork>().
public sealed class MyStartupWork(ISystemService systemService) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        systemService.AboutToStart += OnAboutToStart;
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        systemService.AboutToStart -= OnAboutToStart;
        return Task.CompletedTask;
    }

    private void OnAboutToStart(object? sender, ServerAboutToStartEventArgs e)
    {
        // Safe here: the database is up and every core service is usable.
        var repository = e.ServiceProvider.GetRequiredService<MyRepository>();
        repository.PrimeCache();
    }
}
```

**`AboutToStart` is invoked synchronously, on the startup thread.** A slow
handler delays the server, so hand anything expensive to the queue. **A handler
that throws fails the whole startup**, surfaced through `StartupFailed` and
`StartupFailedException`; wrap the body in a `try`/`catch` unless a failure
should stop the server.

`Started`, `SetupRequired` and `SetupCompleted` are dispatched on a background
task, so a throwing handler there does not fail startup.

A failed plugin database migration is a start-up failure too, before
`AboutToStart`; see
[the plugin namespace](../../Plugin/README.md#when-they-run-and-when-one-fails).

If you would rather await than subscribe, `WaitForStartupAsync()` completes when
the server is up and throws `StartupFailedException` if it is not. `IsStarted`
and `StartedAt` answer the same question after the fact.

### Setup mode

`InSetupMode` is `true` while the server waits for first-run setup.
`CompleteSetup()` belongs to the setup UI: called from a plugin it starts the
server before the user has finished configuring it.

### Shutdown

| Member | Notes |
|---|---|
| `ShutdownOrRestartRequested` | `CancelEventArgs`. Set `Cancel = true` to refuse a shutdown, for instance while your plugin is mid-transaction. Use this sparingly: a user who asked to stop the server expects it to stop. |
| `Shutdown` | The server is going down now. There is a tight time budget before the parent process kills it, so flush and return. |
| `CanShutdown` / `CanRestart` | Whether a controlled stop or restart is possible at all. |
| `ShutdownPending` / `RestartPending` | Whether one has already been requested and is under way. |
| `RequestShutdown()` / `RequestRestart()` | Returns `false` when the request was refused. |
| `WaitForShutdownAsync()` | Awaits the stop. |

### Restart reasons

`RestartPending` says a restart was asked for. Whether a change is *waiting* on
one is answered by `RestartReasons`, oldest first, held in memory so a restart
empties it. `RestartRequired` is `true` while any stands, and
`RestartReasonsChanged` carries the full list after each change. The reasons
come from three sources, named by `RestartReason.Source`:

| `Source` | Raised when | `Key` |
|---|---|---|
| `Configuration` | Any saved configuration changed members marked `[RequiresRestart]`. One reason for all of them, cleared once every value is back; `IConfigurationService.RestartPendingFor` lists the members per configuration. The server's enabled plugins are left to `PluginState`. | `configuration` |
| `PluginState` | Any plugin was enabled, disabled, installed, uninstalled or switched to another version, so the next start would load something else. An enabled plugin that cannot load never raises it. One reason for all of them; each `LocalPluginInfo` tells its own state. | `plugins` |
| `Plugin` | A plugin raised a reason of its own. One reason per handle. | A generated ID |

Each reason also names its `PluginID`, a human-readable `Description` and
`RaisedAt`, the UTC time it was first raised, which it keeps while it stands.
The `Configuration` and `PluginState` reasons belong to the core plugin and
carry fixed descriptions; a `Plugin` reason belongs to the plugin that raised
it.

A plugin raises its own reasons, naming itself by its `IPlugin` type, and
gets back a handle, and disposing the handle clears the reason:

```csharp
IRestartRequirement? restart = null;
// When a setting changes something that only applies on the next start:
restart ??= systemService.RequireRestart<MyPlugin>("The enabled sources changed and are registered on the next start.");
// ...and when the setting goes back to what was loaded:
restart?.Dispose();
restart = null;
```

Each call raises a reason of its own, so keep the handle rather than raising
again. Disposing twice is harmless, and `IsHeld` tells whether it still stands.
A type that is not an active plugin throws `InvalidOperationException`, so call
it from `Setup` onwards. The typical use is a setting that changes what the
plugin sets up once, such as the metadata sources it registers.

`RestartReasonsChanged` is raised synchronously, on the thread that made the
change, so keep handlers short; one that throws does not stop the others.
Admins follow the same list through the `restart` feed of the SignalR aggregate
hub and `GET /api/v3/Init/RestartReasons`.

### The database gate

The database can be blocked mid-run, during a migration for instance. Three
members cover it:

- `IsDatabaseBlocked`, the current state.
- `DatabaseBlockedChanged`, carrying `IsBlocked`.
- `WaitForDatabaseUnblockedAsync()`, to await the gate opening.

A queue job marked `[DatabaseRequired]` (from `Shoko.QueueProcessor`) is
already held back until the database is up; these members are for code outside
the queue.

### Version and identity

`Version` is a `VersionInformation`: the server version, the runtime identifier,
the `AbstractionVersion` it was built against, source revision, release tag,
`ReleaseChannel` and release date. `AbstractionVersion` is the one worth reading
if your plugin needs to behave differently against different contract versions.

`MediaInfoVersion` and `RHashVersion` report the two native components, or `null`
when unavailable. `BootstrappedAt`, `Uptime`, `StartupTime` and `StartedAt` cover
the clock.

### `StaticServices`, the escape hatch

```csharp
[Obsolete("Do not use this unless DI is not an option and only use it as a LAST RESORT!")]
public static IServiceProvider StaticServices { get; set; }
```

A process-wide service provider core sets during startup, for code that
predates DI. It is `[Obsolete]` so every use shows up as a warning. Prefer, in
order:

1. **Constructor injection.** A provider, an action and a queue job are all
   constructed through the container, so they can just ask. The class
   implementing `IPlugin` is the exception: it takes its services in `Setup`.
2. **`ServerAboutToStartEventArgs.ServiceProvider`**, for a handler that needs to
   resolve something late.
3. `StaticServices`, only when neither is available.

It is **write-once per process** (a second assignment throws; never set it),
and reading it before core has set it throws. `HasStaticServices` tests that
without throwing, which matters in unit tests, where nothing sets it.

---

## `ISystemUpdateService`

This checks release manifests for the server and the WebUI, and installs WebUI
versions. It is driven by the settings UI and the `/api/v3` update endpoints;
a plugin has little reason to call it. The read side:

- `GetLatestServerVersion(channel, force)` and `GetServerHistory(channel, force)`
  return `ReleaseVersionInformation` off the server manifest.
- `GetLatestWebComponentVersion(...)` and `GetWebComponentHistory(...)` do the
  same for the WebUI, as `WebReleaseVersionInformation`.
- `LoadWebComponentVersionInformation()` and
  `LoadIncludedWebComponentVersionInformation()` read what is installed and what
  shipped in the box.

Both manifest URLs are settable (`ServerManifestUrl`, `WebComponentManifestUrl`),
and both `GetLatest*`/`Get*History` calls are cached unless you pass `force: true`.

The write side (`InstallWebComponentVersion`, `UpdateWebComponent`,
`ReactToManualWebComponentUpdate`) replaces the user's WebUI installation, which
a plugin should not do behind the user's back. `WebComponentUpdated` fires after
an install.

`ReleaseChannel.Auto` matches the running server's own channel, and is almost
always what you want; the explicit members are `Debug`, `Stable` and `Dev`.
