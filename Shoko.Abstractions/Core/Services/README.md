# System Lifecycle

This folder holds the two services that describe the server itself rather than
anything in your library: `ISystemService` (what state the server is in, and
when it changes) and `ISystemUpdateService` (what versions exist).

Neither is an extension point. Nothing here is discovered by
`PluginManager.GetExports<T>()`; there is no interface in this folder for a
plugin to implement. Both are registered as singletons in DI, so anything the
container builds gets them the ordinary way:

```csharp
public class MyServerWatcher(ISystemService systemService, ISystemUpdateService updateService)
{
    public bool ServerIsUp => systemService.IsStarted;
}
```

Note the class taking them is *not* the one implementing `IPlugin`. The plugin
class is built with `Activator.CreateInstance` during the plugin scan, before
any container exists, so it must have a public parameterless constructor and can
take no dependencies at all; see
[the plugin overview](../../README.md#iplugin-needs-a-public-parameterless-constructor).
Register a class like the one above from your `RegisterServices` and inject the
services there instead, or take them in `IPlugin.Setup(IServiceProvider)`, which
exists for the plugin class.

---

## `ISystemService`

### The startup timeline, and where a plugin fits into it

Your plugin class is constructed twice, then `IPlugin.Setup` and `IPlugin.Ready`
run, and only after the web host has started is the database opened. The full
order is in
[the plugin overview](../../README.md#the-order-a-plugin-is-started-in). None of
those points is the right place to touch anything that needs the database. The
events below mark the points where it becomes safe:

| Event | Fired | What is ready |
|---|---|---|
| `StartupMessageChanged` | Throughout startup | Nothing in particular. It carries the user-facing progress string. |
| `SetupRequired` | When the server boots into first-run setup instead of starting | Nothing. The server is waiting for the user. |
| `AboutToStart` | After the database, relocation presets, the AniDB UDP handler and the file watchers are all up, and before `Started` | Everything. This is the hook to initialise against. |
| `SetupCompleted` | After `AboutToStart`, on the first run only | Same as `AboutToStart`. |
| `Started` | Immediately after, once `StartedAt` is stamped | Same. It is raised before the startup scan and import jobs are queued, so do not expect them to be waiting yet. |

`AboutToStart` is the one to use. Its `ServerAboutToStartEventArgs` carries the
`IServiceProvider`, so a handler can resolve anything it needs without reaching
for `StaticServices`.

Subscribe from a hosted service, or from `IPlugin.Setup`; the class implementing
`IPlugin` cannot take `ISystemService` in its constructor at all. Hosted
services are started with the host, and `AboutToStart` is fired later, from
`LateStart()`, so the subscription is always in place before the event fires.

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

**`AboutToStart` is invoked synchronously, on the startup thread.** Two things
follow from that, and both bite:

- A slow handler delays the server for everyone. Do the minimum, and hand
  anything expensive to the queue.
- **A handler that throws fails the whole startup.** The exception is caught by
  the startup routine, stored as `StartupFailedException`, and surfaced through
  `StartupFailed` with the message "Failed to start. Check your logs for more
  information." Wrap your handler body in a `try`/`catch` unless you genuinely
  want a bad plugin to stop the server.

`Started`, `SetupRequired` and `SetupCompleted` are dispatched on a background
task instead, so a throwing handler there does not take startup with it. That is
a difference in blast radius, not a reason to throw.

If you would rather await than subscribe, `WaitForStartupAsync()` completes when
the server is up and throws `StartupFailedException` if it is not. `IsStarted`
and `StartedAt` answer the same question after the fact.

### Setup mode

`InSetupMode` is `true` while the server is waiting for a user to complete
first-run setup, and `CompleteSetup()` is what moves it out. That call belongs to
the setup UI, not to a plugin: calling it from plugin code starts the server on
behalf of a user who has not finished configuring it. Read `InSetupMode` if you
need to know; leave `CompleteSetup()` alone.

### Shutdown

| Member | Notes |
|---|---|
| `ShutdownOrRestartRequested` | `CancelEventArgs`. Set `Cancel = true` to refuse a shutdown, for instance while your plugin is mid-transaction. Use this sparingly: a user who asked to stop the server expects it to stop. |
| `Shutdown` | The server is going down now. There is a tight time budget before the parent process kills it, so flush and return. |
| `CanShutdown` / `CanRestart` | Whether a controlled stop or restart is possible at all. |
| `ShutdownPending` / `RestartPending` | Whether one has already been requested. |
| `RequestShutdown()` / `RequestRestart()` | Returns `false` when the request was refused. |
| `WaitForShutdownAsync()` | Awaits the stop. |

### The database gate

The database can be blocked mid-run, during a migration for instance. Three
members cover it:

- `IsDatabaseBlocked`, the current state.
- `DatabaseBlockedChanged`, carrying `IsBlocked`.
- `WaitForDatabaseUnblockedAsync()`, to await the gate opening.

A queue job does not need any of this. `[DatabaseRequired]`, from the `Shoko.QueueProcessor`
package, already holds the job back until the database is up. These members are for code that runs outside the queue.

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

This is a static, process-wide service provider that core sets during startup. It
exists for code that predates DI and cannot be reached through the container.
It carries the `[Obsolete]` attribute deliberately, so every use of it shows up
as a warning.

Prefer, in order:

1. **Constructor injection.** A provider, an action and a queue job are all
   constructed through the container, so they can just ask. The class
   implementing `IPlugin` is the exception: it takes its services in `Setup`.
2. **`ServerAboutToStartEventArgs.ServiceProvider`**, for a handler that needs to
   resolve something late.
3. `StaticServices`, only when neither is available.

Two properties of it to know:

- **It is write-once per process.** The setter throws `InvalidOperationException`
  on a second assignment. Never set it; core does.
- **It throws when read too early.** The getter throws if core has not set it yet.
  `HasStaticServices` tests that without throwing, which matters in unit tests,
  where nothing sets it at all.

---

## `ISystemUpdateService`

This checks release manifests for the server and the WebUI, and installs WebUI
versions. In practice it is driven by the settings UI and the `/api/v3` update
endpoints, and there is very little reason for a plugin to call it. The read side
is harmless if you want it:

- `GetLatestServerVersion(channel, force)` and `GetServerHistory(channel, force)`
  return `ReleaseVersionInformation` off the server manifest.
- `GetLatestWebComponentVersion(...)` and `GetWebComponentHistory(...)` do the
  same for the WebUI, as `WebReleaseVersionInformation`.
- `LoadWebComponentVersionInformation()` and
  `LoadIncludedWebComponentVersionInformation()` read what is installed and what
  shipped in the box.

Both manifest URLs are settable (`ServerManifestUrl`, `WebComponentManifestUrl`),
and both `GetLatest*`/`Get*History` calls are cached unless you pass `force: true`.

The write side, `InstallWebComponentVersion`, `UpdateWebComponent` and
`ReactToManualWebComponentUpdate`, replaces the user's WebUI installation. A
plugin should not be doing that behind the user's back. `WebComponentUpdated`
fires after an install, if you need to react to one.

`ReleaseChannel.Auto` means "match the running server's own channel", which is
what you want almost every time. The explicit members are `Debug`, `Stable` and
`Dev`.
