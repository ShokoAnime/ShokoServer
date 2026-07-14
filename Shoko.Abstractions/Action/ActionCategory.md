# ActionCategory — Design Overview

`ActionCategory` is a hard-validated enum (`: byte`) with fixed members.
Plugins cannot register new categories at runtime — adding a genuinely new
category requires a PR against core to extend the enum.

## Members

| Member | Value | Description |
|--------|-------|-------------|
| `Import` | `0x01` | File import and scanning actions. |
| `AniDB` | `0x21` | AniDB metadata and synchronization actions. |
| `TMDB` | `0x22` | TMDB metadata and synchronization actions. |
| `AniList` | `0x23` | AniList metadata and synchronization actions. |
| `Sync` | `0x31` | Data synchronization actions across providers. |
| `Images` | `0x71` | Image download and management actions. |
| `Maintenance` | `0xF1` | System maintenance actions. |
| `Mischievous` | `0xF2` | Uncategorized or miscellaneous actions. |
| `Destructive` | `0xFE` | Destructive operations such as purging data. |
| `PluginInferred` | `0xFF` | Category label is inferred from the owning plugin's name. |

## Value spacing

The hex values are intentionally spaced into ranges to allow future additions
without renumbering:

- `0x01–0x3F` — provider-specific categories (Import, AniDB, TMDB, AniList, Sync)
- `0x71–0x7F` — system categories (Images)
- `0xF1–0xFF` — special categories (Maintenance, Mischievous, Destructive,
  PluginInferred)

## `Mischievous` vs `PluginInferred`

- **`Mischievous`** is the default fallback. An action that doesn't explicitly
  set a category lands here. Its label is "Mischievous", owned centrally by
  core.
- **`PluginInferred`** is an opt-in choice. When an action uses this category,
  `ActionService.AddParts` sets `ExecutableActionInfo.CategoryName` to the
  owning plugin's display name instead of the enum member name.

  This means `PluginInferred` effectively delegates the category label itself to
  the plugin — distinct from the RFC's original `Generic` proposal, which
  specified a fixed core-owned label. `Mischievous` exists as the true
  core-owned fallback that aligns with the original RFC intent.

## Category display names

Each enum member's display name is its own `ToString()` value, except for
`PluginInferred`, where the display name is the plugin's name. The
`ExecutableActionInfo.CategoryName` field carries the resolved display label
for API consumers.
