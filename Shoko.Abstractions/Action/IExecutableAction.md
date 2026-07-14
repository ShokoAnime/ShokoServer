# IExecutableAction — Plugin Author's Guide

## Overview

`IExecutableAction` is the base interface for all plugin-registrable actions.
A plugin action is a discrete, invokable unit of work that appears in the Shoko
Actions menu and can be triggered by ID via the API.

## Sub-interfaces

Rather than a single `Execute(CancellationToken)` as proposed by the RFC, this
implementation defines 8 scope-specific sub-interfaces, each providing exactly
the context the action needs:

| Interface | Execute Signature | Use Case |
|-----------|-------------------|----------|
| `IExecutableGlobalSystemAction` | `Execute(CancellationToken)` | Admin-only global action |
| `IExecutableGlobalUserAction` | `Execute(IUser, CancellationToken)` | User-facing global action |
| `IExecutableGroupSystemAction` | `Execute(IShokoGroup, CancellationToken)` | Admin-only group action |
| `IExecutableGroupUserAction` | `Execute(IShokoGroup, IUser, CancellationToken)` | User-facing group action |
| `IExecutableSeriesSystemAction` | `Execute(IShokoSeries, CancellationToken)` | Admin-only series action |
| `IExecutableSeriesUserAction` | `Execute(IShokoSeries, IUser, CancellationToken)` | User-facing series action |
| `IExecutableEpisodeSystemAction` | `Execute(IShokoEpisode, CancellationToken)` | Admin-only episode action |
| `IExecutableEpisodeUserAction` | `Execute(IShokoEpisode, IUser, CancellationToken)` | User-facing episode action |

The interface an action implements is a self-documenting declaration of what it
needs — no guesswork about whether `Execute` will receive a series, a user, or
neither.

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

The permission level (admin-only vs. any authenticated user) is encoded in
which sub-interface you implement:

- **`*SystemAction`** — requires admin privileges. Only users with `IsAdmin = 1`
  can list or invoke this action.
- **`*UserAction`** — any authenticated user can list and invoke this action.

See `ActionScope.md` for details on how `System` and `User` flags work
under the hood.

## Execution

Actions always run through the job queue. The API schedules execution via
`ScheduleExecuteOf*` methods — there is no direct/immediate execution path
exposed to API consumers. This ensures progress/status is visible through the
standard queue UI and error logging.
