# Managed Folder Ignore Rules

This folder holds the video-side abstractions: the managed folder and video
file types, the media info interfaces, and the video events. The one pluggable
contract that lives directly in it is `IManagedFolderIgnoreRule`, which decides
**what the library scanner is allowed to look at**.

The other pluggable video contracts have their own documents:
[hashing](Hashing/README.md), [releases](Release/README.md),
[relocation](Relocation/README.md) and [streaming](Streaming/README.md).

---

## Which way round the boolean goes

This is the one thing to get right, so it goes first.

```csharp
bool ShouldIgnore(IManagedFolder folder, FileSystemInfo fileSystemInfo);
```

| Return | Meaning |
|---|---|
| `true` | **Ignore this entry.** It is dropped from the scan. |
| `false` | Nothing to say about it. It stays in the scan. |

`false` is the neutral answer, not an endorsement. The scanner keeps an entry
only when **every** rule says `false`:

```csharp
// Shoko.Server/Services/VideoService.cs
return !_ignoreRules.Any(rule => rule.ShouldIgnore(folder, info));
```

So rules are OR'd together, and there is no way to un-ignore something another
rule ignored. A rule that answers `true` too eagerly makes files invisible to
Shoko with no error and no log line, which is why the direction is worth
double-checking before you ship.

---

## When it is consulted

Ignore rules run during a **managed folder scan**: `ScanFolderJob` calls
`IVideoService.ScanManagedFolder`, which walks the folder tree and asks every
rule about every entry it meets.

```
ScanFolderJob
   └─ ScanManagedFolder
        └─ GetFilesInImportFolder        ← ignore rules run here
             └─ surviving files are extension-checked, then hashed
```

Three details follow from where the call sits in that walk:

- **Directories are passed in too,** not just files. Answering `true` for a
  directory prunes it *and everything under it*: the walker never descends into
  a directory a rule rejected. This is the cheap way to exclude a whole tree.
  It is also why `fileSystemInfo` is a `FileSystemInfo` rather than a
  `FileInfo`.
- **It runs before the video-extension check,** so you see every file in the
  folder: subtitle files, artwork, `.nfo` sidecars, partial downloads. Do not
  assume an entry is a video.
- **It runs on the whole tree, every scan.** One rule call per entry across the
  entire library, on every sweep and every full scan.

### What it does not cover

The live file system watcher (`RecoveringFileSystemWatcher`) **does not consult
ignore rules**. A file that appears while the server is running is filtered
only by the `Import.ExcludeExpressions` regexes from settings and by the
configured video extensions. A rule is therefore an authority over scans, not
over the library as a whole: something your rule excludes can still be picked
up when it is created live. If a plugin needs both, it has to cover the watcher
path by other means.

Ignore rules also never remove anything already imported. They only control
what a scan finds.

---

## Implementing a rule

Both members are required and neither has a default.

```csharp
public class SampleFileIgnoreRule(ConfigurationProvider<MyPluginConfiguration> configurationProvider) : IManagedFolderIgnoreRule
{
    public string Name => "Ignore Sample Files";

    public bool ShouldIgnore(IManagedFolder folder, FileSystemInfo fileSystemInfo)
    {
        // Prune a whole tree by name, without descending into it.
        if (fileSystemInfo is DirectoryInfo directory)
            return directory.Name.Equals("Extras", StringComparison.OrdinalIgnoreCase);

        // Configuration is read per call: the rule set itself is fixed at
        // startup, so anything configurable has to be re-read here to stay live.
        if (!configurationProvider.Load().SkipSamples)
            return false;

        // Cheap, string-only checks. No stat, no database, no network.
        return fileSystemInfo.Name.Contains("-sample.", StringComparison.OrdinalIgnoreCase);
    }
}
```

The built-in `CoreIgnoreRule` is the same shape: it applies the user's
`Import.ExcludeExpressions` regexes to files **and** directories (a matching
directory is pruned with everything under it, and patterns written with a
trailing separator also match the directory they name), and reads them from
settings on every call so a settings change takes effect on the next scan.

### Registering

Most rules need no DI registration at all:
`PluginManager.GetExports<IManagedFolderIgnoreRule>()` finds the type,
constructs it with constructor injection, and `IVideoService` holds that
instance for the life of the process. Register the **concrete type** as a
singleton only if your own code resolves the rule, and never register it under
the `IManagedFolderIgnoreRule` interface. The full rule is in the
[abstractions README](../README.md).

Registered rules are readable back from `IVideoService.IgnoreRules`, which is
mainly useful for diagnostics.

---

## Mistakes that are easy to make

- **Inverting the boolean.** `true` ignores. A rule written as "does this file
  look like something we want?" excludes exactly the files it meant to keep.
- **Forgetting that directories arrive too.** A rule matching on file
  extensions with `fileSystemInfo.Name.EndsWith(".mkv")` and returning the
  negation will reject every directory in the library, pruning the entire tree
  from the first level down.
- **Ignoring everything.** `ScanManagedFolder` treats a folder that yields zero
  files as temporarily unavailable and aborts the scan with a log line about
  the folder being unreachable, so an over-broad rule looks like a mounting
  problem rather than a filter.
- **Throwing.** An exception out of `ShouldIgnore` is swallowed by the parallel
  walker, which logs a warning and treats the *containing directory* as a dead
  end. One bad entry silently costs you a whole subtree, so guard your own
  parsing rather than letting it throw.
- **Doing real work per call.** The rule is invoked once per file and once per
  directory across the whole library, from a parallel walk. A database lookup,
  a `File.Exists`, or reading `fileSystemInfo.Length` (which costs a stat the
  walker has not already paid for) turns a scan into an hours-long job.
- **Keeping mutable state.** Calls come in concurrently from multiple walker
  threads, and there is no per-scan begin or end callback to reset anything
  against. Keep the rule pure, or make its state thread-safe.
- **Expecting the rule set to be reloadable.** The rules are taken once, at
  startup. Anything the user should be able to change has to be read inside
  `ShouldIgnore`.

---

## File groups and kept locations

A video can have more than one location (`IVideoFile`). Locations that are the
same file on disk, hard links of each other and symbolic links with the file
they point to, are **one file group** of the video, not duplicates, while
`IVideoService.GroupLinkedFiles` is on (the default). Turned off, every
location is a group of its own, as before 6.0.

Which locations are one group is **stored**, as last seen on disk: each
location names its group's primary location (`IVideoFile.PrimaryFileID`), the file itself rather than a link
to it where it is in the collection, and records whether it is a symbolic link
(`IVideoFile.IsSymbolicLink`) and where it leads (`SymbolicLinkTarget`). The
core updates them when a location is found, hashed or moved, and during folder
scans: a link added before the file it points to hands its group to the file
once that is added, and a removed primary hands its group to the next location.
Reading groups reads nothing from disk; a link made or removed outside Shoko
shows after the next scan of its folder.

`IVideoService.GetVideoFileGroups` reads a video's groups (`VideoFileGroup`):
each with its locations (the primary first), whether any of them is kept, and
whether it is **preferred**, a group to keep:

- every group with a kept location (`IVideoFile.IsKept`, set with
  `SetVideoFileKept`, announced by `VideoFileKeptChanged`), when there is one;
- otherwise the one group holding the video's first available location.

Every other group is a duplicate. `GetVideosWithDuplicates` lists the videos
that have one, for the whole collection or for one series, and the core's own
duplicate removal (on import, and the APIv3 duplicate routes) only ever removes
whole groups that are not preferred, so a link is never left pointing at a
removed file. Passing `ignoreKept: true` treats no location as kept.
