# Metadata Services

This folder holds six interfaces. One of them,
`ISupplementaryMetadataProvider`, is an extension point your plugin
implements. The other five are implemented by the server and called by you.

| Interface | Use it to | You |
|---|---|---|
| `ISupplementaryMetadataProvider` | React when AniDB data for an anime is confirmed or refreshed | implement |
| `ISupplementaryMetadataService` | Dispatch that reaction to every registered provider | consume |
| `IMetadataService` | Look up series, episodes, seasons, movies, groups and custom tags, and subscribe to metadata events | consume |
| `IShokoGroupManager` | Create, name, nest and delete groups, and move series between them | consume |
| `IImageManager` | Read, add, link and download images | consume |
| `IAiringScheduleService` | Read and write broadcast schedules | consume |

Three of these have most of their surface documented elsewhere, and this page
does not repeat it. `IAiringScheduleService` has its own page,
[`../Airing/README.md`](../Airing/README.md), covering the service and the two
contracts that feed it. The resolver half of `IImageManager` is in
[`../Image/CrossReferences/README.md`](../Image/CrossReferences/README.md), and
the resolver half of `IMetadataService` is in
[`../Resources/README.md`](../Resources/README.md). What follows is the
consumer side of the last two.

## Getting hold of one

Every service here is registered as a singleton in `SystemService` before
plugins are constructed, so plain constructor injection works everywhere: in
your `IPlugin`, in a service you register yourself, in a queue job, in a
controller.

```csharp
public class MyLibraryService(
    IMetadataService metadataService,
    IShokoGroupManager groupManager,
    IImageManager imageManager
)
{
    public IShokoSeries? Find(int anidbAnimeID)
        => metadataService.GetShokoSeriesByAnidbID(anidbAnimeID);
}
```

Each of the three also carries an `AddParts` method. Those belong to the core:
`PluginManager` calls them once during start-up with the contract
implementations it discovered in your assembly. Never call `AddParts` yourself.

---

# `ISupplementaryMetadataProvider`

`ISupplementaryMetadataProvider` is a single callback with a single question
behind it: *AniDB just told us about anime N, who else wants to know?*

It is not a metadata provider in the usual sense. It returns nothing, it is
never asked for titles, images or episodes, and core never reads anything back
out of it. It is a hook at one specific moment in the import pipeline, and the
thing an implementation does with that moment is normally to enqueue a job.

TMDB and AniList ship as implementations, and both do exactly that: look up
whether this anime already has a link, and schedule either a search or a
refresh.

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

## Registering it

Usually you don't. `PluginManager.GetExports<ISupplementaryMetadataProvider>()`
finds the type in your assembly, constructs it with constructor injection and
hands it to `SupplementaryMetadataService.AddParts`, which holds it for the life
of the process. Register the **concrete type** as a singleton only when your own
code resolves the provider, and never register it under the
`ISupplementaryMetadataProvider` interface.

The full rule, and why the interface registration is actively harmful, is in the
[abstractions README](../../README.md).

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

## `ISupplementaryMetadataService`

The dispatcher side, and the only reason to inject it is to trigger the fan-out
yourself, for an anime your own code just linked or re-read. It has three
methods, `ScheduleForAnime`, `ScheduleForAnimes` and `OnSeriesRemoved`, each
awaiting every registered provider in turn. `ScheduleForAnimes` is a loop over
`ScheduleForAnime`, so a batch of 500 anime is 500 sequential passes over every
provider, not one.

It exposes no list of registered providers and no way to call just one.

---

# `IMetadataService`

The main entry point into library metadata. If a plugin needs to turn an ID into
a series, walk the collection, or hear that something changed, this is the
service it injects.

## Looking things up

The shoko-side lookups return Shoko's own wrappers, and are what most plugin
code wants:

| Call | Returns |
|---|---|
| `GetAllShokoSeries()`, `GetShokoSeriesByID(int)`, `GetShokoSeriesByAnidbID(int)` | `IShokoSeries` |
| `GetAllShokoEpisodes()`, `GetShokoEpisodeByID(int)`, `GetShokoEpisodeByAnidbID(int)` | `IShokoEpisode` |
| `GetAllShokoGroups()`, `GetShokoGroupByID(int)` | `IShokoGroup` |

