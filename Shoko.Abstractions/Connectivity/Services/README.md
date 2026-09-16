# Connectivity

`IConnectivityService` answers one question: can this machine reach the internet
right now? It is a small, read-mostly service, and this is a short page because
there is not much to it.

It is not an extension point. Nothing in this folder is discovered by
`PluginManager.GetExports<T>()`, and `IConnectivityMonitor` is a data shape (a
name, a URL and a request method) rather than a provider interface. The service
is a DI singleton, so a plugin injects it:

```csharp
public class MyJob(IConnectivityService connectivityService) { }
```

---

## Most plugins should not call it

If your work runs as a queue job, mark the job `[NetworkRequired]` and stop
thinking about connectivity. The queue's `NetworkRequiredAcquisitionFilter`
already holds such jobs back until `NetworkAvailability` reaches
`PartialInternet` or better, and re-evaluates on every change. That is a better
place for the check than the job body, because a job that never starts costs
nothing, while a job that starts and bails still occupies a worker slot.

Read the service yourself when the work does not go through the queue at all: a
timer of your own, a controller, an event handler.

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

`PartialInternet` is the threshold core itself uses, and usually the right one.
Insisting on `Internet` means a single unreachable probe, a blocked host or a
regional outage, stops your plugin from doing anything.

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

The state is refreshed by `CheckNetworkAvailabilityJob`, a recurring job that
runs every 30 minutes and once at startup. Between those, the property is simply
the last answer.

`CheckAvailability()` performs real HTTP requests with a five second timeout
each, so it is not free. Call it when you have a specific reason to believe the
answer is stale, not on every operation. It never throws: a failure is logged and
reported as `NoInterfaces`.

`NetworkAvailabilityChanged` is dispatched on a background task, and **`sender`
is `null`**, so do not read it. The event fires only on transitions, so a
subscriber that arrives while the network is already up hears nothing until
something changes. Read `NetworkAvailability` once when you subscribe rather than
assuming an initial event:

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

The WAN check works by requesting a list of endpoints. The defaults are
CloudFlare (`HEAD https://1.1.1.1/`), Mozilla's captive portal endpoint (`HEAD`)
and WeChat (`GET`), the last of which exists so the check still works from
networks where the first two are unreachable.

`GetMonitorDefinitions()` lists them. `AddMonitorDefinition(MonitorDefinitionData)`
adds one and `RemoveMonitorDefinition(name)` removes one by name,
case-insensitively; both persist the whole list to the user's server settings.

Adding a monitor is a legitimate thing for a plugin to do when your service lives
somewhere the defaults do not prove reachability for, a network where the default
hosts are blocked but yours is not, for instance. But be aware of what it is:

- **It changes a global, user-visible setting**, and it survives your plugin
  being uninstalled. If you add one, remove it again when you no longer need it.
- **It makes every future check slower**, since the probe list grows for
  everyone.
- **`AddMonitorDefinition` throws `ArgumentException`** on a blank name or
  address, a name that already exists (case-insensitively), or an address that is
  not an absolute URI. `RemoveMonitorDefinition` does not throw; it returns
  `false` when there was nothing to remove.

Use `HEAD` where the endpoint supports it. `ConnectivityCheckType` offers only
`Get` and `Head`.
