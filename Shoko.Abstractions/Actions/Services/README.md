# Executable Actions

An action is a discrete, invokable unit of work with a name, a category and a
permission level: "Auto-Search Metadata Links" on a series, "Sync Votes (Export)".
Core registers a few dozen of them, and a plugin can register its own. They are
listed through the API, rendered as buttons in the WebUI, and always executed
through the job queue.

Work that runs on its own, on triggers the admin sets, is not an action but a
[scheduled action](../../ScheduledActions/Services/README.md): global, with no
caller, no parameters and no permission level, run and scheduled by admins
only. "Import New Files" is a scheduled action. A run with options is an action
of its own: the scheduled "Sync AniDB MyList" runs as the MyList settings say,
and the action of the same name, run by a person, forces a fresh MyList
download unless told not to.

This folder is both sides at once: `IExecutableAction` and the four scoped base
classes are an **extension point** you implement, and `IActionService` is the
**consumption surface** you inject to list and invoke actions, yours or anyone
else's.

---

## Registering an action

Implement `IExecutableAction` for a global action, or derive from one of the four
scoped base classes:

```csharp
public class PurgeMyCacheAction(MyCache cache, ILogger<PurgeMyCacheAction> logger) : IExecutableAction
{
    public string Name => "Purge My Plugin's Cache";

    public string? Description => "Drops every cached response, forcing a refetch.";

    public ActionCategory Category => ActionCategory.PluginInferred;

    public ActionPermission Permission => ActionPermission.Admin;

    public bool RequiresConfirmation => true;

    public string? ConfirmationMessage => "Everything will be refetched on the next sweep.";

    public Task Execute(CancellationToken token = default)
    {
        cache.Clear();
        logger.LogInformation("Cache purged.");
        return Task.CompletedTask;
    }
}
```

**There is nothing to register in DI.** Every public, non-abstract class in
your plugin's main assembly that implements `IExecutableAction` is registered
as a **transient** service and handed to the action service at startup. An
`internal` action, or one in a second assembly, is never found, and nothing
says so.

