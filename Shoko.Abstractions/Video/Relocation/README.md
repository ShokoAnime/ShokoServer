# Relocation Providers

This folder defines the public API surface for deciding **where a video file
should live and what it should be called**. Providers are pluggable: any plugin
can register one or more `IRelocationProvider` implementations, historically
called renamers, alongside the WebAOM renamer that ships in core.

A provider never touches the file system. It is handed everything known about
one file and answers with a destination; `IVideoRelocationService` does the
rest, including creating the destination tree, handling collisions, updating
the `VideoLocal_Place` record and firing `FileRelocated`.

---

## How a provider gets picked

There is no priority list and no enabled flag. Selection is by **preset**:

```
IRelocationPreset  =  ProviderID (Guid)  +  packed configuration (byte[])
```

`IStoredRelocationPreset` adds an ID, a display name and an `IsDefault` flag,
and is managed through `IRelocationPresetManager` (`StorePreset`,
`GetStoredPresets`, `GetDefaultPreset`, `UpdatePreset`, `DeletePreset`). An
`AutoRelocateRequest` with no preset falls back to the default preset; if there
is no default, relocation fails with an error rather than picking a provider
for you.

The practical upshot is that **one provider can back many presets**, each with
its own configuration, and the user chooses per operation. A provider that
wants configuration implements `IRelocationProvider<TConfig>` where
`TConfig : IRelocationProviderConfiguration`; the preset's packed bytes are
deserialised into `TConfig` and handed to you on the `RelocationContext<TConfig>`.

### When core calls it

| Trigger | Path |
|---|---|
| A release was saved for a video, or a release search finished or was skipped | `VideoReleaseService` and `FinalizeReleaseSearchJob` call `ScheduleAutoRelocationForVideo` / `ChainAutoRelocationForVideo` → `RenameMoveFileJob` |
| An already-recognised file was force re-hashed and `RelocateOnImport` is on | `VideoHashingService` calls `ScheduleAutoRelocationForVideoFile` → `RenameMoveFileLocationJob` |
| A series or group is relocated in bulk | `RelocateSeriesFilesAction` / `RelocateGroupFilesAction` |
| A user or plugin asks directly | `IVideoRelocationService.AutoRelocateFile` |
| The WebUI previews a rename | `AutoRelocateFile` with `Preview = true` |

Auto-relocation is skipped entirely, without ever reaching a provider, when
`RelocateOnImport` is off, when every file for the video sits in an `Excluded`
folder, when no providers are registered, or when the file is unrecognised and
no registered provider sets `SupportsUnrecognized`.

---

## Drop folders

`ShokoManagedFolder.DropFolderType` is a flags enum with three meaningful
states, and it gates relocation on the **source** side:

| Value | Meaning for relocation |
|---|---|
| `Excluded` (0) | Files here are never relocated, and the folder is never offered as a destination. |
| `Source` (1) | Files here get relocated out. |
| `Destination` (2) | Offered as a destination. A file already sitting here is only relocated when `AllowRelocationInsideDestination` is set (it defaults to the `AllowRelocationInsideDestinationOnImport` setting). |
| `Both` | Both of the above. |

`RelocationContext.AvailableFolders` is the whole list of non-`Excluded`
folders, both sources and destinations, and it is the list to choose your
`ManagedFolder` from. Picking the right one is your job, but two helpers on
`IVideoRelocationService` do the common work:

- **`GetFirstDestinationWithSpace(context)`**, the first folder flagged
  `Destination` that exists and can hold the file.
- **`GetExistingSeriesLocationWithSpace(context)`**, the folder and relative
  directory where other files of the same series already live, so a new episode
  lands next to its siblings.
- **`ManagedFolderHasSpace(folder, file)`** if you want to filter yourself.

---

## Implementing a provider

```csharp
public class MyRenamer(IVideoRelocationService relocationService) : IRelocationProvider<MyRenamerConfig>
{
    public string Name => "My Renamer";

    public string Description => "Lays files out as <Series>/Season <n>/<Series> - <ep>.<ext>";

    // Defaults to false. Say true only if GetPath can cope with
    // context.Episodes and context.Series being empty.
    public bool SupportsUnrecognized => false;

    public RelocationResult GetPath(RelocationContext<MyRenamerConfig> context)
    {
        if (context.Episodes is not [var episode, ..] || context.Series is not [var series, ..])
            return RelocationResult.FromError("No episode or series linked to the file.");

        // PreferredTitle is an ITitle, and nullable, like every title in the abstractions.
        if (series.PreferredTitle?.Value is not { Length: > 0 } title)
            return RelocationResult.FromError($"No usable title for series {series.ID}.");

        var result = new RelocationResult();

        if (context.RenameEnabled)
            result.FileName = $"{title} - {episode.EpisodeNumber:D2}{Path.GetExtension(context.File.FileName)}";
        else
            result.SkipRename = true;

        if (context.MoveEnabled)
        {
            // Prefer where the series already lives, else the first destination with room.
            if (relocationService.GetExistingSeriesLocationWithSpace(context) is { } existing)
                (result.ManagedFolder, result.Path) = (existing.ManagedFolder, existing.RelativePath);
            else if (relocationService.GetFirstDestinationWithSpace(context) is { } folder)
                (result.ManagedFolder, result.Path) = (folder, Path.Combine(title, $"Season {episode.SeasonNumber ?? 1}"));
            else
                return RelocationResult.FromError("No destination folder with enough free space.");
        }
        else
        {
            result.SkipMove = true;
        }

        return result;
    }
}
```

