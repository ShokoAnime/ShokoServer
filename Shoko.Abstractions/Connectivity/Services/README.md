# Connectivity

`IConnectivityService` answers one question: can this machine reach the internet
right now? It is not an extension point (`IConnectivityMonitor` is a data shape:
a name, a URL and a request method). Inject it:

```csharp
public class MyJob(IConnectivityService connectivityService) { }
```

---

## Most plugins should not call it

Mark a queue job `[NetworkRequired]` instead: the queue holds it back until
`NetworkAvailability` reaches `PartialInternet` or better, so it never takes a
worker slot just to bail. Read the service yourself only for work outside the
queue: a timer of your own, a controller, an event handler.

---

## The availability ladder

`NetworkAvailability` is ordered, which is the point. Compare with `>=` rather
than matching each member.

| Value | Meaning |
|---|---|
| `NoInterfaces` | No network interfaces at all. |
| `NoGateways` | Interfaces, but no usable local gateway. |
| `LocalOnly` | A gateway was found. The LAN works; the internet was not reached. |
| `PartialInternet` | Some of the WAN probes answered. |
| `Internet` | All of them did. |

`PartialInternet` is the threshold core uses, and usually the right one: with
`Internet`, a single blocked probe host stops your plugin.

```csharp
if (connectivityService.NetworkAvailability < NetworkAvailability.PartialInternet)
    return;
```

---

## Reading and reacting

| Member | Notes |
|---|---|
| `NetworkAvailability` | The last known state. A cached value, not a live probe. |
| `LastChangedAt` | When it last changed. Local time, not UTC. |
| `CheckAvailability()` | Probes now and returns the updated state. |
| `NetworkAvailabilityChanged` | Fires only on an actual change, carrying the new value and the timestamp. |

The state is refreshed by the "Check Network Availability" scheduled action,
by default at startup, every 30 minutes and whenever the queue is cleared (the
admin can change its triggers).
Between runs the property is the last answer.

`CheckAvailability()` makes real HTTP requests, five seconds' timeout each, so
call it only when you have reason to believe the answer is stale. It never
throws: a failure is logged and reported as `NoInterfaces`.

`NetworkAvailabilityChanged` is dispatched on a background task, with **`sender`
`null`**, and only on transitions. Read `NetworkAvailability` once when you
subscribe rather than waiting for an initial event:

```csharp
public MyService(IConnectivityService connectivityService)
{
    _isOnline = connectivityService.NetworkAvailability >= NetworkAvailability.PartialInternet;
    connectivityService.NetworkAvailabilityChanged += (_, e)
        => _isOnline = e.NetworkAvailability >= NetworkAvailability.PartialInternet;
}
```

---

## Monitor definitions

The WAN check requests a list of endpoints. The defaults are CloudFlare
(`HEAD https://1.1.1.1/`), Mozilla's captive portal endpoint (`HEAD`) and
WeChat (`GET`), the last for networks where the first two are unreachable.

`GetMonitorDefinitions()` lists them, `AddMonitorDefinition(MonitorDefinitionData)`
adds one and `RemoveMonitorDefinition(name)` removes one by name,
case-insensitively; both persist the list to the server settings.

A plugin may add a monitor when the defaults do not prove its own service is
reachable, but:

- **It changes a global, user-visible setting** that survives your plugin being
  uninstalled. Remove it when you no longer need it.
- **It makes every check slower** for everyone.
- **`AddMonitorDefinition` throws `ArgumentException`** on a blank name or
  address, a duplicate name, or an address that is not an absolute URI.
  `RemoveMonitorDefinition` returns `false` when there was nothing to remove.

Use `HEAD` (`ConnectivityCheckType.Head`) where the endpoint supports it.

---

## Suspensions

A service you talk to may refuse work for a while: it rate limits you, bans
you, answers with server errors, or rejects your key. Say so through the
suspension contract, in `Connectivity/Suspensions/`, and the core holds back
the queued jobs of the providers you name, shows the status to clients and
lets an admin lift what can be lifted.

Export one `ISuspensionProvider` per status (a service with two channels that
are limited on their own exports two):

```csharp
public sealed class MyServiceSuspensionProvider : ISuspensionProvider
{
    public string Name => "My Service";

    public string? Description => null;

    public IReadOnlyList<Type> HeldProviderTypes => [typeof(MyMetadataProvider)];

    public Task Lift(SuspensionKind kind, CancellationToken token) => Task.CompletedTask;
}
```

Report through `ISuspensionReporter<TProvider>`, injected wherever the
knowledge lives, such as your rate limiter:

```csharp
public class MyRateLimiter(ISuspensionReporter<MyServiceSuspensionProvider> reporter)
{
    public void OnTooManyRequests(TimeSpan retryAfter)
        => reporter.Suspend(SuspensionKind.RateLimited, resumesAt: DateTime.UtcNow + retryAfter);
}
```

| Member | Notes |
|---|---|
| `Suspend(kind, reason, resumesAt, isLiftable)` | One suspension per kind. Reporting a kind again updates its reason, end and liftability, and keeps `RaisedAt`. An end already in the past is ignored. |
| `Resume(kind)`, `ResumeAll()` | End what you reported, as soon as you know it is over. |
| `Current` | Your status as the core keeps it. |

- **Ends.** With a `resumesAt` the core clears the suspension once it passes,
  so you never need a timer of your own. Without one it lasts until you
  resume it or an admin lifts it.
- **Reasons.** Leave `reason` `null` unless the service told you something a
  client could not word from the kind. The core adds no text of its own.
- **Lifting.** Only a suspension reported with `isLiftable: true` can be
  lifted. The core calls your `Lift`, then removes it.
- **Timing.** Every reporter member throws `InvalidOperationException` until
  the providers are registered at start-up, and for a provider type that was
  not loaded.

`ISuspensionService` reads it all back: `GetAll`, `Get(providerID)`,
`GetForSource(source)` (the statuses holding a source's metadata providers),
`Lift`, and `SuspensionChanged`, raised after every change with what was
raised, updated and removed (and why: `Expired`, `Resumed` or `Lifted`).

Clients read the same statuses from `GET /api/v3/Suspension` and the
`suspension` SignalR feed; an admin lifts through
`POST /api/v3/Suspension/{providerID}/{kind}/Lift`.