This is `GetTypes<T>()`, not `GetExports<T>()`, so the singleton rule in
[the main README](../../README.md#the-three-branch-registration-rule) does not
apply. An action is resolved fresh for each validation and each execution; keep
long-lived state in a singleton service and let the action be a thin shell over
it.

Only the execution resolves from a job's container. The startup probe and each
`Validate` instance come from the root container, so an `IDisposable` action is
held by the root until shutdown, and a scoped constructor dependency either
throws or lives for the rest of the process. Give an action nothing to dispose
and no scoped dependencies.

### Two mistakes that fail at startup

Both throw `InvalidOperationException` while the action service takes the
discovered actions, which fails the whole server's startup, not just your
plugin.

**Declare `Permission` on the action class itself.** There is no default: a
getter inherited from a base class or an interface default is rejected, so
every action states its permission in its own source file.

**Derive scoped actions directly from the four base classes.** The check is on
the immediate base type, so `MyAction : MyBaseAction : SeriesAction` is
rejected. Share code through a helper service instead. (`IScopedAction` is
internal to `Shoko.Abstractions`, so a plugin cannot implement it directly.)

### Action IDs change when you rename the class

An action's ID is a UUIDv5 of the action class's **fully-qualified name**,
namespaced by the owning plugin's ID. It is **not stable across a class rename
or a namespace move**: a saved shortcut, a script or another plugin holding the
old ID stops resolving. Treat the class's full name as public surface.

Get the ID of one of your own actions with
`IActionService.GetActionInfo<TAction>()` rather than deriving it yourself.

---

## Scope

A global action implements `IExecutableAction` directly. A scoped action derives
from a base class that fixes the scope at compile time and hands you the entity:

| Base class | `ActionScope` | Protected property |
|---|---|---|
| `GroupAction` | `Group` | `IShokoGroup Group` |
| `SeriesAction` | `Series` | `IShokoSeries Series` |
| `EpisodeAction` | `Episode` | `IShokoEpisode Episode` |
| `VideoAction` | `Video` | `IVideo Video` |

```csharp
public class RefreshFromMySourceAction(MyClient client) : SeriesAction
{
    public override string Name => "Refresh From My Source";

    public override ActionPermission Permission => ActionPermission.User;

    public override Task Execute(CancellationToken token = default)
        => client.RefreshAsync(Series, token);
}
```

The entity is set before `Validate` and `Execute` run.

Scope is a property of the action type, not of the entity: listings are
filtered by scope, never by entity. If your action only applies to *some*
series, say so from `Validate`.

---

## Knowing who invoked it

Implement `IActionCaller` to receive the invoking user. Any action of any scope
may do this:

```csharp
public class ExportMyListAction : IExecutableAction, IActionCaller
{
    private IUser _caller = null!;

    public void SetCaller(IUser caller) => _caller = caller;

    // ...
}
```

An action implementing `IActionCaller` **requires** a caller: an invocation
with `caller: null` is rejected ("The action 'X' requires a calling user.")
rather than run without one. If your action can work either way, do not
implement the interface.

---

## Reporting progress

Implement `IProgressReportingAction` to show how far a long action is on its
queue job:

```csharp
public class RefreshEverythingAction(MyClient client) : IExecutableAction, IProgressReportingAction
{
    private IProgress<decimal> _progress = null!;

    public void SetProgress(IProgress<decimal> progress) => _progress = progress;

    public async Task Execute(CancellationToken token = default)
    {
        var items = await client.ListAsync(token);
        _progress.Report(0);
        for (var i = 0; i < items.Count; i++)
        {
            await client.RefreshAsync(items[i], token);
            _progress.Report(100m * (i + 1) / items.Count);
        }
    }
}
```

The value is a percentage from 0 to 100, kept in memory only. The job shows no
progress until the action first reports.

---

## The invocation path

Every invocation, from the API or from a plugin, follows the same sequence:

1. **Scope check.** The entity handed in must match the action's declared scope.
   A mismatch is a rejection, not an exception.
2. **Permission check.** An `Admin` action invoked by a non-admin user is
   rejected. **A `null` caller skips this check entirely**, because it means a
   trusted in-process call rather than an anonymous one.
3. **Validate.** A fresh instance is resolved from DI, given its scope entity,
   its caller and the invocation parameters, and `Validate` is awaited. Returning
   a non-null `ActionValidationResult` rejects the invocation before anything
   touches the queue; the API maps it to a 400 with `Reason` as the message. That
   probe instance is then discarded.
4. **Enqueue.** An `ActionExecutionJob` goes onto the queue carrying the action
   ID, scope, entity ID, caller ID and parameters.
5. **Validate again.** The job resolves **another** fresh instance, repopulates
   it, and awaits `Validate` a second time. A rejection here is not a failure:
   the job logs the reason and completes without running, and without spending
   its retry budget rediscovering a condition that is not going to change back.
6. **Execute.** Only then is `Execute` awaited, on that same second instance,
   after an `IProgressReportingAction` is handed its progress reporter.

What follows from that:

- **`Validate` runs twice, on two instances, and the second answer decides.**
  The first gives the caller a 400 instead of a doomed job; the second runs when
  the job reaches the front of the queue, possibly hours later, when what
  actions check (a provider still enabled, a file still having a location) may
  have changed. Keep `Validate` cheap, side-effect free and truthful about the
  present. Its first run gets the API request's token.
- **The instance that validated is not the one that executes.** Anything the
  first `Validate` computes is thrown away.
- **`Execute`'s token is the queue job's.** It is cancelled when a user cancels
  the running action, and when its pool stops (shutdown or a queue stop). Throw
  `OperationCanceledException` (`token.ThrowIfCancellationRequested()`) to stop:
  after a user cancel the action ends as cancelled and is not retried, after a
  shutdown it goes back in the queue. Nothing kills a running action; until it
  notices, the queue shows it as cancellation requested.
- **`Execute` runs for whoever invoked it.** The job carries the invoking API
  token as its actor, so events the action raises name that caller (see
  [`IActorContext`](../../User/Services/README.md#iactorcontext-who-did-it)).
- **There is no result hook.** An action reports by logging. An exception out of
  `Execute` is recorded as a job failure.
- **Nothing limits how many actions run at once.** Every action runs inside the
  one wrapper job `ActionExecutionJob`, which has no concurrency attributes, and
  attributes on your own action class are never read. Two invocations of your
  action can overlap; if it is not reentrant, guard it with a lock or semaphore
  on an injected singleton.

---

## Invoking actions: `IActionService`

Inject it as a DI singleton.

```csharp
public class MyService(IActionService actionService, ILogger<MyService> logger)
{
    public async Task RunIt(IShokoSeries series)
    {
        var info = actionService.GetActions(ActionScope.Series)
            .FirstOrDefault(a => a.Name == "Auto-Search Metadata Links");
        if (info is null)
            return;

        // null caller: a trusted in-process call, permission check skipped.
        if (await actionService.InvokeAsync(info.ID, series) is { } rejected)
            logger.LogWarning("Refused: {Reason}", rejected.Reason);
    }
}
```

A scheduled action, as "Import New Files", is not listed here; run it through
`IScheduledActionService.InvokeAsync` instead.

| Member | Notes |
|---|---|
| `GetActions(scope?, callerPermission?)` | `scope` is a filter, not a required partition: omit it to list everything. Passing `ActionPermission.User` narrows the list to actions a regular user may invoke. Ordered by category, then category name, then name. |
| `GetActionInfo(Guid)` | `null` when the ID is not registered. |
| `GetActionInfo<TAction>()` / `GetActionInfo(Type)` | The same, by action type. `null` when the type is not a registered action. |
| `InvokeAsync(...)` | One overload per scope, each taking optional `parameters`, a `caller` and a cancellation token. |

**`null` means accepted and queued.** A non-null `ActionValidationResult`
means refused, with `Reason` saying why. An **unregistered action ID throws
`KeyNotFoundException`**, so check with `GetActionInfo` first when the ID came
from somewhere you do not control.

`ExecutableActionInfo` carries `ID`, `Name`, `Description`, `Category`,
`CategoryName`, `IsPrimaryAction`, `Scope`, `Permission`,
`RequiresConfirmation`, `ConfirmationMessage` and `PluginId`. The concrete
action type stays inside the server, so you invoke by ID.

### Invocation parameters

The `parameters` argument is a free-form dictionary that is populated onto
matching public settable properties of the action instance, the same way queue
jobs are populated from their job data:

```csharp
public class SweepAction : IExecutableAction
{
    public bool Force { get; set; }
    public List<string> Sources { get; set; } = [];
    // ...
}

await actionService.InvokeAsync(id, new Dictionary<string, object?>
{
    ["Force"] = true,
    ["Sources"] = new List<string> { "tmdb" },
});
```

Booleans, numbers and string lists are supported; nested objects are not. A
name with no matching property is ignored silently, typos included. Both the
validation probe and the executing instance are populated, so `Validate` sees
what `Execute` will.

An omitted parameter keeps the value the instance was constructed with, so a
parameter is optional unless the action marks it `[Required]` or declares it
`required`. Such a parameter is marked required in the action's UI definition,
and `ValidateParameters` refuses a body without it, an absent body included.

### Asking without running

`ValidateAsync` runs everything `InvokeAsync` does before it queues (scope,
permission and the action's own `Validate`) and queues nothing. Pass the
parameters the invocation would carry, since `Validate` sees them:

```csharp
var refusal = await actionService.ValidateAsync(actionId, series, caller: user);
```

`null` means it would be accepted, which is how a client greys out an action.
For a list, call it once per entity.

**The answer is advisory.** An invocation can still be refused for a reason that
was not true a moment earlier, so handle the rejection anyway.

### Applying one action to many entities

`InvokeBulkAsync` takes a list of entities of one scope instead of a single one,
and queues the action **for all of them or for none**:

```csharp
await actionService.InvokeBulkAsync(actionId, selectedSeries, caller: user);
```

Every entry is validated first, and nothing is queued unless all of them
passed, so a caller can fix the selection and retry.

A rejection throws `GenericValidationException`. Each entry's failure is keyed
`IDs[i]`, by its position in the list you passed. Failures of the action itself
(wrong scope, not permitted for this caller) are keyed by the empty string and
fail the call as a whole. An unregistered action ID throws
`KeyNotFoundException`, as it does for `InvokeAsync`.

All-or-none is a promise about the queue, not the work: each entry's job
validates again when it runs, and one whose conditions changed skips while the
rest go through.

Over HTTP this is `POST /api/v3/Action/{actionID}/{Group,Series,Episode,File}/Bulk`,
taking `{ "IDs": [1, 2, 3], "Parameters": { … } }`. An ID naming nothing is
reported against its `IDs[i]` key before the action is consulted.

### Repeated invocations collapse

An action's dedup key is the action ID, the scope entity ID, the caller's user
ID and the parameters. Invoking the same action on the same entity, as the same
user, with the same parameters, while an identical job is still waiting is a
no-op. A bulk invocation dedups per entity. Differing parameters enqueue
separately.

---

## Categories

`ActionCategory` is a closed, core-owned enum: `Import`, `AniDB`, `Sync`,
`Images`, `Maintenance`, `Miscellaneous`, `Destructive`, `PluginInferred`.
AniDB, which every series is built on, is the only source with a named
category. The actions of any other source, a bundled plugin's included, go
under what they do (`Images`, `Maintenance`, `Destructive`, …) or under the
plugin's own group. A plugin's real choices are two:

- **`Miscellaneous`**, the shared fallback and the default. Fine for a one-off.
- **`PluginInferred`**, a group of your own labelled with your plugin's name.
  Use it when your plugin contributes several actions that belong together.

A core category such as `Images` or `Sync` fits only when your action genuinely
belongs alongside core's.

`IsPrimaryAction` (default `false`) is a separate axis: it says the action is
prominent enough to be offered on its own, outside its category, which it keeps
either way. Promote sparingly.

`RequiresConfirmation` and `ConfirmationMessage` are UI hints: the WebUI prompts
before invoking, with a generic prompt when the message is null. They are not a
security boundary; `Permission` is.