`Name` is the only property you must supply, and `GetPath` is the only method
that does any work. Everything else has a default:

| Member | Default | Notes |
|---|---|---|
| `Description` | `null` | Shown in the UI. |
| `Version` | Assembly version | |
| `SupportsUnrecognized` | `false` | See below. |
| `SupportsIncompleteMetadata` | `false` | See below. |
| `SupportsMoving` | `true` | When `false`, every result is treated as `SkipMove`. |
| `SupportsRenaming` | `true` | When `false`, every result is treated as `SkipRename`. |
| `GetPath(RelocationContext)` | Returns a `NotImplementedException` error | Leave it alone when you implement `IRelocationProvider<TConfig>`. |

Note the shape of the configured variant: `IRelocationProvider<TConfig>.GetPath`
is a **separate method**, not an override. When your provider declares a
configuration type the service resolves and invokes the generic overload
directly, so the inherited non-generic default is never called and there is no
need to implement it.

### Registering

Most providers need no DI registration at all:
`PluginManager.GetExports<IRelocationProvider>()` finds the type, constructs it
with constructor injection, and `IVideoRelocationService` holds that instance
for the life of the process. Register the **concrete type** as a singleton only
if your own code resolves the provider, and never register it under the
`IRelocationProvider` interface. The full rule is in the
[abstractions README](../../README.md).

---

## What the two support flags actually gate

`SupportsUnrecognized` and `SupportsIncompleteMetadata` are checked by the
service *before* your `GetPath` is called, so they are guards, not hints:

- **`SupportsUnrecognized = false`** (the default) means a file with no
  cross-references never reaches you. The check happens twice: once when
  deciding whether to schedule auto-relocation at all (across every registered
  provider), and once when running the preset (against your provider alone).
  Set it to `true` only if `GetPath` genuinely handles empty `Episodes` and
  `Series`, for instance by laying unrecognised files out by filename.
- **`SupportsIncompleteMetadata = false`** (the default) means the service
  refuses when the file has cross-references but some of them do not resolve to
  a `IShokoEpisode` yet, which is the normal state while AniDB data is still
  being fetched. Set it to `true` only if a partial `Episodes` list is enough
  for you.

`SupportsMoving` and `SupportsRenaming` work on the result instead. The
service still asks you when they are `false`, but whatever you return is
treated as `SkipMove` or `SkipRename` for that half, so the file keeps its
current folder or name. They are also reported through the v3 API so a client
can grey out an option. Beyond that, honour `context.MoveEnabled` and
`context.RenameEnabled` yourself, and set `SkipMove` / `SkipRename` on the
result for anything else you decline to decide.

---

## The result

| Field | When to set it |
|---|---|
| `FileName` | The new name, no directories. Required when renaming and `SkipRename` is not set. |
| `Path` | The new directory, relative to `ManagedFolder`. Required when moving and `SkipMove` is not set. |
| `ManagedFolder` | One of `context.AvailableFolders`. Required when moving. |
| `SkipMove` / `SkipRename` | "I am deliberately not deciding this half"; the file keeps its current folder or name. |
| `Error` | Anything went wrong. Use `RelocationResult.FromError(...)`. |

The service normalises what comes back before acting on it: a directory left on
`FileName` moves into `Path` when `Path` is empty and is discarded otherwise,
alternate separators are folded to `Path.DirectorySeparatorChar`, and a leading
separator on `Path` is stripped. It then rejects the result outright if a
rename was requested and `FileName` is null or whitespace, or a move was
requested and `ManagedFolder` or `Path` is null.

Afterwards the service refuses a relative path that resolves outside the
managed folder, refuses a destination folder without enough free space (unless
the file is staying in its current folder), creates the destination tree, and
handles the case where a file already exists at the target.

---

## Mistakes that are easy to make

- **Assuming `SupportsMoving = false` stops the service asking you to move.**
  It does not: `context.MoveEnabled` can still be `true`. The flag only makes
  the service ignore the move half of whatever you return.
- **Doing work in a preview.** `AutoRelocateRequest.Preview` is not passed
  down: the context looks identical to a real run and only the service knows
  the difference. `GetPath` must be free of side effects, every time.
- **Blocking.** `GetPath` is synchronous by design. Network calls, database
  sweeps or `.Result` on a task all stall the relocation job, and there is no
  cancellation past the token on the context, which you should check yourself
  in any loop.
- **Throwing instead of returning an error.** Exceptions are caught and turned
  into a `RelocationError`, so nothing breaks, but the message a user sees is
  whatever `ex.Message` happened to say. `RelocationResult.FromError` with a
  sentence that explains the situation is much better. An
  `OperationCanceledException` is reported as a cancellation by the renamer.
- **Returning a path with directories in `FileName` and a non-empty `Path`.**
  The directories are silently dropped, which looks like the renamer ignoring
  half its own output.
- **Renaming or moving the provider class.** The provider ID is a v5 UUID over
  `"RelocationProvider={type.FullName}"` in the plugin's own ID namespace, so
  changing the namespace or the class name produces a different ID. Every
  stored preset still points at the old one and reports the provider as
  unavailable.
- **Relying on `context.Series` being non-empty for a recognised file.** It is
  built from resolved cross-references; a file linked to an anime whose series
  entry does not exist yet has cross-references and no series. That is exactly
  what `SupportsIncompleteMetadata` is about.
- **Building a path that is fine on your platform only.** Names come from
  metadata a user controls, so strip or replace characters the destination file
  system rejects rather than assuming they never appear.