The provider-side lookups take an `IMetadataService.ProviderName` and hand back
the raw per-provider record behind it:

| Call | `Shoko` | `AniDB` | `TMDB` |
|---|---|---|---|
| `GetAllSeriesForProvider`, `GetSeriesByProviderID` | `AnimeSeries` | `AniDB_Anime` | `TMDB_Show` |
| `GetAllEpisodesForProvider`, `GetEpisodeByProviderID` | `AnimeEpisode` | `AniDB_Episode` | `TMDB_Episode` |
| `GetAllMoviesForProvider`, `GetMovieByProviderID` | nothing | nothing | `TMDB_Movie` |
| `GetAllSeasonsForProvider`, `GetSeasonByProviderID` | nothing | nothing | `TMDB_Season` |
| `GetAllCollectionsForProvider`, `GetCollectionByProviderID` | `AnimeGroup` | nothing | `TMDB_Collection` |

"Nothing" means an empty sequence or `null`, not an exception: those pairs are
answered, because the concept does not exist for that provider. Every `int`
lookup here also returns `null` for an ID of zero or less without touching a
repository, so an unset ID is safe to pass. A `ProviderName` outside the three
values, which you can only produce by casting, throws
`ArgumentOutOfRangeException`.

`GetSeasonByProviderID` is the odd one out: its `providerID` is a `string`,
because a TMDB season is either a numeric season ID or the 24-character hex ID
of an episode group used for an alternate ordering. The implementation matches
`^(?:[0-9]{1,23}|[a-f0-9]{24})$` and routes on length, and anything else returns
`null`. `GetAllSeasonsForProvider(TMDB, includeAlternativeSeasons: true)`
concatenates real seasons with alternate-ordering ones; the default is real
seasons only.

### `ProviderName` has three values, and AniList is not one of them

`IMetadataService.ProviderName` is `Shoko`, `AniDB` and `TMDB`. The wider
`DataSource` enum used everywhere else in the abstractions has fourteen more,
AniList among them, and AniList metadata does ship in core. None of it is
reachable through these lookups. Go through
[`../Anilist/Services/README.md`](../Anilist/Services/README.md) instead, and
read `IShokoSeries` for the links between the two.

## Events

Twelve events, in four families: `Movie`, `Episode`, `Season` and `Series`, each
with `…Added`, `…Updated` and `…Removed`. They are relays of the server's
internal update events, split on `UpdateReason`: `Added` and `Removed` go to
their own event and **everything else**, including reasons you might not think
of as an update, goes to `…Updated`.

The payload is provider-agnostic. `SeriesInfoUpdatedEventArgs.SeriesInfo` is an
`ISeries`, which may be an `AnimeSeries`, an `AniDB_Anime`, a `TMDB_Show` or an
`Anilist_Anime`, so check `SeriesInfo.Source` before assuming. It also carries
`Seasons` and `Episodes`, the nested event args for whatever moved along with
the series, which is how you avoid subscribing to three events to learn one
thing.

There are no group events here. Those live on `IShokoGroupManager`.

```csharp
public class MyWatcher(IMetadataService metadataService) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        metadataService.SeriesAdded += OnSeriesAdded;
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        metadataService.SeriesAdded -= OnSeriesAdded;
        return Task.CompletedTask;
    }

    private void OnSeriesAdded(object? sender, SeriesInfoUpdatedEventArgs eventArgs)
    {
        if (eventArgs.SeriesInfo is IShokoSeries series)
            logger.LogInformation("{Title} was added with {Count} episodes", series.PreferredTitle, eventArgs.Episodes.Count);
    }
}
```

Subscribe and unsubscribe from a hosted service, not from your `IPlugin` class.
The reasons are in the [abstractions README](../../README.md).

## Custom tags

`GetAllCustomTags`, `GetCustomTagByID`, `CreateCustomTag`, `UpdateCustomTag` and
`DeleteCustomTag` manage the tag itself; `AddCustomTagsToSeries`,
`RemoveCustomTagsFromSeries` and `ClearCustomTagsForSeries` manage the links.
The three link methods return `true` when something actually changed and
`false` when the call was a no-op, and they throw `ArgumentException` if any
tag in the list is not one the server knows about.

