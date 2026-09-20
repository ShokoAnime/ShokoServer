# Executable Actions

An action is a discrete, invokable unit of work with a name, a category and a
permission level: "Run Import", "Sync MyList", "Refresh this series from TMDB".
Core registers a few dozen of them, and a plugin can register its own. They are
listed through the API, rendered as buttons in the WebUI, and always executed
through the job queue.

This folder is unusual among the service folders in being **both** sides at once:

- `IExecutableAction` and the four scoped base classes are an **extension
  point**. You implement them.
- `IActionService` is a **consumption surface**. You inject it to list and invoke
  actions, your own or anyone else's.

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

That is all. **There is nothing to register in DI.** `PluginManager` collects
every public, non-abstract class in your plugin's main assembly assignable to
`IExecutableAction`, registers each one as a **transient** service, and hands
the list to the action service during startup. An `internal` action, or one in
a second assembly your plugin ships, is never found, and nothing says so.

Note that this uses `GetTypes<IExecutableAction>()`, not `GetExports<T>()`, so the
three-branch singleton rule in [the main README](../../README.md) does not apply
here. If anything the guidance inverts: an action is resolved fresh for each
validation and each execution, and registering one as a singleton yourself would
break that. Dependencies your action takes in its constructor still resolve
normally, so put the long-lived state in a singleton service and let the action
be a thin shell over it.

Only the execution resolves from a job's container. The probe the action
service takes at startup, and the instance each `Validate` runs on, come from
the root container. That has two consequences. An action that implements
`IDisposable` is held by the root until shutdown, once for startup and once
more per validation, so don't give an action anything to dispose; keep it on
the singleton. And a scoped service in the constructor is resolved from the
root as well, where it either throws or lives for the rest of the process.

### Two mistakes that fail at startup

Both throw `InvalidOperationException` while the action service takes the
discovered actions during startup. That happens inside plugin initialisation,
so the exception takes the whole server's startup down with it, not just your
plugin: loud rather than subtle.

**Declare `Permission` on the action class itself.** There is deliberately no
default, and the registry checks that the getter's declaring type is the action
type. Inheriting it from your own intermediate base class, or leaning on an
interface default, is rejected. Every action states its permission in its own
source file, where a reviewer will see it.

**Derive scoped actions directly from the four base classes.** The check is on
the action's immediate base type, so `MyAction : MyBaseAction : SeriesAction` is
rejected the same way an unrelated type would be. Share code between scoped
actions through a helper service, not an intermediate base class. (`IScopedAction`
itself is internal to `Shoko.Abstractions`, so a plugin can't implement it
directly anyway.)

### Action IDs change when you rename the class

An action's ID is a UUIDv5 derived from the action class's **fully-qualified
name**, with the owning plugin's ID as the namespace. That makes collisions
between unrelated plugins effectively impossible without asking anyone to manage
an explicit key.

The cost is that the ID is **not stable across a class rename or a namespace
move**. Anything holding the old ID, a saved shortcut, a script hitting
`/api/v3`, another plugin, stops resolving. Treat the class's full name as part
of your public surface and rename with the same care.

To get the ID of one of your own actions, ask for it by type with
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

The entity is populated by the framework before `Validate` and `Execute` run, so
it is non-null by the time your code sees it.

Scope is a property of the action type, not of the particular entity. Which
actions exist for a series does not vary from one series to the next, so listings
are filtered by scope and never by entity. If your action only applies to *some*
series, say so from `Validate` rather than trying to hide it from the list.

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

The catch: an action implementing `IActionCaller` **requires** a caller.
A trusted programmatic invocation that passes `caller: null` is rejected with
"The action 'X' requires a calling user." rather than running without one. If
your action can work either way, do not implement the interface.

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
6. **Execute.** Only then is `Execute` awaited, on that same second instance.

Consequences worth internalising:

- **`Validate` runs twice, on two different instances, and the second answer is
  the one that decides.** The first runs on the request thread so a caller gets
  a 400 instead of a job that was never going to work; the second runs in the
  queue, where the state has moved on. A queue can be hours deep, and what
  actions check — a provider still being enabled, a file still having a
  location, a user still being linked — is exactly what changes in that time.
  Write `Validate` so it can be asked twice: cheap, side-effect free, and
  truthful about the present rather than about when it was queued.
