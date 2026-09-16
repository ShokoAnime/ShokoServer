# Release Provider Pipeline

This folder defines the public API surface for identifying what anime episode(s)
a video file contains. Providers are pluggable: any plugin can register one or
more `IReleaseInfoProvider` implementations to participate in the pipeline.

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

After the chain finishes, `FinalizeReleaseSearchJob` marks the match attempt
`IsCompleted = true` and fires `IVideoReleaseService.SearchCompleted`.

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
                .Select(ep => new ReleaseVideoCrossReference()
                    .ForAniDB(ep.AnidbEpisodeId, ep.AnidbAnimeId))
                .ToList(),
        };
    }

    // Part of the interface, so it has to be implemented, but returning null is
    // fine if your provider cannot look a release up by its own ID.
    public Task<ReleaseInfo?> GetReleaseInfoById(
        string releaseId, CancellationToken cancellationToken)
        => Task.FromResult<ReleaseInfo?>(null);
}
```

`Name` and the two `GetReleaseInfo*` methods are the only members you have to
supply. `Description`, `Version` and `GetRescanDelay` all have default
implementations, with `Version` falling back to your assembly version. The core
attaches the provider name to the result on the way in, so leaving
`ReleaseInfo.ProviderName` unset is fine.

### Registering

Most providers need no DI registration at all. `PluginManager` finds every type
in your plugin assembly that implements `IReleaseInfoProvider`, constructs it
through `ActivatorUtilities.GetServiceOrCreateInstance` (so constructor
injection works as usual), and hands that instance to `IVideoReleaseService`,
which holds it for the life of the process. That covers the common case,
including a provider that keeps its own caches or runs its own internal timer,
since the held instance is long lived.

Register the **concrete type** as a singleton only when your own code needs to
resolve the provider, such as a sweep job or a controller that calls into it:

```csharp
services.AddSingleton<MyReleaseProvider>();
```

The singleton lifetime is the whole point: it is what makes your job and the
core share one object. A transient registration hands your job a second, freshly
constructed provider and puts you straight back into the split-instance problem
below.

Never register a provider under the `IReleaseInfoProvider` interface:

```csharp
services.AddSingleton<IReleaseInfoProvider, MyReleaseProvider>(); // don't
```

- It pollutes the container for everyone. Resolving a single `T` when several
  registrations exist returns the *last* one registered, so whichever plugin
  loads last silently wins and `GetRequiredService<IReleaseInfoProvider>()`
  hands the caller an arbitrary plugin's provider.
- The core never reads that registration, because `GetExports<T>` asks the
  container for the concrete type.
- So a second instance gets constructed. Anything you treat as singleton state
  (rate limiters, caches, warn-once flags) then splits across two objects, and
  the instance you resolve from DI is not the one the service calls.

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
new ReleaseVideoCrossReference().ForAniDB(episodeID: 123, animeID: 456)

// File covers a range of the episode
new ReleaseVideoCrossReference().ForAniDB(episodeID: 123, animeID: 456,
    percentStart: 0, percentEnd: 50)
```

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
`IVideoReleaseService` and apply uniformly to every provider. Earlier revisions
of this interface exposed `PrepareForSave`, `OnReleaseSaved`, `OnReleaseCleared`
and `OnSearchCompleted`; those were removed, and there is no per-provider
replacement.

`GetRescanDelay` is the one optional hook that remains:

| Method | Called when | Typical use |
|---|---|---|
| `GetRescanDelay(existingInfo, lastAttempt)` | When deciding whether to re-queue a file whose stored info is incomplete | Return a `TimeSpan` to schedule a rescan, or `null` to opt out |

To react to a release being saved, replaced or removed, subscribe to the events
on `IVideoReleaseService`: `ReleaseSaved`, `ReleaseDeleted` and
`SearchCompleted`.

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
    DeferToNext = true,  // provisional, let higher-priority providers try
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

The recurring `ScanForMissingReleaseInfoJob` picks up files whose info is
incomplete and calls `GetRescanDelay` on each provider that took part in the
previous attempt, re-queuing only those whose delay has already elapsed. Return
`null` from `GetRescanDelay` to opt out of automatic rescanning for a given
file. A stored release with `PreventRescan` set is never rescanned at all,
whatever the providers would have returned.