Tag names are unique. `CreateCustomTag` throws `DuplicateNameException` (from
`System.Data`) when the name is taken, and `UpdateCustomTag` throws the same
when a rename collides. The links are keyed by AniDB anime ID, not by shoko
series ID, so they survive a series being removed and re-added.

## Contributing resources

`GatherResourcesForEntity(entity)` is the read side of the resource system: it
runs every registered `IResourceResolver` over the entity and returns what they
contributed. Entities call it themselves from their `Resources` getter, so a
plugin rarely calls it directly. Writing a resolver, what an entity does with
the result, and the re-entrance guard are all in
[`../Resources/README.md`](../Resources/README.md).

`ResourceResolvers` exposes the registered resolvers, and `AddParts` is the
core's to call.

---

# `IShokoGroupManager`

Groups are the part of the model that surprises people most, so start with what
a group is.

An `AnimeGroup` is a container for series that nests arbitrarily: a group has an
optional parent group, any number of child groups, and any number of series
directly in it. There is no depth limit and no schema-level shape, just a
parent pointer. Every group belongs to a top-level group, which is itself when
it has no parent.

`IShokoGroup` exposes both the direct and the recursive view of that tree, and
getting the two confused is the usual first bug:

| Member | |
|---|---|
| `Series` | The series directly in this group, ordered by air date |
| `AllSeries` | Those plus every series in every descendant group, ordered by air date |
| `Groups` | The direct child groups, unordered |
| `AllGroups` | Every descendant group at any depth, unordered |
| `ParentGroup`, `AllParentGroups`, `TopLevelGroup` | The other direction |

(The concrete `AnimeGroup` calls its recursive child list `AllChildren`;
`IShokoGroup.AllGroups` is the same walk.)

None of these are cached. `AllSeries` runs a depth-first walk of the group tree,
hits the series repository once per group, concatenates and then sorts the whole
result, on **every** access. Reading `group.AllSeries.Count` in a loop over
every group in the collection is quadratic. Read it once into a local.

`MainSeries` is the series a group borrows its title, description and images
from. It resolves in three steps: the user's configured main series
(`DefaultAnimeSeriesID`), then `MainAniDBAnimeID`, then the first entry of
`AllSeries`, which is the earliest-airing series in the group or any descendant.
`HasConfiguredMainSeries` tells the first case from the other two. Note that
`IShokoGroup.MainSeries` is non-nullable and **throws
`NullReferenceException`** for a group with no series at all. Groups are not
supposed to be able to reach that state, which is the next section.

## Reading

`GetAllGroups()` and `GetGroupByID(int)`. `IMetadataService.GetAllShokoGroups()`
and `GetShokoGroupByID(int)` return the same rows from the same repository; the
only difference is that the `IMetadataService` overload short-circuits to `null`
for an ID of zero or less while `GetGroupByID` passes it through.

## Creating, updating and moving

`CreateGroup(GroupData)` returns the new group. `UpdateGroup(IShokoGroup,
GroupUpdateData)` returns the updated one. `SetMainSeries` and `MoveSeries` are
thin wrappers over `UpdateGroup`.

A group must contain at least one series, directly or through a child group.
`CreateGroup` with an empty `GroupData` throws `GenericValidationException` with
errors under both `Series` and `Groups`, and so does any update that would leave
the group empty.

```csharp
// Two series, one group, named after the first.
var group = groupManager.CreateGroup(new()
{
    Series = [firstSeries, secondSeries],
    MainSeries = firstSeries,
});

// Nest it under an existing group. This cannot be done in CreateGroup; see below.
groupManager.UpdateGroup(group, new() { ParentGroup = parentGroup });

// Pull a third series in from wherever it currently lives.
groupManager.MoveSeries(thirdSeries, group);
```

`GroupUpdateData` uses a set-flag pattern: assigning `Name`, `Description`,
`ParentGroup` or `MainSeries` sets the matching `HasName`, `HasDescription`,
`HasParentGroup` or `HasMainSeries` to `true`, **including when you assign
`null`**. That is deliberate, and it is how you clear a field: `Name = null`
resets the group to automatic naming, `ParentGroup = null` promotes it to
top level, `MainSeries = null` hands the choice back to auto-detection. A
property you never touch is left alone.

`Groups` and `Series` are additive. Items you list are moved into the group;
items already there are never removed. There is no "remove a series from this
group" call, because a series always belongs to exactly one group: you move it
to another one. Moving the last series out of a group deletes that group
automatically, along with clearing the old group's main-series pointers if they
referred to the departing series.