- **The instance that validated is not the instance that executes.** Anything
  the first `Validate` computes is thrown away. Do the work again in `Execute`,
  or keep it in an injected singleton.
- **`Validate` runs on the request thread and `Execute` does not.** `Validate`
  should be a cheap precondition check, not the work, and its token is the API
  request's.
- **`Execute`'s token is the worker pool's, so it only fires on shutdown.**
  `ActionExecutionJob` passes the token of the pool running it, taken from
  `IJobCancellationAccessor`. It is cancelled when that pool stops, which means
  server shutdown or an explicit queue stop, and at no other time. There is no
  way to cancel one running action, so an action that polls the token expecting
  a user to be able to stop it will wait forever. Honour it anyway, so a long
  action does not hold up shutdown, and keep `Execute` short enough that the
  difference does not matter. Note nothing kills a running action either: the
  pool waits for in-flight work to finish, and the token only lets a polite
  action cut its own work short.
- **There is no result hook.** An action reports what it did by logging, the same
  as every other queue job. Exceptions out of `Execute` are caught by the worker
  and recorded as a job failure.
- **Nothing constrains how many actions run at once, and you cannot change
  that.** The queue's `[LimitConcurrency]`, `[DisallowConcurrentExecution]` and
  `[DisallowConcurrencyGroup]` attributes are read only off registered
  `IQueueJob` types. An action is not one: every action runs inside the single
  wrapper job `ActionExecutionJob`, which carries no concurrency attributes at
  all, so an attribute you put on your own action class is never looked at.
  Every action in the server shares that one job's unconstrained pool, which
  also means two invocations of *your* action can overlap, as can your action
  and someone else's. If your action is long-running or not reentrant, guard it
  yourself, with a lock or a semaphore on an injected singleton.

---

## Invoking actions: `IActionService`

Inject it as a DI singleton.

```csharp
public class MyService(IActionService actionService)
{
    public async Task RunIt()
    {
        var info = actionService.GetActions(ActionScope.Global)
            .FirstOrDefault(a => a.Name == "Run Import");
        if (info is null)
            return;

        // null caller: a trusted in-process call, permission check skipped.
        if (await actionService.InvokeAsync(info.Id) is { } rejected)
            logger.LogWarning("Refused: {Reason}", rejected.Reason);
    }
}
```

| Member | Notes |
|---|---|
| `GetActions(scope?, callerPermission?)` | `scope` is a filter, not a required partition: omit it to list everything. Passing `ActionPermission.User` narrows the list to actions a regular user may invoke. Ordered by category, then category name, then name. |
| `GetActionInfo(Guid)` | `null` when the ID is not registered. |
| `GetActionInfo<TAction>()` / `GetActionInfo(Type)` | The same, by action type. `null` when the type is not a registered action. |
| `InvokeAsync(...)` | One overload per scope, each with a `parameters` variant. |

**The return value reads backwards from the usual convention.** `null` means
accepted and queued. A non-null `ActionValidationResult` means refused, and
`Reason` says why. An **unregistered action ID throws `KeyNotFoundException`**
rather than returning a rejection, so check with `GetActionInfo` first if the ID
came from somewhere you do not control.

`ExecutableActionInfo` is the whole of what a plugin sees: `Id`, `Name`,
`Description`, `Category`, `CategoryName`, `IsPrimaryAction`, `Scope`,
`Permission`, `RequiresConfirmation`, `ConfirmationMessage` and `PluginId`. The
concrete action
type stays inside the server on purpose, so you invoke by ID rather than by type.

### Invocation parameters

The `parameters` overloads take a free-form dictionary that is populated onto
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

Booleans, numbers and string lists are supported; nested objects are not. Names
with no matching property are ignored silently, so a typo in a key looks exactly
like everything working. Both the validation probe and the executing instance are
populated, so `Validate` sees what `Execute` will see rather than the compiled-in
defaults.

### Asking without running

`ValidateAsync` runs everything `InvokeAsync` does before it queues — scope,
permission, and the action's own `Validate` — and queues nothing:

```csharp
var refusal = await actionService.ValidateAsync(actionId, series, caller: user);
```

`null` means it would be accepted. This is how a client greys out an action it
would otherwise only learn about by invoking it and reading the rejection.
For a list, loop it — validation is advisory, so asking once per entity loses
nothing that asking in one call would have kept.

⚠️ **The answer is advisory.** Nothing holds still between asking and invoking,
so an invocation can still be refused for a reason that was not true a moment
earlier. Invoke and handle the rejection; do not treat a clean validation as
permission to skip that.

Validating a *parameter payload* is a separate question and a separate method,
`ValidateParameters`, which checks a document against the action's parameter
schema. `ValidateAsync` takes parameters that are already a typed dictionary.

### Applying one action to many entities

`InvokeBulkAsync` takes a list of entities of one scope instead of a single one,
and queues the action **for all of them or for none**:

```csharp
await actionService.InvokeBulkAsync(actionId, selectedSeries, caller: user);
```

Every entry is validated first, and the action is queued for all of them only if
all of them passed. A set containing one entry the action refuses therefore
changes nothing, which is the point: a caller applying an action to a selection
wants to fix the selection and retry, not discover afterwards which half of it
ran.

A rejection throws `GenericValidationException` rather than returning a reason,
because a single reason cannot say which of twenty entries objected. Each entry's
failure is keyed `IDs[i]`, by its position in the list you passed, so a caller can
map every failure back to what it sent. Whether the action exists, applies to the
scope, and may be invoked by this caller are properties of the *action* rather
than of any entry, so they fail the call as a whole — keyed by the empty string —
instead of repeating the same complaint once per entry.

Queuing is still all it does, so all-or-none is a promise about the queue rather
than about the work. Each entry is validated again when its own job runs, and one
whose conditions have changed by then skips while the rest go through. An entry
that fails outright once it reaches the front of the queue is a failed job and is
reported as one; neither reaches back into the call that enqueued it.

Over HTTP this is `POST /api/v3/Action/{actionID}/{Group,Series,Episode,File}/Bulk`,
taking `{ "IDs": [1, 2, 3], "Parameters": { … } }`. An ID naming nothing is
reported the same way, against the same key, before the action is consulted.

### Repeated invocations collapse

The queue deduplicates, and an action's dedup key is the action ID, the scope
entity ID, the caller's user ID and the parameters. Invoking the same action, on
the same entity, as the same user, with the same parameters, while an identical
job is still waiting is a no-op rather than a second run. A bulk invocation is
one job per entity and dedups per entity, so an entity already queued for the
same action is skipped while the rest go through. Differing parameters
enqueue separately. This is usually what you want from a button, and something to
remember if you were expecting a per-call fan-out.

---

## Categories

`ActionCategory` is a closed, core-owned enum: `Import`, `AniDB`, `TMDB`,
`AniList`, `Sync`, `Images`, `Maintenance`, `Miscellaneous`, `Destructive`,
`PluginInferred`. A plugin cannot invent a category at runtime; adding a
core-owned one takes a PR against core.

Two of them are the plugin author's real choices:

- **`Miscellaneous`** is the shared fallback, and the default when an action
  declares no category. Fine for a one-off.
- **`PluginInferred`** asks for a group of your own. Its display label is always
  your plugin's own name, which is collision-free because plugin names are
  unique. This is what you want when your plugin contributes several actions that
  belong together.

Picking a core category such as `TMDB` is reasonable when your action genuinely
belongs alongside core's, and misleading otherwise.

`IsPrimaryAction` is a separate axis and defaults to `false`. A category says
what an action is about and groups it with its peers; this says whether the
action is prominent enough to be offered on its own, outside that group. The two
do not trade off, so an action keeps the category it belongs to whether or not
it is promoted, and a client with no room for the distinction is free to ignore
the flag. Promote sparingly: a list where everything is primary has no primary.

`RequiresConfirmation` and `ConfirmationMessage` are UI hints. The WebUI prompts
before invoking, falling back to a generic prompt when the message is null. They
are not a security boundary; `Permission` is.
