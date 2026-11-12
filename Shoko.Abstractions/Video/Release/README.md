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

## Cross-references and their IDs

Each `ReleaseInfo.CrossReferences` entry maps a segment of the file to content.
The mapping is a list of `MetadataGuid`s (`IDs`), each naming one entry of one
source, written `<source>://<kind>/<id>`: an AniDB episode is
`anidb://episode/123` and its anime `anidb://series/456`. A cross-reference
names at least one entry, of any source; it need not name an AniDB one.

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
The `AnidbEpisodeID` and `AnidbAnimeID` members read the first AniDB episode
and anime ID of any cross-reference, and set them on a
`ReleaseVideoCrossReference`.

### Linking entries of other sources

Add their IDs to the list, or use `ReleaseVideoCrossReference.For`:

```csharp
var source = MetadataSource.Get("my-source");
ReleaseVideoCrossReference.For([new MetadataGuid(source, MetadataEntityType.Episode, "e12")])
```

Every source and kind a cross-reference names needs a validator (see
[Validating cross-references](#validating-cross-references)), or the release is
refused when it is saved.

## Validating cross-references

A release is checked when it is saved, whoever saves it: a provider's search, a
user through the API, or a plugin. The core first tidies each cross-reference's
percentage range, then runs every `IReleaseCrossReferenceValidator`.

- **Only series, episodes and movies.** A cross-reference may name a series,
  an episode or a movie, of any source, and nothing else: a season, a
  collection or any other kind refuses the release, whatever a validator's
  scope says. An episode usually comes with its series, which a validator may
  fill in, as the core's AniDB one does with the anime.
- **A source and kind needs a validator.** A validator's `Scope` (a
  `MetadataEntityScope`) is what lets cross-references name entries of those
  sources and kinds. A cross-reference naming an entry no validator takes, or
  naming no entry at all, refuses the release.
- **Order.** Validators run in plugin load order, the core's first. Each is
  asked about every cross-reference naming an entry of its scope, with the
  whole release in view (`ReleaseCrossReferenceValidationContext`).
- **Results.** `null` means "not my concern". Otherwise
  `ReleaseCrossReferenceValidationResult.IsValid` decides. An invalid result
  carries a `Reason` and refuses the whole release. A valid one may correct
  the cross-reference:
  - `Corrected(ids)` gives the IDs to save in its place, keeping its range.
    Only IDs of the validator's own scope may be added or removed.
  - `Corrected(ids, start, end)` also gives the range. The range belongs to the
    whole cross-reference, whatever IDs sit in it, so any validator taking one
    of them may change it.
- **Overlaps.** Several validators may take the same source and kind. All of
  them must find a cross-reference valid, and corrections chain: each sees the
  cross-references as the ones before corrected them. When a validator changes
  a range, the validators that already found the cross-reference valid are
  asked again. If one of them changes the range again, the two disagree and
  the release is refused, naming it.
- **Failures.** A refused release throws `ReleaseValidationException`, naming
  the validator that refused it. A validator that throws refuses the release
  too, and is logged. When the service a validator needs cannot be reached,
  it should find the cross-reference valid as it is.
- **Only on save, and when healing.** Stored releases are never refused after
  the fact, so those naming a source whose plugin is gone still load. The
  "Download Missing Metadata for Files" action asks the validators again about
  a stored release whose cross-reference names an episode without its series
  (`ReleaseCrossReferenceValidationContext.IsHealing`): their corrections are
  saved, with the AniDB link rows rebuilt to match, while an invalid result or
  a throw only leaves that cross-reference as it was. A validator fills in
  what it can then, such as the series of a stored episode, and otherwise
  finds it valid as it is.

Validators belong to the plugin using the links, not to the metadata provider
of the source. The core's `AniDB` validator takes AniDB episodes and anime:
each cross-reference names one AniDB episode AniDB knows, and at most one
anime, which it fills in from the episode when missing (left out while AniDB
bans us). Only AniDB cross-references get `CrossRef_File_Episode` rows and
queue the AniDB anime after the save, so a release naming only another
source's entries links nothing on the AniDB side.

```csharp
public class MySourceValidator : IReleaseCrossReferenceValidator
{
    public string Name => "MyPlugin";

    public MetadataEntityScope Scope { get; } =
        MetadataEntityScope.Single(MetadataSource.Get("my-source"), MetadataEntityType.Episode);

    public Task<ReleaseCrossReferenceValidationResult?> Validate(
        ReleaseCrossReferenceValidationContext context, CancellationToken cancellationToken)
        => Task.FromResult<ReleaseCrossReferenceValidationResult?>(
            context.CrossReference.IDs.All(IsKnown)
                ? ReleaseCrossReferenceValidationResult.Valid()
                : ReleaseCrossReferenceValidationResult.Invalid("Unknown episode."));
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
`CrossRef_File_Episode` in sync, validating the cross-references (the core's
AniDB validator resolves a missing anime ID from an episode ID), running
auto-management and scheduling relocation all live in `IVideoReleaseService`
and apply uniformly to every provider.

`GetRescanDelay` is the one optional hook that remains:

| Method | Called when | Typical use |
|---|---|---|
| `GetRescanDelay(existingInfo, lastAttempt)` | When deciding whether to re-queue a file whose stored info is incomplete | Return a `TimeSpan` to schedule a rescan, or `null` to opt out |

To react to a release being saved, replaced or removed, subscribe to the events
on `IVideoReleaseService`: `ReleaseSaved`, `ReleaseDeleted` and
`SearchCompleted`.

The core only uses a provider while it is available
(`IVideoReleaseService.GetAvailability`): enabled by the admin, configured,
and not held back by a suspension. Return `false` from `IsConfigured` while you
lack what you need, such as credentials, with a `NotConfiguredReason` a person
can act on: searches pass you by and none of your jobs are queued until you
are configured. A suspension provider naming your type in `HeldProviderTypes`
holds you back while it is suspended: searches started directly pass you by,
and your queued jobs wait. The AniDB provider does both: it is not configured
without AniDB credentials, and the `AniDB UDP` suspension holds it during a
ban.

A search runs one job per video, so an import can queue hundreds of yours at
once. Set `MaxConcurrentJobs` to cap how many run at the same time, such as
one for a provider calling a slow or paid service. A job class of your own
keeps the limit its attributes set.

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
