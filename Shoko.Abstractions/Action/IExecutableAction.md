# IExecutableAction — Plugin Author's Guide

## Overview

`IExecutableAction` is the base interface for all plugin-registrable actions.
A plugin action is a discrete, invokable unit of work that can be triggered by ID
via the API. All actions appear in the same listing endpoints regardless of their
permission level — admins see everything, standard users only see user-facing
actions.

## Sub-interfaces

Rather than a single `Execute(CancellationToken)` as proposed by the RFC, this
implementation defines 8 scope-specific sub-interfaces, each providing exactly
the context the action needs:

| Interface | Execute Signature | Description |
|-----------|-------------------|-------------|
| `IExecutableGlobalSystemAction` | `Execute(CancellationToken)` | Global scope, system-level (admin). No user context. |
| `IExecutableGlobalUserAction` | `Execute(IUser, CancellationToken)` | Global scope, user-level (any auth). Operates on behalf of the given user. |
| `IExecutableGroupSystemAction` | `Execute(IShokoGroup, CancellationToken)` | Group scope, system-level (admin). |
| `IExecutableGroupUserAction` | `Execute(IShokoGroup, IUser, CancellationToken)` | Group scope, user-level (any auth). |
| `IExecutableSeriesSystemAction` | `Execute(IShokoSeries, CancellationToken)` | Series scope, system-level (admin). |
| `IExecutableSeriesUserAction` | `Execute(IShokoSeries, IUser, CancellationToken)` | Series scope, user-level (any auth). |
| `IExecutableEpisodeSystemAction` | `Execute(IShokoEpisode, CancellationToken)` | Episode scope, system-level (admin). |
| `IExecutableEpisodeUserAction` | `Execute(IShokoEpisode, IUser, CancellationToken)` | Episode scope, user-level (any auth). |

The interface an action implements is a self-documenting declaration of what it
needs — no guesswork about whether `Execute` will receive a series, a user, or
neither.

**Note:** implementing `IExecutableAction` directly without any sub-interface
will not register a valid action — the service detects scope support only from
the sub-interfaces. At least one sub-interface must be implemented for the
action to be registered.

## Multi-scope actions

A single class can support multiple scopes by implementing more than one
sub-interface:

```csharp
internal sealed class MyAction : IExecutableGlobalSystemAction, IExecutableSeriesSystemAction
{
    public string Name => "My Action";
    public ActionCategory Category => ActionCategory.Miscellaneous;

    public Task Execute(CancellationToken cancellationToken = default) { … }
    public Task Execute(IShokoSeries series, CancellationToken cancellationToken = default) { … }
}
```

`ActionService.AddParts` detects every implemented interface via
`typeof(T).IsAssignableFrom` and registers each as a supported scope on
`ExecutableActionInfo.Scopes`. The action then appears in both the global and
series listing endpoints.

## Optional defaults

| Member | Default | Description |
|--------|---------|-------------|
| `Name` | Inferred from class name | When `null`, the service derives a display name from the implementing class (splits PascalCase, strips "Action" suffix). |
| `Description` | `null` | Longer description of what the action does. |
| `Category` | `ActionCategory.Mischievous` | Groups the action into a named category in the UI. See `ActionCategory.md` for available values. |
| `RequiresConfirmation` | `false` | Set to `true` for destructive or irreversible actions so the UI prompts for confirmation before invoking. |

## ID derivation

Each action's stable identifier (`ExecutableActionInfo.ID`) is a UUIDv5
deterministically derived from:

- **Namespace:** the owning plugin's ID
- **Name:** the action class's fully-qualified name

```
UuidUtility.GetV5(actionType.FullName, pluginInfo.ID)
```

**This ID is not stable across class renames or namespace moves.** If you rename
or move the implementing class, the derived UUID will change. Existing references
to the old ID will no longer resolve. This is by design — deriving from plugin
ID + class name makes accidental collisions between unrelated plugins
structurally impossible.

## Lifetime

Action instances are **transient**. A fresh instance is created via
`IPluginManager.GetExport<IExecutableAction>(type)` on each execution. If you
need to persist state across invocations, declare your action as a singleton
in your plugin's DI registration — but this is your responsibility, not the
framework's.

## Permission model

The permission level is encoded in which sub-interface you implement. The API
layer enforces access before the action ever runs:

- **`*SystemAction`** — the API layer rejects non-admin callers before
  the action is scheduled.
- **`*UserAction`** — any authenticated user may invoke. The API layer ties
  the invocation to the caller's API token. The `IUser` parameter identifies
  the user on whose behalf the action runs.

See `ActionScope.md` for details on how `System` and `User` flags work
under the hood.

## Execution

Actions triggered through the API always run via the job queue system — the API
schedules execution through `ScheduleExecuteOf*` methods, ensuring progress and
status are visible through the standard queue UI and error logging.

Plugins may use the `IActionService.Execute*` methods directly to run an action
immediate in-process without going through the queue system, if needed.