Parent assignment is validated against cycles, both group-to-parent and
child-to-parent, and a cycle throws `GenericValidationException` with the error
under `ParentGroup`.

### Auto versus manual naming

A group carries two flags, surfaced as `IShokoGroup.HasCustomTitle` and
`HasCustomDescription`. Setting `Name` to a string sets the title flag and
pins the name; setting it to `null` clears the flag and re-derives the name from
`MainSeries.Title`. The description works the same way against the main series'
preferred overview. Changing the main series on a group that has not been
manually named re-derives both.

`RenameAllGroups()` sweeps every group and re-derives whichever of the two is
not pinned, skipping groups where both are. It is the repair tool for a
collection whose titles drifted after a metadata refresh, and it is safe for
manually named groups by construction.

## Deleting

`DeleteGroup(group, deleteSeries: false, deleteFiles: false)`.

With `deleteSeries: false`, every series in the group and its descendants is
moved into a new group of its own. With `deleteSeries: true`, they are deleted,
and `deleteFiles` decides whether their files go with them. `deleteFiles` does
nothing when `deleteSeries` is `false`.

The group row itself is removed by the same empty-group cleanup that runs on any
move, not by `DeleteGroup` directly. The observable result is the same, but it
means the delete is a cascade rather than one statement, and the
`GroupRemoved` event for the group arrives from that cleanup.

## Auto-grouping

Four properties, all of them live views over server settings that save on every
assignment. Nothing is applied retroactively: changing one affects future
grouping decisions, and `RecreateAllGroups()` if you ask for it.

| Member | |
|---|---|
| `IsAutoGroupingEnabled` | Whether new series are auto-grouped by relation at all |
| `UseAutoGroupingRelationWeighting` | Pick a group's main series by relation weight instead of earliest air date |
| `AutoGroupingRelationExclusions` | Relation types that do not pull two series into one group |
| `AllowDissimilarTitleExclusion` | See the warning below |

`RecreateAllGroups()` is not an incremental pass. It pauses the queue, blocks the
database, drops every group and rebuilds the lot from the current settings. Any
manual grouping, manual naming and manually chosen main series in the collection
is part of "every group". Do not call it on a schedule, and do not call it as
part of installing a plugin.

### Two sharp edges in the auto-grouping properties

**`AllowDissimilarTitleExclusion` reads backwards.** Its doc comment says "allow
titles that are not similar to be grouped together". The setting it toggles does
the opposite: `true` turns **on** the fuzzy title check for secondary relations,
so two related series whose titles are not similar enough end up in *different*
groups. Set it to `true` to keep dissimilar titles apart.

**`AutoGroupingRelationExclusions` does not round-trip cleanly.** It is stored
as a list of strings shared with the grouping task, which parses those strings
into its own internal enum rather than `RelationType`. The names mostly line up,
but `RelationType.SharedCharacters` and `RelationType.MainStory` have no
counterpart there and are silently ignored when grouping runs. In the other
direction, entries the grouping task understands and `RelationType` does not are
dropped from the value you read back, though the setter does preserve them in
storage.

## Events

`GroupAdded`, `GroupUpdated`, `GroupRemoved` (all
`GroupInfoUpdatedEventArgs`), `SeriesMoved` (`SeriesMovedEventArgs`, carrying
the old and new group IDs) and `GroupsRecreated` (a bare `EventHandler`).
Adds and removes are split off `UpdateReason` the same way the metadata events
are, with everything else arriving as `GroupUpdated`.

One user-visible move raises more than one event. Moving a series fires
`GroupUpdated` for the destination, `SeriesMoved` for the series, and, if the
source group is now empty, `GroupRemoved` for it. Write handlers that tolerate
being called several times for one logical change.

## Things that are not what the doc comments say

The interface documents `ArgumentException` on several methods for an argument
that is not a concrete `AnimeGroup` or `AnimeSeries`. Only `DeleteGroup`
actually raises it. What the others do:

- `UpdateGroup`, `SetMainSeries` and `MoveSeries` cast their group argument
  straight to `AnimeGroup`, so a foreign `IShokoGroup` raises
  `InvalidCastException`. Every `IShokoGroup` the server hands you is an
  `AnimeGroup`, so in practice this only bites a test double.
