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
| `null` | — | No match; chain advances to the next provider |
| `ReleaseInfo` | `false` (default) | Match saved; chain stops |
| `ReleaseInfo` | `true` | Match saved as provisional; chain continues — a later provider may replace it |

If every provider in the chain returns `null`, the file is left unrecognised.
If every provider defers, the last provisional result stands.

After the chain finishes, `FinalizeReleaseSearchJob` marks the match attempt
`IsCompleted = true` and fires `IReleaseInfoProvider.OnSearchCompleted` on the
winning provider (if any).

---

## Implementing a provider

```csharp
public class MyReleaseProvider : IReleaseInfoProvider
{
    public string Name => "MyProvider";
    public Version Version => new(1, 0, 0);

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
            ProviderName = Name,
            CrossReferences = result.Episodes
                .Select(ep => new ReleaseVideoCrossReference()
                    .ForAniDB(ep.AnidbEpisodeId, ep.AnidbAnimeId))
                .ToList(),
        };
    }

    public Task<ReleaseInfo?> GetReleaseInfoById(
        string releaseId, CancellationToken cancellationToken)
        => Task.FromResult<ReleaseInfo?>(null); // optional
}
```

Register in your plugin's `RegisterServices`:

```csharp
services.AddSingleton<IReleaseInfoProvider, MyReleaseProvider>();
```

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
file covers — useful when a single file spans multiple episodes or when multiple
files together make up one episode:

- Both `null` → the file covers the whole episode (0–100)
- `PercentageStart = 0, PercentageEnd = 50` → first half of the episode
- `PercentageStart = 50, PercentageEnd = 100` → second half

---

## Provider lifecycle hooks

All hooks have default no-op implementations; only override what your provider
needs.

| Method | Called when | Typical use |
|---|---|---|
| `PrepareForSave(video, releaseInfo)` | Before the matched result is written to the DB | Fill in missing IDs (e.g. look up anime ID from episode ID), validate group names |
| `OnReleaseSaved(video, saved, xrefs)` | After the result is written to the DB | Schedule metadata downloads, update MyList |
| `OnReleaseCleared(video, cleared, replacing)` | When the result is removed or replaced | Clean up provider-side bookkeeping |
| `OnSearchCompleted(args)` | After the full chain finishes — only on the winning provider | Trigger any post-import work that must run once the whole chain is done |
| `GetRescanDelay(existing, lastAttempt)` | When checking whether to re-queue a file with incomplete info | Return a `TimeSpan` to schedule a rescan, or `null` to skip |

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
    DeferToNext = true,  // provisional — let higher-priority providers try
};
```

---

## Match attempts and rescans

`IReleaseMatchAttempt` tracks the per-file match history:

- `IsSuccessful` — `true` if any provider matched
- `IsCompleted` — `true` once the chain ran to completion (set by a non-deferred
  save or by `FinalizeReleaseSearchJob`)
- `AttemptCount` — how many times the file has been (re-)processed; used to
  compute backoff delays
- `AttemptedProviderNames` — ordered list of every provider that was called

The recurring `ScanForMissingReleaseInfoJob` calls `GetRescanDelay` on each
provider for files whose info is incomplete. Return `null` from `GetRescanDelay`
to opt out of automatic rescanning for a given file.
