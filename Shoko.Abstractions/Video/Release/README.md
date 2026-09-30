# Release Provider Pipeline

The API for identifying which anime episodes a video file contains. Any plugin
can add `IReleaseInfoProvider` implementations to the pipeline.

---

## How the chain works

Enabled providers are sorted by their configured priority and queried one at a
time for each video file. For each provider the outcome is one of three things:

| Provider returns | `DeferToNext` | Effect |
|---|---|---|
| `null`, or a result with no cross-references | (not read) | No match; the chain advances to the next provider |
| `ReleaseInfo` | `false` (default) | Match saved; the chain stops |
| `ReleaseInfo` | `true` | Match saved as provisional, and the chain keeps running so a later provider can replace it |

If every provider in the chain returns `null`, the file is left unrecognised.
If every provider defers, the last provisional result stands.

A provider that **throws** ends the search there. `SearchCompleted` is raised
with the exception, and the rest of the chain is abandoned: later providers are
not asked, and the finalizing step below never runs, so the attempt is not
marked completed and no relocation follows. Return `null` for "no match" and
keep exceptions for genuine failures.

After the chain finishes, `FinalizeReleaseSearchJob` marks the match attempt
`IsCompleted = true` and fires `IVideoReleaseService.SearchCompleted`.

Saving a match does the follow-up work too. `SaveReleaseForVideo` queues the
fetch of every AniDB anime the release links, and the supplementary metadata
after it, so a provider must not refresh the AniDB anime itself, nor queue
anything for it: return the cross-references and let the save do the rest.

---

## Implementing a provider

```csharp
public class MyReleaseProvider : IReleaseInfoProvider
{
    public string Name => "MyProvider";

    public async Task<ReleaseInfo?> GetReleaseInfoForVideo(
        ReleaseInfoContext context, CancellationToken cancellationToken)
    {
        var (video, isAutomatic) = context;
        // Look up the file by ED2K hash or filename, return null if not found.
        var result = await LookupAsync(video.Hashes, cancellationToken);
        if (result is null) return null;

        return new ReleaseInfo
        {
            ID = result.Id,
            CrossReferences = result.Episodes
                .Select(ep => ReleaseVideoCrossReference.ForAniDB(ep.AnidbEpisodeId, ep.AnidbAnimeId))
                .ToList(),
        };
    }

    // Part of the interface, so it has to be implemented. A provider that
    // cannot look a release up by its own ID says so with NotSupportedException.
    public Task<ReleaseInfo?> GetReleaseInfoById(
        string releaseId, CancellationToken cancellationToken)
        => throw new NotSupportedException();
}
```

`GetReleaseInfoById` returns `null` for an ID it does not know. Throw
`NotSupportedException` when the provider cannot look releases up by ID at all:
the API's preview answers that with a `400` naming the provider, where any
other exception, `NotImplementedException` included, is a `500`.

`Name` and the two `GetReleaseInfo*` methods are the only members you have to
supply. `Description`, `Version` and `GetRescanDelay` all have default
implementations, with `Version` falling back to your assembly version. The core
attaches the provider name to the result on the way in, so leaving
`ReleaseInfo.ProviderName` unset is fine.

### Registering