- A series in `GroupUpdateData.Series` that is not an `AnimeSeries` is skipped
  in silence rather than rejected, so the call succeeds having moved nothing.
- `GroupData.ParentGroup` is **not applied by `CreateGroup`**. The value is
  accepted and dropped. Create the group, then `UpdateGroup` it with
  `ParentGroup` set, as the example above does.
- `SetMainSeries` validates the group's *existing* main series, not the one you
  are setting. Pointing it at a series outside the group succeeds, and the next
  update of that group throws `GenericValidationException` for a state your
  earlier call created. Pass a series you know is in the group.

---

# `IImageManager`

Every image in Shoko, whatever provider it came from, is one row in one table,
and every link from an image to an entity is a cross-reference row. This service
owns both. A plugin uses it to read the images an entity has, to add its own, and
to schedule downloads.

The other half of the interface, `TryGetMetadataForEntity`, `GetEntityForImage`,
`AddParts` and `ImageCrossReferenceResolvers`, exists so a plugin's own entity
types can take part in that scheme, and is covered in
[`../Image/CrossReferences/README.md`](../Image/CrossReferences/README.md).

## Reading the images for an entity

`GetImagesForEntity(entity, options)` is the call, but you rarely write it out.
`IWithImages`, which every entity that can carry an image implements, has
default methods that resolve the manager and forward to it:

```csharp
// The poster to actually show, whatever the entity has available.
var poster = series.GetBestImageForType(ImageEntityType.Primary);

// Everything, or a filtered view.
var backdrops = series.GetImages(new() { ImageType = ImageEntityType.Backdrop });

// Just what the user pinned.
var preferred = series.GetPreferredImageForType(ImageEntityType.Primary);
```

`GetBestImageForType` is the one to reach for when you want *an* image and do
not care where it came from. It tries the preferred cross-reference, then the
entity's default one, then the first cross-reference of that type that is
enabled and available, degrading to enabled-and-desired and finally to merely
enabled. It returns `null` only when the entity has no usable cross-reference
of that type at all.

`ImageEntityType` is `Primary`, `Backdrop`, `Banner`, `Logo` and `Disc`, plus
`None`, which is never a valid value to pass.

### `ImageFilteringOptions`

Every field is optional and every `bool?` is a tri-state: `true` keeps only
matches, `false` keeps only non-matches, `null` filters nothing.

| Field | |
|---|---|
| `ImageSource` | Restrict to images from one `DataSource` |
| `ImageType` | Restrict to one `ImageEntityType` |
| `XrefSource` | Restrict to cross-references created by one `DataSource` |
| `IsEnabled`, `IsDesired`, `IsPreferred` | Cross-reference state |
| `IsAvailable`, `IsPrimaryAvailable` | Whether the file is on disk |
| `IsPrimaryImage` | `true` for canonical images, `false` for variants |
| `AsPrimaryImage` | A plain `bool`, not a filter: redirect each result to its canonical primary image |
| `LinkedEntityImages` | See below |

### `LinkedEntityImages` is a tri-state with a real default

This is the field worth understanding, because leaving it unset does not mean
"off".

`false` returns only the images cross-referenced against the entity itself.
`true` also walks the entity's links and returns theirs. `null`, the default,
lets the service decide, and it decides `true` for `IShokoGroup`,
`IShokoSeries`, `IShokoSeason` and `IShokoEpisode`, and `false` for everything
else.

That default is what makes a shoko series show TMDB posters without anyone
asking: the walk covers the series, its linked provider series, its TMDB seasons
and its linked movies, deduplicating entities reachable by more than one path. A
group goes through its main series, and falls back to the other series in the
group when the main series turned out to have no images of its own. A season or
episode walks its linked seasons or episodes plus linked movies.

The results are then ordered by image type, own images before linked ones, with
user and locally-generated sources first within that, then by source, entity and
the cross-reference's own `Ordering`, and deduplicated on image plus type. Pass
`LinkedEntityImages = false` when you want to know what an entity itself owns,
for example before adding a cross-reference of your own.

`GetImageCrossReferencesForEntity` takes an
`ImageCrossReferenceFilteringOptions` with the same fields plus `EntitySource`
and `EntityType`, and behaves identically. Those two extra fields are ignored
there, since the entity is already known; they apply to
`GetAllImageCrossReferences`, which sweeps the whole table.

