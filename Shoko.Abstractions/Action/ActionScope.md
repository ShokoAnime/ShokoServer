# ActionScope — Design Overview

`ActionScope` is a `[Flags]` enum that encodes two orthogonal concerns in a
single value:

1. **Permission level** — whether an action requires admin privileges or is open
   to any authenticated user.
2. **Entity scope** — the entity type the action operates on (Global, Group,
   Series, or Episode).

## Base flags

| Flag | Bit | Meaning |
|------|-----|---------|
| `System` | `1 << 0` | Admin-only action. Requires `[Authorize("admin")]`. |
| `User` | `1 << 1` | Any authenticated user may invoke. |
| `Global` | `1 << 2` | Not tied to any particular entity. |
| `Group` | `1 << 3` | Scoped to a group. |
| `Series` | `1 << 4` | Scoped to a series. |
| `Episode` | `1 << 5` | Scoped to an episode. |

## Combined (named) values

Every value stored in an action's `ExecutableActionInfo.Scopes` set is always a
combination of exactly one permission flag (`System` or `User`) plus exactly one
entity flag (`Global`, `Group`, `Series`, or `Episode`). The named combined
values exist for client convenience:

| Named value | Permission | Entity |
|-------------|------------|--------|
| `SystemAndGlobal` | System | Global |
| `UserAndGlobal` | User | Global |
| `SystemAndGroup` | System | Group |
| `UserAndGroup` | User | Group |
| `SystemAndSeries` | System | Series |
| `UserAndSeries` | User | Series |
| `SystemAndEpisode` | System | Episode |
| `UserAndEpisode` | User | Episode |

**Important:** bare flags like bare `Global` or bare `System` are never stored in
an action's scope set. They exist only as filter values for querying.

## Filtering

Because every stored value is a combination, `HasFlag`-based filtering allows
querying by either axis independently:

- `GetActions(scopes: [ActionScope.System])` — returns all admin actions across
  all entity levels.
- `GetActions(scopes: [ActionScope.Global])` — returns all global actions
  (both admin and user variants).
- `GetActions(scopes: [ActionScope.SystemAndGroup])` — returns actions that
  require admin privileges and target a group.

This is the approach used in `ActionService.GetActions` and the listing
endpoints in the API controller.

## Why a single enum instead of separate Scope + Permission enums?

The RFC's revised Gap 8 specifies a separate `ActionPermission` enum orthogonal
to `ActionScope`. Keeping both on a single `[Flags]` enum simplifies the
`ExecutableActionInfo.Scopes` set: a multi-scope action stores values like
`{ SystemAndGlobal, UserAndGlobal }` — both carry the entity scope (`Global`)
alongside the permission level. A listing endpoint can filter by either axis
with a single `HasFlag` call, without joining across two separate enums.
