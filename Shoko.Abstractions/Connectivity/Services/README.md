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