## Finding an image

`GetImageByID(Guid)` is the lookup. The `int` overload is for legacy IDs and is
marked `[Obsolete]`. `GetImageBySourceAndRemoteResourceID(source, resourceID)`
is the safe way for a provider to check whether it has already stored an image
before adding it again. All three take a `primaryImage` flag that redirects a
variant to its canonical image.

`GetFirstSeriesForImage(image)` answers "what is this a picture of", returning
the linked series with the earliest release date, or `null`.

## Adding an image and linking it

Two ways in. `AddImage(ImageData)` registers an image that lives at a provider
and will be fetched later, which requires a template URL to be configured for
that source and throws `MissingImageSourceTemplateUrlException` if there is
none. `UploadImage(stream | byte[], contentType, userSubmitted)` stores bytes
you already have; pass `userSubmitted: false` for something your plugin
generated, such as an extracted thumbnail. Both reject a MIME type outside
`AllowedMimeTypes` with `UnsupportedImageTypeException`.

Neither attaches the image to anything. That is `AddImageCrossReference`:

```csharp
var image = imageManager.UploadImage(stream, "image/jpeg", userSubmitted: false);
var xref = imageManager.AddImageCrossReference(episode, image, new()
{
    ImageType = ImageEntityType.Backdrop,
    Source = DataSource.Plugin,
    IsEnabled = true,
    IsDesired = true,
    IsPreferred = false,
});
```

`ImageType` is required and rejects `None` on assignment. `Source` defaults to
`DataSource.User`, so a plugin that wants its rows attributed to itself has to
say `DataSource.Plugin`. `IsEnabled` defaults to `true`, `IsDesired` (which is
what marks an image for auto-download) and `IsPreferred` default to `false`, and
`Ordering` appends at the end when left `null`. `Rating` and `RatingVotes` keep
each other in step: setting one while the other is unset fills the other in.

Duplicate detection is on the triple of image, image type **and** source, so the
same image can legitimately be attached to the same entity twice under two
different sources. A genuine collision throws
`ImageCrossReferenceExistsException`, which carries the existing cross-reference,
the image and the entity, so catching it is a reasonable alternative to checking
first.

Setting `IsPreferred = true`, on an add or an update, demotes whichever
cross-reference of the same image type on that entity was preferred before.
Preferred is one image per entity per type, enforced for you.

`SetPreferredImageForEntity(entity, imageType, image)` is the shortcut, and it
creates the cross-reference if there is none. Be aware that the row it creates
takes the default `Source`, `DataSource.User`. Call `AddImageCrossReference`
first if the attribution matters. The overload taking an `IImageCrossReference`
promotes an existing row instead. `UnsetPreferredImageForEntity` and
`UnsetAllPreferredImagesForEntity` undo it, and `RemoveImageCrossReference`
drops the link entirely, leaving the image itself alone.

## Downloading, validating and purging

`DownloadImage(image, force)` fetches now and returns whether it succeeded;
`ScheduleDownloadOfImage` queues it. `CheckIfAvailableAtRemote` asks the
provider without fetching. For bulk work,
`ScheduleAutoDownloadsForEntity(entity, imageSource, imageType, xrefSource,
force)` queues every desired image linked to one entity, and
`ScheduleAllAutoDownloads` does the same across the collection. Only
cross-references with `IsDesired` set are candidates unless `force` is set.

On the other side, `GetOrphanedImages(daysOld, imageSource)` lists images no
cross-reference points at any more, `PurgeImage` and `PurgeOrphanedImages`
delete them, each with a `Schedule…` variant, and `ValidateAllImages` re-checks
what is actually on disk. `daysOld` defaults to `7` throughout.

## Events

`ImageAdded`, `ImageUpdated`, `ImageDownloaded`, `ImageRemoved`, and
`ImageCrossReferenceAdded`, `ImageCrossReferenceUpdated`,
`ImageCrossReferenceRemoved`. `ImageAdded` means a row exists, not that a file
does; `ImageDownloaded` is the one that means there are bytes on disk.

Demoting a sibling preferred image raises its own `ImageUpdated` and
`ImageCrossReferenceUpdated`, so setting one preferred image can produce two
pairs of events.