Most providers need no DI registration at all. The core discovers the type,
constructs it with constructor injection, and `IVideoReleaseService` holds that
instance for the life of the process. Register the **concrete** type as a
singleton only when your own code has to reach the same object, and never
register it under `IReleaseInfoProvider`; see
[Contracts the server discovers for you](../../README.md#contracts-the-server-discovers-for-you).

A newly discovered provider starts **disabled** (only one named `AniDB`, the
core's, starts enabled). It is listed, but not asked about any file until a user
enables it in the release provider settings. A provider that never seems to run
is almost always this, not a registration problem.

A provider that needs user-editable settings implements
`IReleaseInfoProvider<TConfiguration>` where
`TConfiguration : IReleaseInfoProviderConfiguration`, which is what tells the
Web UI to render that configuration on the provider's own page.

---

## Cross-references and ProviderIDs

Each `ReleaseInfo.CrossReferences` entry maps a segment of the file to content.
The mapping is expressed as a string-to-string dictionary (`ProviderIDs`) so
any provider can store its own keys without breaking consumers that don't know
about them. Consumers ignore keys they don't recognise.

### Well-known keys

`CrossReferenceIDs` defines constants for the AniDB keys the server understands:

| Constant | Value | Meaning |
|---|---|---|
| `CrossReferenceIDs.AniDB_Episode` | `"AniDB_Episode"` | AniDB episode ID (integer string) |
| `CrossReferenceIDs.AniDB_Anime` | `"AniDB_Anime"` | AniDB anime ID (integer string) |

### Creating AniDB cross-references

Use `ReleaseVideoCrossReferenceExtensions.ForAniDB` (from
`Shoko.Abstractions.Extensions`):

```csharp
// Single full episode
ReleaseVideoCrossReference.ForAniDB(episodeID: 123, animeID: 456)

// File covers a range of the episode
ReleaseVideoCrossReference.ForAniDB(episodeID: 123, animeID: 456,
    percentStart: 0, percentEnd: 50)
```

`ForAniDB` is called **on the type, not on an instance**: it is a static member
of a C# 14 `extension(...)` block, a factory returning a new cross-reference.
The `this`-style extension in the next section is the older form, called on an
instance.

### Adding your own provider IDs

Define your own constants and a matching extension method:

```csharp
public static class MyProviderIDs
{
    public const string Episode = "MyProvider_Episode";
    public const string Anime   = "MyProvider_Anime";
}

public static class MyProviderCrossReferenceExtensions
{
    public static ReleaseVideoCrossReference ForMyProvider(
        this ReleaseVideoCrossReference xref, int episodeId, int animeId)
    {
        xref.ProviderIDs[MyProviderIDs.Episode] = episodeId.ToString();
        xref.ProviderIDs[MyProviderIDs.Anime]   = animeId.ToString();
        return xref;
    }
}
```

### Percentage range

`PercentageStart`/`PercentageEnd` describe what fraction of the episode this
file covers, which matters when a single file spans multiple episodes or when
multiple files together make up one episode:

- Both `null` → the file covers the whole episode (0–100)
- `PercentageStart = 0, PercentageEnd = 50` → first half of the episode
- `PercentageStart = 50, PercentageEnd = 100` → second half

---

## Provider lifecycle

A provider's job is matching, and nothing else. Saving the result, keeping
`CrossRef_File_Episode` in sync, resolving a missing anime ID from an episode
ID, running auto-management and scheduling relocation all live in
`IVideoReleaseService` and apply uniformly to every provider.

`GetRescanDelay` is the one optional hook that remains:

| Method | Called when | Typical use |
|---|---|---|
| `GetRescanDelay(existingInfo, lastAttempt)` | When deciding whether to re-queue a file whose stored info is incomplete | Return a `TimeSpan` to schedule a rescan, or `null` to opt out |

To react to a release being saved, replaced or removed, subscribe to the events
on `IVideoReleaseService`: `ReleaseSaved`, `ReleaseDeleted` and
`SearchCompleted`.

**Do not rename or move the provider class.** Its ID is derived as a v5 UUID
over `"ReleaseProvider={type.FullName}"` in the plugin's own ID namespace, so
changing the class name or its namespace produces a different ID. The user's
enabled flags and priority order are both keyed on that ID, so the renamed
provider comes back disabled and last in priority, while the old ID lingers in
the settings pointing at nothing. Hash and relocation providers derive their IDs
the same way. Treat the type's name and namespace as part of your contract.

---

## DeferToNext

Set `ReleaseInfo.DeferToNext = true` when your provider has a partial or
low-confidence result and a later, more authoritative provider might do better.
The result is saved immediately so the file isn't left unrecognised, but the
chain keeps running. If a subsequent provider saves its own result (deferred or
not), it replaces yours.

```csharp
return new ReleaseInfo
{
    // ...
    DeferToNext = true,  // provisional, let the later providers try
};
```

---

## Match attempts and rescans

`IReleaseMatchAttempt` tracks the per-file match history:

- `IsSuccessful`, `true` if any provider matched
- `IsCompleted`, `true` once the chain ran to completion (set by a non-deferred
  save or by `FinalizeReleaseSearchJob`)
- `AttemptCount`, how many times the file has been (re-)processed, starting at 1
  for the initial attempt; used to compute backoff delays
- `AttemptedProviderNames`, the ordered list of providers making up the chain
- `ProviderName`/`ProviderID`, the provider behind the winning match, if any

The `ScanForMissingReleaseInfoAction` scheduled action picks up files whose info
is incomplete and calls `GetRescanDelay` on each provider that took part in the
previous attempt, re-queuing only those whose delay has already elapsed. Return
`null` from `GetRescanDelay` to opt out of automatic rescanning for a given
file. A stored release with `PreventRescan` set is never rescanned at all,
whatever the providers would have returned.
