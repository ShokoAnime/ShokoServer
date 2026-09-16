# Supplementary Metadata Providers

This folder holds several metadata services. This document covers one of them,
`ISupplementaryMetadataProvider`, the only one in here a plugin implements
rather than consumes. For `IAiringScheduleService` see
[`../Airing/README.md`](../Airing/README.md), and for the image cross-reference
side of `IImageManager` see
[`../Image/CrossReferences/README.md`](../Image/CrossReferences/README.md).

`ISupplementaryMetadataProvider` is a single callback with a single question
behind it: *AniDB just told us about anime N, who else wants to know?*

It is not a metadata provider in the usual sense. It returns nothing, it is
never asked for titles, images or episodes, and core never reads anything back
out of it. It is a hook at one specific moment in the import pipeline, and the
thing an implementation does with that moment is normally to enqueue a job.

TMDB and AniList ship as implementations, and both do exactly that: look up
whether this anime already has a link, and schedule either a search or a
refresh.

---

## When core calls it

`SupplementaryMetadataService` holds the registered providers and dispatches to
them. Core reaches it through `ISupplementaryMetadataService`, from three
places:

| Caller | Call | Situation |
|---|---|---|
| `AnidbService.CreateAnimeSeriesAndGroup` | `ScheduleForAnime(animeID, isNew: true)` | An `AnimeSeries` was just created for an anime that had none. |
| `AnidbService`, after the refresh pass | `ScheduleForAnime(animeID, isNew: false)` | AniDB data for the anime was confirmed or refreshed. |
| `AnimeMetadataOrchestrator` | `ScheduleForAnimes(animeIDs, isNew: false)` | A batch sweep over anime with missing or stale data. |

All of it is gated on one flag. `AnidbRefreshMethod.SkipSupplementaryUpdate`,
surfaced as `SkipSupplementaryUpdate` on `GetAniDBAnimeJob` and
`GetRemoteAniDBAnimeJob`, suppresses both `ScheduleForAnime` calls. Core sets it
for refreshes that are not about new content: database fixups, bulk re-reads
from the XML cache, and the AniDB-only refresh paths in `ActionService`. A
provider never sees those.

### A new anime gets two calls, not one

The two `ScheduleForAnime` calls above sit in the same execution path, both
behind the same flag. For an anime Shoko has never seen, the first fires from
inside series creation with `isNew: true`, and the second fires later in the
same pass with `isNew: false`. Both are real, both reach every provider, and
neither is a bug.

The shipping providers survive this because they enqueue through
`IQueueScheduler.RunAfterCurrent`, and the queue deduplicates by job key: the
second call builds the same key as the first and collapses into it. If your
provider does anything other than enqueue a keyed job, it has to be idempotent
per anime by itself.

### `isNew: true` is earlier than it looks

The `isNew: true` call happens inside `CreateAnimeSeriesAndGroup`, immediately
after the `AnimeSeries` row is saved and **before** `CreateAnimeEpisodes` runs.
At that moment the series exists, its group exists, and it has no
`AnimeEpisode` records at all. Treat `isNew: true` as "the series row now
exists", not as "the series is ready to read". Anything that needs episodes
belongs on the `isNew: false` call, or in the job you enqueue, which runs after
the current one finishes.

---

## Implementing one

```csharp
public class MySupplementaryProvider(
    IQueueScheduler scheduler,
    MyLinkRepository linkRepository
) : ISupplementaryMetadataProvider
{
    public string Name => "MyProvider";

    public async Task ScheduleForAnime(int anidbAnimeID, bool isNew)
    {
        // No link yet: let the search job find one.
        var links = linkRepository.GetByAnidbAnimeID(anidbAnimeID);
        if (links.Count == 0)
        {
            await scheduler.RunAfterCurrent<SearchMyProviderJob>(job => job.AnimeID = anidbAnimeID);
            return;
        }

        // Already linked: refresh what it points at.
        foreach (var link in links)
            await scheduler.RunAfterCurrent<UpdateMyProviderJob>(job =>
            {
                job.RemoteID = link.RemoteID;
                job.DownloadImages = true;
            });
    }
}
```

That is the whole shape, and it is close to line-for-line what
`TmdbSupplementaryProvider` and `AnilistSupplementaryProvider` do.

### Required versus defaulted

| Member | |
|---|---|
| `Name` | Required. A display name. Nothing dispatches on it. |
| `ScheduleForAnime(int anidbAnimeID, bool isNew)` | Required. The only call core makes. |
| `Description` | Defaults to `null`. |
| `Version` | Defaults to your assembly version. |
| `OnSeriesRemoved(int anidbAnimeID)` | Defaults to a completed task. See below. |

There is no priority, no enabled flag and no per-provider settings page.
`ISupplementaryMetadataProvider<TConfiguration>` and its
`ISupplementaryMetadataProviderConfiguration` marker exist in the abstractions,
but nothing in the server reflects over them the way `VideoReleaseService` does
for `IReleaseInfoProvider<>`, so implementing the typed variant currently gets
you no configuration UI. Use a `ConfigurationProvider<T>` injected into the
constructor instead.

### `OnSeriesRemoved` has no caller today

`ISupplementaryMetadataService.OnSeriesRemoved` is implemented and fans out to
every provider, and `AnilistSupplementaryProvider` overrides it to drop its
links. But nothing in `Shoko.Server` calls the service method, so as of this
writing the hook never fires. Implement it if cleaning up after a removed series
is cheap to express, and do not rely on it running.

---

## Registering it

Usually you don't. `PluginManager.GetExports<ISupplementaryMetadataProvider>()`
finds the type in your assembly, constructs it with constructor injection and
hands it to `SupplementaryMetadataService.AddParts`, which holds it for the life
of the process. Register the **concrete type** as a singleton only when your own
code resolves the provider, and never register it under the
`ISupplementaryMetadataProvider` interface.

The full rule, and why the interface registration is actively harmful, is in the
[abstractions README](../../README.md).

---

## Mistakes that are easy to make

- **Doing the work instead of scheduling it.** The method is called
  `ScheduleForAnime` for a reason. Providers are awaited one after another, in
  registration order, inline in the middle of the AniDB job that triggered them.
  Time spent here is time the AniDB job is not finishing, and it is multiplied
  by every provider and every anime in a batch. Enqueue and return.
- **Throwing.** `SupplementaryMetadataService` loops the providers with no
  try/catch. An exception from one provider skips every provider after it and
  propagates into the AniDB job that called it. Catch your own failures, log
  them, and return.
- **Treating `isNew` as "this is the only call".** See above: a genuinely new
  anime produces one `true` call and one `false` call. `isNew` answers "did an
  `AnimeSeries` exist before this call", nothing more.
- **Reading episodes on the `isNew: true` call.** They do not exist yet.
- **Assuming a refresh means something changed.** The `isNew: false` call fires
  on a completed refresh pass whether or not any field actually moved. If your
  provider only wants to act on real changes, subscribe to the metadata events
  instead, or make the job you enqueue decide.
- **Expecting to be called for every AniDB read.** `SkipSupplementaryUpdate` is
  set on a good number of internal refresh paths, and a provider that treats
  `ScheduleForAnime` as its only trigger will have gaps. A recurring sweep job
  of your own, registered through `RecurringJobRegistry`, is the usual way to
  cover them.
