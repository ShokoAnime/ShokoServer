# Video Services

This folder holds the services a plugin **calls** to work with video: find
files, hash them, match them to episodes, move them on disk, review duplicate
releases, and see what the streaming endpoints are doing.

None of these seven interfaces is an extension point. You never implement one;
you resolve it from DI and call it. The pluggable parts these services drive
(hash providers, release providers, relocation providers, ignore rules, stream
transforms, playback observers) live in sibling folders, each with its own
README.

| Interface | Use it to | You |
|---|---|---|
| `IVideoService` | Look up videos, files and managed folders, scan, delete, read media info, subscribe to file events | consume |
| `IVideoHashingService` | Hash a file, inspect and configure hash providers | consume |
| `IVideoReleaseService` | Read, search, save and clear the episode mapping for a file | consume |
| `IVideoRelocationService` | Move and rename files, now or through the queue | consume |
| `IRelocationPresetManager` | Manage the named relocation presets and the import toggles | consume |
| `IReleaseManagementService` | Review redundant releases per series and queue their deletion | consume |
| `IVideoStreamPipelineService` | Inspect and configure stream transforms and playback observers | consume |

---

## Getting hold of one

Every service here is registered as a singleton in the core container before
plugins are constructed, so plain constructor injection works everywhere: in a
service you register yourself, in a hosted service, in a queue job, in a
controller. The one place it does *not* work is the class implementing
`IPlugin`, which is built without DI during the plugin scan and must have a
public parameterless constructor; see
[the plugin overview](../../README.md#iplugin-needs-a-public-parameterless-constructor).

Subscribing to an event means holding the subscription for the life of the
process and dropping it on shutdown, so a hosted service is the natural home:

```csharp
// Registered from your plugin's RegisterServices with
// services.AddHostedService<UnmatchedFileLogger>().
public sealed class UnmatchedFileLogger(
    IVideoReleaseService releaseService,
    ILogger<UnmatchedFileLogger> logger
) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        releaseService.SearchCompleted += OnSearchCompleted;
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        releaseService.SearchCompleted -= OnSearchCompleted;
        return Task.CompletedTask;
    }

    private void OnSearchCompleted(object? sender, VideoReleaseSearchCompletedEventArgs e)
    {
        if (e.ReleaseInfo is null && e.Exception is null)
            logger.LogInformation("Video {VideoID} was not matched by any provider", e.Video.ID);
    }
}
```

`SearchCompleted` is the event to use for "was this file matched". Checking the
current release from `VideoFileHashed` looks equivalent and is not: that event
fires before the release search has even been scheduled, so every new file
looks unmatched there.

`IVideoRelocationService` and `IRelocationPresetManager` are two faces of the
same object: the core registers one `VideoRelocationService` singleton and
resolves both interfaces from it. Inject whichever half you need, or both, and
you get the same instance.

## What each service collects

Each service is handed, once during start-up, everything `PluginManager`
discovered through `GetExports<T>()`. There is nothing to call; implement the
extension point and discovery does the rest.

| Service | Collects | Written up in |
|---|---|---|
| `IVideoService` | `IManagedFolderIgnoreRule` | [`../README.md`](../README.md) |
| `IVideoHashingService` | `IHashProvider` | [`../Hashing/README.md`](../Hashing/README.md) |
| `IVideoReleaseService` | `IReleaseInfoProvider` | [`../Release/README.md`](../Release/README.md) |
| `IVideoRelocationService` | `IRelocationProvider` | [`../Relocation/README.md`](../Relocation/README.md) |
| `IVideoStreamPipelineService` | `IVideoStreamTransform`, `IPlaybackObserver` | [`../Streaming/README.md`](../Streaming/README.md) |

Implementations of those contracts almost never need a DI registration of their
own: register the concrete type as a singleton only when your own code has to
reach the same instance the core holds, and never register it under its
interface. The reasoning for all three branches is in [Contracts the server
discovers for you](../../README.md#contracts-the-server-discovers-for-you).

---

## `IVideoService`

The front door to files, and the one service worth injecting even if you touch
nothing else here.

### Videos and video files are different things

- **`IVideo`** is the unique content, keyed by ED2K hash plus size. It has
  hashes, media info and an ID. It has no path.
- **`IVideoFile`** is one *location* of that content: a managed folder plus a
  relative path, with the absolute path computed at runtime.

The same file copied into two managed folders is one `IVideo` with two
`IVideoFile`s. Methods are named for whichever of the two they deal in, and
`DeleteVideo` (all locations) is not `DeleteVideoFile` (one location).

### Looking things up

| Call | Notes |
|---|---|
| `GetVideoByID`, `GetAllVideos` | Straight lookups over the video records |
| `GetVideoByHash(hash, algorithm)` | Returns a video **only if exactly one** matches; ambiguity gives `null`, not a first hit |
| `GetVideoByHashAndSize(hash, size, algorithm)` | Same, narrowed by file size. Prefer this one |
| `GetAllVideoByHash(hash, algorithm[, metadata])` | Every match, for when you want to handle ambiguity yourself |
| `GetVideoFileByID`, `GetAllVideoFiles` | Straight lookups over the location records |
| `GetVideoFileByAbsolutePath` / `GetVideoFilesByAbsolutePath` | Compares the full path of every known location, so it is linear in collection size. Keep it out of tight loops |
| `GetVideoFileByRelativePath(path, folder?)` | Without a folder the relative path has to be unique on its own |
| `GetVideoFilesInManagedFolder(folder, relativePath?)` | Prefix match inside one folder; pass `null` for the whole folder |

The `algorithm` parameter defaults to `"ED2K"` and matches whatever hash types
the enabled providers have stored (`CRC32`, `MD5`, `SHA1`, and anything a plugin
provider contributes).

### Managed folders

`GetAllManagedFolders`, `GetManagedFolderByID` and `GetManagedFolderByPath`
read; `AddManagedFolder(ManagedFolderData)`,
`UpdateManagedFolder(folder, ManagedFolderUpdateData)` and
`RemoveManagedFolder(folder, keepRecords, skipEvents)` write.
`ManagedFolderUpdateData` is a patch: every property is nullable and only the
non-null ones are applied. `keepRecords: true` on removal drops the folder row
but leaves the video and location rows alone, which is what a migration to a new
path wants.

### Scanning

```csharp
// Runs here and now, on your thread, and returns when the walk is done.
await videoService.ScanManagedFolder(folder, relativePath: "Winter 2026", onlyNewFiles: true);

// Queues the same work and returns as soon as the job is registered.
await videoService.ScheduleScanForManagedFolder(folder, onlyNewFiles: true);
await videoService.ScheduleScanForManagedFolders(onlyDropSources: true);
```

`ScanManagedFolder` walks the folder inline; the `Schedule…` pair hands the walk
to the queue and is what you almost always want from a plugin. Either way the
files found are pushed through the hashing and release pipeline, not processed
by the scan itself.

`NotifyVideoFileChangeDetected` is the "something happened at this path" hook,
for a plugin that has its own watcher or has just written a file. It throws
`InvalidOperationException` when the path falls outside every managed folder,
and it quietly forwards to `ScanManagedFolder` when the path turns out to be a
directory, so it is safe to point at either.

`MaxAutoScanAttemptsPerVideo` caps how many times a file that keeps failing is
retried automatically (`0` disables auto-scanning, values outside `0`–`100`
throw `ArgumentOutOfRangeException`). The `forceScan` flag on the scan and
notify methods is what gets you past that cap for one specific file.

### Deleting

```csharp
await videoService.DeleteVideoFile(file, removeFile: true);   // this location, and the bytes
await videoService.DeleteVideo(video, removeFiles: false);    // every location, records only
```

Pass the flag explicitly. `DeleteVideoFile` declares `removeFile = false` while
`DeleteVideoFiles`, `DeleteVideo` and `DeleteVideos` all declare their
equivalent as `true`, so the defaults of the four methods do not agree and
relying on them is a good way to delete more than you meant to.
`removeFolders` (default `true`) cleans up parent directories left empty
afterwards.

### Events

`IVideoService` is the aggregation point. `VideoFileHashed` is forwarded from
`IVideoHashingService`, `VideoFileRelocated` from `IVideoRelocationService`, and
the three `ManagedFolder*` events from the folder repository, so a plugin that
just wants to know what is happening to files can subscribe here and ignore the
other services entirely.

| Event | Fires when |
|---|---|
| `VideoFileDetected` | A scan or a watched folder finds a file Shoko has no location record for yet. Nothing has been done with it, so there is no `IVideo` to speak of. It is not a one-time "first seen": it is raised again on every scan until the file has been hashed and its location saved, and a folder scan raises it from several threads at once, so a handler must be thread-safe and idempotent |
| `VideoFileHashed` | Hashing finished and the records are in the database. The file is ready to be matched |
| `VideoFileRelocated` | A file was moved or renamed |
| `VideoFileDeleted` | A file was removed from Shoko |
| `ManagedFolderAdded` / `ManagedFolderUpdated` / `ManagedFolderRemoved` | A managed folder changed |

Every file event carries both `RelativePath`, from the managed folder's root, and
`Path`, the absolute path as it was when the event was raised.

The service is a process-wide singleton, so unsubscribe when your plugin shuts
down rather than leaving handlers attached to it.

### Odds and ends

`ReadMediaInfoFromPath` and `ConvertJsonToMediaInfo` both return `null` rather
than throwing on input they cannot parse.

`IsAllowedVideoExtension(fileName)` is a suffix test against the configured
extension list. `VideoExtensions` exposes that list, but note its setter writes
to the in-memory settings object **without** saving them, unlike
`MaxAutoScanAttemptsPerVideo` which persists immediately. Treat the setter as a
runtime override, not a configuration change.

---

## `IVideoHashingService`

Hashing a file means running every enabled hash provider over it and storing the
digests on the video. The service owns that run, the provider list and the
per-hash-type enablement.

### Hashing a file

```csharp
// Inline: returns when the file has been hashed and saved.
var result = await hashingService.GetHashesForPath(path, useExistingHashes: true);
var (video, file, hashes) = result;   // HashingResult deconstructs

// Or hand it to the queue and move on.
await hashingService.ScheduleGetHashesForFile(file, prioritize: true);
```

`HashingResult` also carries `UsedExistingHashes`, `IsNewVideo` and `IsNewFile`,
which is how you tell "this file was already known" from "this file just
entered the collection".

Both path-based calls throw `FileNotFoundException` for a missing file (or a
symlink pointing nowhere) and `InvalidOperationException` when the path is
outside every managed folder. Hashing is not a read-only operation: it creates
or updates the `IVideo` and `IVideoFile` records, raises `VideoFileHashed`, and,
for a file that still has no release, schedules the release search afterwards
unless you pass `skipFindRelease: true`. A re-hash of an already relocated file
can schedule a relocation too.
`skipEvents: true` additionally keeps the file out of the provider-specific
post-save sync, which is what stops it from being added to a remote tracking
list such as AniDB MyList.

### Providers and hash types

`AllAvailableHashTypes` and `AllEnabledHashTypes` give the union across
providers. `GetAvailableProviders(onlyEnabled)` and the four `GetProviderInfo`
overloads (by plugin, by `Guid`, by instance, by type) return
`HashProviderInfo` objects, each carrying the provider, its plugin, its
configuration info and its `EnabledHashTypes`.

To change what is enabled, take an info object, edit it, and pass it back:

```csharp
var info = hashingService.GetProviderInfo<MyHashProvider>();
info.EnabledHashTypes.Add("SHA256");
hashingService.UpdateProviders(info);
```

Things to know before you do:

- **The info objects are copies.** Mutating one changes nothing until it goes
  through `UpdateProviders`.
- **They must come from the service.** Matching is by provider *reference*, and
  an info object naming a provider the service does not hold is skipped in
  silence.
- **A hash type has exactly one owner.** Enabling `CRC32` on two providers does
  not run both; one of them keeps it. If you want a type, check afterwards that
  you actually got it.
- **ED2K is never off.** If an update would leave nobody computing it, the core
  provider is put back in charge of it, because the rest of Shoko keys files on
  it.
- `ProvidersUpdated` fires only when something actually changed, and the change
  is persisted to the hashing configuration.

`ParallelMode` runs every provider over the file at once instead of one after
another. It trades I/O and CPU for wall-clock time and is a user-facing
performance setting, so flip it only if the user asked you to.

---

## `IVideoReleaseService`

The consuming half of the release pipeline. Writing a provider is
[`../Release/README.md`](../Release/README.md); this is everything else.

### Reading

| Call | Returns |
|---|---|
| `GetCurrentReleaseForVideo(video)` | The stored release for the file, or `null` if it is unrecognised |
| `GetAllReleases(providerNames?)` | Every stored release, optionally filtered |
| `GetReleaseMatchAttemptsForVideo(video)` | Every match attempt recorded for the file |
| `GetStoredReleaseProviderNames()` | Every provider name that appears on a stored release, including names of providers no longer installed |
| `AutoMatchEnabled` | Whether any provider is enabled for automatic matching |

The filter on `GetAllReleases` takes provider names, with a leading `!` to
exclude: `["AniDB"]` keeps only AniDB releases, `["!AniDB"]` keeps everything
else. Several names in the include list are an **and**, not an or: a release has
to carry all of them, which matters because a merged release stores its
providers as a `+`-joined name.

### Searching and saving

```csharp
// Queue the whole provider chain for this file, as the import pipeline does.
await releaseService.ScheduleFindReleaseForVideo(video, force: true);

// Or run the chain here and now, without committing anything.
var candidate = await releaseService.FindReleaseForVideo(video, saveRelease: false, isAutomatic: false);
if (candidate is not null)
    await releaseService.SaveReleaseForVideo(video, candidate);
```

`ScheduleFindReleaseForVideo` records a match attempt and builds a job chain,
one entry per enabled provider plus `FinalizeReleaseSearchJob`, then enqueues
it. Without `force: true` it skips files that already have a release. Note it
still schedules relocation when `relocateFiles` is left at its default, even
when it skips the search.

`FindReleaseForVideo` runs the providers inline, in priority order, and returns
the first valid result. The overload taking an explicit
`IEnumerable<ReleaseProviderInfo>` lets you run one provider, or your own
ordering, which is the usual way to offer a user "search again with X".
`saveRelease: false` gives you the result to show before committing, and
`isAutomatic: false` tells providers this is a user-initiated search, which some
of them treat differently. Pass `saveRelease` explicitly either way: the two
overloads declare opposite defaults for it, `true` without a provider list and
`false` with one.

`SaveReleaseForVideo` overwrites whatever release the file had. The overload
taking a `ReleaseInfo` defaults `providerName` to `"User"`, which is what a
manual link should stay as. It throws `InvalidOperationException` if the release
carries no usable cross-references, so build those with the helpers described in
the release README rather than by hand.

### Clearing

`ClearReleaseForVideo(video)` unlinks one file. `RemoveRelease(release)` drops a
specific stored release. `PurgeUsedReleases` and `PurgeUnusedReleases` are the
bulk hammers, optionally scoped to provider names; "unused" means stored
releases no longer linked to any video. All of them take `skipEvents`, which
suppresses the post-clear sync with remote tracking lists.

### Rescans

`TryScheduleRescanForVideo(video, existingRelease)` is the manual trigger for
the same logic the recurring sweep uses: it asks every provider that took part
in the last attempt for a `GetRescanDelay`, and queues a fresh chain of the ones
whose delay has elapsed. It returns `false` when nobody wanted to rescan, and it
refuses outright when the stored release has `PreventRescan` set.

### Provider info

`GetAvailableProviders(onlyEnabled)` and the four `GetProviderInfo` overloads
mirror the hashing service, and so does the read-edit-write cycle through
`UpdateProviders`. The difference is what is editable: `Enabled`, and `Priority`
as a **position**, not a weight. Setting `Priority = 0` moves that provider to
the front of the order and pushes the rest down; a negative value parks it at
the end. The same caveats apply: the info objects are copies, they have to come
from the service, and `ProvidersUpdated` only fires on a real change.

### Events

`ReleaseSaved`, `ReleaseDeleted` and `SearchCompleted`. `SearchCompleted` fires
once per completed search, whether or not anything was found, and carries the
attempted providers, the selected one, and any exception. It is the right place
for "the pipeline gave up on this file" logic.

> The XML docs on this interface still describe a parallel mode that ran every
> provider at once. That mode was removed; the search is sequential, and there
> is no property to switch it.

---

## `IVideoRelocationService`

Moving and renaming files. Writing a relocation provider is
[`../Relocation/README.md`](../Relocation/README.md).

### Ways to ask

```csharp
// Queued, default preset, one file or every location of a video.
await relocationService.ScheduleAutoRelocationForVideoFile(file);
await relocationService.ScheduleAutoRelocationForVideo(video);

// The same, chained to run right after the job you are currently inside.
await relocationService.ChainAutoRelocationForVideo(video, cancellationToken);

// Inline, with full control.
var response = await relocationService.AutoRelocateFile(file, new AutoRelocateRequest
{
    Preset = presetManager.GetStoredPreset("Anime"),
    Preview = true,
});

// Inline, no preset involved: you name the destination.
var moved = await relocationService.DirectlyRelocateFile(file, new DirectlyRelocateRequest
{
    ManagedFolder = destination,
    RelativePath = "Anime/Some Show/Some Show - 01.mkv",
});
```

The `Chain…` pair is for use from inside a running queue job, where you want the
relocation to happen immediately after you return rather than at the back of the
queue.

### Reading the response

`RelocationResponse` reports failure instead of throwing, so check `Success`
before anything else. On success `ManagedFolder` and `RelativePath` are
non-null (and `AbsolutePath` is computed from them), plus `Moved` and `Renamed`
say what actually happened. On failure `Error` holds the message and any
exception. `AutoRelocateFile` retries a transient failure a few times on its own
before it gives up.

Two requests come back as failures rather than doing something surprising:

- `Preview: true` without a `Preset`. Previewing needs to know which preset to
  resolve the path with, so supply one.
- `Move: false` with `Rename: false`. There is nothing left to do.

`AutoRelocateFile(file)` with no request at all is not the same as passing
`new AutoRelocateRequest()`: with no request, `Move`, `Rename`,
`DeleteEmptyDirectories` and `AllowRelocationInsideDestination` are taken from
the user's import settings. Pass a request and you get the record's own
defaults, which enable moving and renaming regardless of what the user
configured. `CancelIfRunning` decides what happens when the same file is already
being relocated: queue behind it (the default) or give up.

### Utilities

`GetFirstDestinationWithSpace(context)`, `ManagedFolderHasSpace(folder, file)`
and `GetExistingSeriesLocationWithSpace(context)` are the helpers the shipped
relocation providers use to pick a destination, exposed so a provider or a
plugin can make the same choices. The last one only considers folders marked
`Destination` or `Excluded`, never a pure drop source.

### Events

`FileRelocated` after a successful move or rename (also re-broadcast as
`IVideoService.VideoFileRelocated`), and `ProvidersUpdated` when the provider
list or one of the import toggles changes.

---

## `IRelocationPresetManager`

The same object as `IVideoRelocationService`, split out because it does an
unrelated job: it manages the named presets that relocation runs against.

A preset is a provider plus a stored, validated configuration plus a friendly
name. One of them is the default, and that is the one every
`ScheduleAutoRelocation…` call uses.

### Reading

`GetDefaultPreset()`, `GetStoredPreset(Guid)`, `GetStoredPreset(string name)`,
and `GetStoredPresets` by provider ID, provider instance, plugin, or all of
them. Every one hands back a `RelocationPresetInfo`, which implements
`IStoredRelocationPreset` and adds two helpers: `LoadConfiguration()` and
`SaveConfiguration(json)`. Its `ProviderInfo` is `null` when the preset's
provider is not currently loaded, which is the normal state for a preset left
behind by an uninstalled plugin.

> `GetStoredPresets(available:)` filters on exactly that: whether the preset's
> provider info resolved. As implemented, the flag selects presets **without** a
> loaded provider when you pass `true`, which is the opposite of what the name
> suggests. Filter on `ProviderInfo is not null` yourself if it matters.

### Writing

```csharp
var preset = presetManager.StorePreset(myProvider, "Anime", myConfiguration, setDefault: true);

preset.Name = "Anime (movies)";
presetManager.UpdatePreset(preset);

presetManager.DeletePreset(preset);
```

- **Names are unique, and `StorePreset` will not tell you it renamed you.** A
  clash appends ` (copy)`, then ` (copy #2)` and so on. Read the name back off
  the returned info rather than assuming you got the one you asked for.
  `UpdatePreset` does the same on a rename.
- **The first preset stored becomes the default**, `setDefault` or not, because
  a collection with presets but no default cannot relocate anything.
- **Configuration is validated on the way in.** A configuration of the wrong
  type is an `InvalidOperationException`, one that fails validation is a
  `ConfigurationValidationException`, and passing a configuration to a provider
  that does not take one is also an `InvalidOperationException`. A provider that
  takes a configuration and gets none is given a fresh default instance.
- **`UpdatePreset` returns `false` when nothing changed.** It still promotes the
  preset to default if you asked for that, so the return value means "the name
  or configuration changed", not "the call did something".
- **The default cannot be deleted.** `DeletePreset` throws
  `InvalidOperationException` for the default preset and for one that is not in
  the database. Promote another preset first.

Writes raise `PresetStored`, `PresetUpdated` and `PresetDeleted`; a change of
default additionally raises `ProvidersUpdated` on the relocation half.

### The import toggles

`RenameOnImport`, `MoveOnImport` and `AllowRelocationInsideDestinationOnImport`
are the user's global relocation settings, readable and writable here. They
persist immediately and raise `ProvidersUpdated`. They are the defaults that
`AutoRelocateFile` falls back on when called with no request, so changing one
changes what every import does from then on. Read them freely; write them only
when the user asked for it.

---

## `IReleaseManagementService`

The API behind the release management screen: for each series, group the files
into release candidates, rank them, and work out which files a keep-the-best
policy would delete. Nothing here deletes anything by itself except
`QueueDeletion`.

```csharp
// Series that need review, one page at a time, sorted by title.
var (page, total) = managementService.GetSeriesWithCandidates(onlyFinishedSeries: true, onlyWithRedundant: true);

// Everything for one series, including the Mix & Match data source.
var candidates = managementService.GetSeriesCandidates(series, includeOverrides: true);

// What would go, if we kept the rank-1 candidate.
if (managementService.GetSeriesDeletionPreview(series) is { } preview)
{
    logger.LogInformation("{Count} files, {Size} bytes", preview.TotalFilesToDelete, preview.TotalSizeToDelete);
    await managementService.QueueDeletion([.. preview.Files.Select(f => f.PlaceID)]);
}
```

- **`GetSeriesCandidates` returns `null`** for a series with nothing to decide:
  unknown, fewer than two distinct candidates, and no episode covered
  ambiguously. That is the common case, not an error.
- **`preferredCandidateKey`** recomputes redundancy as if the user had chosen
  that candidate instead of the top-ranked one. Display order and rank numbers
  do not move, so do not use it to reorder the list.
- **`GetOverrideDeletionPreview`** is the manual path, one file per episode. The
  selection has to cover every episode that has at least one file and every ID
  has to belong to the series, or it throws `ArgumentException`.
- **Deletion is by location ID.** `QueueDeletion` takes `PlaceID` values (an
  `IVideoFile.ID`), queues a background job, and returns as soon as the job is
  registered. It accepts any subset, not only IDs that came out of a preview.
- **Sum with care.** Use `SeriesWithCandidates.FilesToAutoDeleteCount` rather
  than adding up `ReleaseCandidate.RedundantFileCount`, which double-counts
  locations shared between gap-filling candidates.
- **Nothing here is permission-filtered.** The service answers as the server,
  not as a user. If your caller is user-scoped, filter on your side.

---

## `IVideoStreamPipelineService`

Mostly a core service: the streaming endpoints use it to pick a transform for a
request and to fan playback progress out to the observers. Both extension points
it manages, and everything about writing one, are in
[`../Streaming/README.md`](../Streaming/README.md).

Two things here are still useful from a plugin.

**Reading and configuring the pipeline.** `GetAvailableTransforms`,
`GetApplicableTransforms(video, context)`, `GetTransformInfo`,
`SelectTransform(video, context, explicitTransformId?)` and the observer
equivalents `GetAvailableObservers` / `GetObserverInfo` let you show or choose
what would handle a given video; `UpdateTransforms` and `UpdateObservers` write
back the enabled flag and, for transforms, the priority used during automatic
selection. `SelectTransform` returns `null` when nothing applies, which means
the stream is served as a raw passthrough.

**Reporting playback from your own endpoint.** If your plugin serves video
itself rather than through `/api/v3/File/{fileID}/Stream*`, call
`NotifyPlaybackProgress(context)` so enabled observers (scrobbling, for
instance) still see the playback. The observers run one after another and are
awaited, with no cancellation token, so a slow observer holds up the others and
your response until it returns. A failing one is logged and cannot break your
response.

`TransformsUpdated` and `ObserversUpdated` fire when the enabled or priority
state changes, so a cached view of the pipeline can refresh itself.
