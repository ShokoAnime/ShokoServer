# Metadata Services

Every interface here is implemented by the server and called by you.

| Interface | Use it to |
|---|---|
| `IMetadataService` | Look up entries of any source, Shoko's series, episodes and groups, and custom tags, and subscribe to metadata events |
| `IShokoGroupManager` | Create, name, nest and delete groups, and move series between them |
| `IImageManager` | Read, add, link and download images |
| `IAiringScheduleService` | Read and write broadcast schedules ([`../Airing/README.md`](../Airing/README.md)) |
| `IMetadataTextManager` | Keep, choose and gather the titles and overviews of any entry |
| `IMetadataProviderManager` | See and set which provider answers for which source and kind, in what order the rest stand by, which sources are reserved, and each source's icon |
| `IMetadataImageContributorManager` | List the image contributors and turn each one on or off per source and kind |
| `IMetadataLinkingService` | Make, break and correct links between Shoko entries and a source's, one at a time or in bulk |
| `IMetadataRefreshService` | Ask the providers to refresh entries, download their images and auto-search, and read why a source is paused |
| `IMetadataPurgeService` | Purge a source's entries, unused entries, collections and orphaned people, studios and networks |
| `IMetadataCrossReferenceTransferService` | Export a source's links to CSV and import them back |
| `IMetadataMatchingEngine` | Judge how well a source's data lines up with AniDB's |
| `IMetadataOrderingService` | Read and keep the orderings of any source's series, choose the one each series uses, and hide episodes |
| `IMetadataOrderingTransferService` | Export orderings with their images and import them back as local orderings |

This page covers how the services behave together; each member's doc comment
is the reference. Relations and suggestions are in
[`../README.md`](../README.md), the provider side of refreshing, linking and
storing in [`../Providers/README.md`](../Providers/README.md), and the typed
stores sit next to their records in [`../Storage/`](../Storage/). The resolver
halves of `IImageManager` and `IMetadataService` have their own pages:
[`../Image/CrossReferences/README.md`](../Image/CrossReferences/README.md) and
[`../Resources/README.md`](../Resources/README.md).

Every service is a singleton registered before plugins are constructed, so
constructor injection works in your services, queue jobs and controllers. The
class implementing `IPlugin` keeps a parameterless constructor and takes them
in `IPlugin.Setup` instead.

---

# `IMetadataService`

## Looking things up

The Shoko lookups return Shoko's own wrappers and are what most plugin code
wants: `GetShokoSeriesByID`, `GetShokoSeriesByAnidbID`, `GetShokoEpisodeByID`,
`GetShokoEpisodeByAnidbID`, `GetShokoGroupByID` and the `GetAllShoko…`
enumerations.

Everything else takes a `MetadataGuid`. `GetEntry(id)` answers any kind;
`GetEntry<TMetadata>(id)` only when the entry is a `TMetadata`. `GetSeries`,
`GetSeason`, `GetEpisode`, `GetMovie` and `GetCollection` are the typed forms,
and `MetadataServiceExtensions` adds overloads taking a source and an `int`
(`metadataService.GetEpisode(MetadataSource.AniDB, 1)`). `GetSeries` and
`GetEpisode` also take an ordering's ID, for the entry as that ordering
presents it, and give `null` when the ordering is another series' or leaves
the episode out. What each source answers, by kind:

| Kind | `shoko` | `user` | `anidb` | Any plugin source, `tmdb` included |
|---|---|---|---|---|
| `series` | `AnimeSeries` | | `AniDB_Anime` | the series store |
| `season` | a Shoko season | a group of a user's ordering | an AniDB season | the series store, then a group of a stored ordering |
| `episode` | `AnimeEpisode` | | `AniDB_Episode` | the series store |
| `movie` | | | | the movie store |
| `collection` | `AnimeGroup` | | | the collection store |
| `ordering` | the default | a user's ordering | the default | a stored ordering, or the default |
| `creator` | | | AniDB's creator | the people store |
| `character` | | | AniDB's character | the people store |
| `studio` | | | AniDB's creator, as a studio | the studio store |
| `network` | | | | the studio store |
| `tag` | | a custom tag | AniDB's tag | the tag store |
| `video` | `VideoLocal` | | | |
| `user` | `JMMUser` | | | |
| `filter` | `FilterPreset` | | | |
| `channel` | an airing channel | | | |
| any other | | | | the plugin's `IMetadataResolver`, if one took it |

A blank cell answers nothing, and `generated` answers nothing at all. For a
plugin source the resolver that took the ID's source and kind is asked first
and the stores answer when it returns `null`
([resolving your own kinds](../Providers/README.md#resolving-your-own-kinds)).
No provider is ever asked, so a disabled or removed plugin's stored entries
still read. "Nothing" is `null` or an empty sequence, never an exception.

Some IDs are shaped rather than numbered: a Shoko or AniDB season is
`<series ID>:<episode type>:<season number>` (found but not enumerated), a
video `<ED2K>+<file size>`, a TMDb genre or keyword `genre/<id>`,
`genre/<id>/<part>` or `keyword/<id>`, and an airing channel its GUID. Cast
and crew credits, cross-references and search results are not entries; a
cross-reference carries the ID of the entry it points at.

`GetCollectionsWith` answers the stored collections a series, movie or group
is in. `GetAllSeasonsForSource(source, includeAlternativeSeasons: true)` adds
the groups of a source's stored orderings to its real seasons.

`GetSiteUrl(entry)` answers the address of an entry's page on its source's
site, or `null`: the resolver that took the source and kind first, then the
source's series provider (series, seasons, episodes) or movie provider
(movies, collections), enabled or not, or for creators, characters, studios
and networks its entity provider first. The core answers AniDB's anime,
episodes, creators, characters and studios. Shoko's own
entries and users' orderings have none. `GetSiteUrl(id)` looks the entry up
first, and asks about a bare ID when nothing holds it.

## Events

`Series…`, `Season…`, `Episode…` and `Movie…`, each with `Added`, `Updated`
and `Removed`. `Added` and `Removed` get their own event and every other
`UpdateReason` arrives as `…Updated`. A store write raises them for what
changed, provider refreshes included; providers raise nothing themselves.

The payload is provider-agnostic: `SeriesInfoUpdatedEventArgs.SeriesInfo` is
an `ISeries` of any source, so check `SeriesInfo.Source`. It also carries the
nested `Seasons` and `Episodes` that changed with it, and `Actor`, the API
token the change was made for (from `IActorContext`), or `null` for the
system. Group events live on `IShokoGroupManager`.

```csharp
public class MyWatcher(IMetadataService metadataService, ILogger<MyWatcher> logger) : IHostedService
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

Subscribe from a hosted service, not from your `IPlugin` class; see the
[abstractions README](../../README.md).

## Custom tags

`GetAllCustomTags`, `GetCustomTagByID`, `CreateCustomTag`, `UpdateCustomTag`
and `DeleteCustomTag` manage the tags; `AddCustomTagsToSeries`,
`RemoveCustomTagsFromSeries` and `ClearCustomTagsForSeries` manage the links
and return whether anything changed. Names are unique (`DuplicateNameException`
on a clash), and the links are keyed by AniDB anime ID, so they survive a
series being removed and re-added.

## Resources

`GatherResourcesForEntity(entity)` runs every `IResourceResolver` over an
entity. Entities call it from their own `Resources` getter, so a plugin rarely
does; see [`../Resources/README.md`](../Resources/README.md).

---

# Refreshing, purging and linking

`IMetadataRefreshService` queues the providers' jobs:
`RefreshEntry` (one series, film or collection), `RefreshForAnime` and
`RefreshAllLinked`, the image jobs (`DownloadImages`, `DownloadImagesForAnime`,
`DownloadAllImages`) and the auto-linker's searches (`AutoSearch`,
`AutoSearchAll`). A force flag skips the hour-long freshness window, and
`MetadataRefreshOptions` carries the refresh switches. `IsRefreshing` and
`WaitForRefresh` let a reader avoid handing back a copy that is about to
change, and `GetLastRefreshedAt` reads when a series, film or collection was
last refreshed. `GetPauseStatus` and `PauseStatusChanged` say why a source cannot take
work.

`IMetadataPurgeService` queues the core's purge job: `PurgeEntry`,
`PurgeUnused` (entries nothing links to, optionally by age or kind),
`PurgeCollections` and `PurgeOrphaned` (unused people, studios and networks).
The core runs `PurgeUnused` and `PurgeOrphaned` daily as scheduled actions, by
the admin's settings (two weeks and a week unless changed). A purged series
takes every ordering of it along. Keep any cutoff you pass a day or more in
the past, since a running refresh may have saved something it has not used
yet.

`IMetadataCrossReferenceTransferService` writes a source's links as CSV in one
format for every source, and reads them back. An import reads every
line before writing, so a bad line changes nothing.

## Linking

`IMetadataLinkingService` writes links for every source, with
the checks and follow-ups a person's change needs. The store's `Merge…Links`
methods write links without any of them.

- **Adding.** `AddSeriesLink` also matches the anime's episodes to the series
  and saves them, keeping the episode links already there. Adding and matching
  need a provider enabled for the kind, and throw `NotSupportedException`
  otherwise; every removal works regardless.
- **Removing.** `RemoveSeriesLink`, `RemoveMovieLink` and the bulk
  `RemoveAllLinks`, `RemoveLinksForAnime`, `RemoveLinksForEpisode` and
  `RemoveLinksTo` can queue a purge of what the links pointed at. A request
  with `DisableAutoLinking` sets the anime's veto on the source;
  `ResetAutoLinkingState` sets or lifts it for every anime. Removing an
  anime's last series link also takes the episode links naming no series.
- **Auto-linking.** `PreviewAutoLink` hands back every candidate the source's
  auto-linker scored, each one turned down saying why (the core's own
  refusals included). `AutoLink` queues the search that links the ones taken.
  The entries the anime's AniDB resources name (`AnidbResource`) and those its
  links on other sources name (`CrossSourceLink`, from `GetCrossSourceHints`)
  are hints: one at most is taken, only where the search took nothing it
  outranks, and never as `UserVerified`. The matching engine also lines up a
  season's episodes by air date when a candidate carries them
  (`MetadataSearchResultSeason.Episodes`); see `IMetadataMatchingEngine`.
- **Verifying.** `SetMatchRating` changes only the rating of links as read
  from the store, keeping their place, writer, season and numbers. The
  `…SourceLinks` filter expressions find series whose links still need a
  look ([`../../Filtering/Services/README.md`](../../Filtering/Services/README.md)).
- **Episodes.** `MatchEpisodes` asks the source's provider and previews
  unless told to save; a saved result replaces the links of each episode it
  names. `ResetEpisodeLinks` clears an anime's episode links, or with
  `allowAutoMatch: false` leaves each deliberately linked to nothing.

No linking call queues a refresh. The caller asks
`IMetadataRefreshService.RefreshEntry` when the entry is not stored yet or a
person asked for one. The core's search job is the exception: it refreshes
what it linked. Every change to an anime's links drops what its series cached
about its titles and overview.

`LinksChanged` is raised once per write that added, removed, replaced or
re-rated links, through this service or straight through
`IMetadataCrossReferenceStore`. Each `MetadataLinkChange` names the source,
level, AniDB anime and episode, the entry (and the one it replaced) and the
rating before and after; `MetadataLinkChangeReason` says why, and `Actor` who
for. Reordering links and syncing an episode link's season and numbers raise
nothing. SignalR clients of the `metadata` feed get it as `links.changed`.

```csharp
linkingService.LinksChanged += (_, eventArgs) =>
{
    foreach (var change in eventArgs.Changes.Where(change => change.Source == mySource))
        logger.LogInformation("{Kind} {Entry} on AniDB anime {AnimeID} ({Reason})", change.Kind, change.ProviderID, change.AnidbAnimeID, eventArgs.Reason);
};
```

---

# `IMetadataTextManager`

Every stored title and overview belongs to one entry (by `MetadataGuid`) and
to the source that wrote it, in two tables held in memory. A source's own
default stays on the entry's row and is read beside them (`IText.IsInlineDefault`).

## Who writes what

The manager checks no source; the conventions are:

- A provider writes its own entries' texts under its source with `SetTitles`
  and `SetOverviews`, each call replacing that source's texts on the entry. A
  text that stays keeps its ID, so a user's picks on it stay too.
- A plugin may add texts under its own source to any entry.
  `GetContributedTitles` and `GetContributedOverviews` list them, and
  `RemoveContributions` takes a source's additions off every entry but its own.
- `user` holds what a person picked or typed (`AddText`, `UpdateText`,
  `EnableText`, `SetPreferredTitle`, `SetPreferredOverview` and the unsetting
  members); `shoko` is for text the core makes.

## Main titles and synthesized ones

An entry's main title from its own source (`TitleType.Main`) is its default.
A plugin source's entry stored without one gets a synthesized default instead,
listed first in `Titles` as its main title and never stored
(`ITitle.IsSynthesized`): an episode's generic title and a season's generic
name (`Season 2`, `Staffel 2`, `Specials`) in each episode language the user
picked that has a form, then in English, or `<source> <kind> <id>` for any
other kind, such as `TMDb Series 46195`.

An episode as an ordering other than the default presents it, such as a
place's untyped `Episode`, is numbered by its place there, so its synthesized
titles are too, as in `Episode 7` or AniDB's `Episode S2`. Its real titles and
a user's pick are the episode's own, as it keeps the episode's ID.

For an episode or season whose only name is a generic one, storing nothing is
the better choice: the core synthesizes it in the user's languages and in the
numbering of each ordering. A write leaves out the generic titles of every
source's episodes and seasons, in every language the core knows, and adding
or picking one by hand throws `ArgumentException`. A title is generic only when
it carries the entry's own number: `Season 11` on season 8 or `Episode 13` on
episode 1 is a real title and stays.

## A user's picks

A disabled text is kept, so a refresh does not bring it back, but it is not
listed by default or chosen. `TextPreference.Overall` beats every other text
of the entry; `TextPreference.Language` wins once the language order reaches
its language. Picking something that is not a stored text of the entry (the
row's default, another entry's text, a typed value) stores a `user` copy; a
copy of another stored text follows it through `ReferenceID` and goes when it
goes.

## How a text is chosen

One chooser serves every entry and `ChoosePreferredTitle`:

1. A user's overall pick.
2. Language by language: a user's pick for that language, then the sources in
   their order. `x-main` reads each source's main title. For an entry whose
   texts the store keeps, `user` texts come after the ranked sources, and an
   unranked own source is read between the two. A text in a lower language
   never beats the entry's own in a higher one. Synthesized titles are never
   chosen here, so every real title in a preferred language beats them.
3. For episodes, real titles in every preferred language before generic ones
   such as `Episode 5` or `第5話`.
4. When the own source is ranked and nothing was found, its own titles by the
   rule in step 2.
5. The entry's default, which is the synthesized one, in the first preferred
   language that has a form, for a plugin source's entry with no main title.
6. For an AniDB or Shoko episode or season with no title at all, a generic
   title synthesized the same way, never stored.

Overviews follow steps 1, 2, 4 and 5.

## Entries of each source

- **Plugin sources.** Series, seasons, episodes, films, collections, creators
  and characters read their texts through the manager. `PreferredTitle` is
  steps 1 to 4, else the synthesized default when there is one, else `null`, so
  `Title` falls back on the default. A person's
  `AlternativeNames` are the titles its source stored. Orderings and their
  groups read theirs the same way, the users' own under `user`; a group with
  no title is named by its generic season name. Tags, studios and networks
  keep their name on their own rows.
- **AniDB.** An anime's and episode's titles are stored under `anidb`; an
  episode with no English title is named by its generic title, synthesized when
  read. Descriptions and the names of characters, creators and tags stay on
  their rows. The names the core gives the tags it renames are `shoko` titles.
- **Shoko.** A name a user gives a series, episode or group is its `user`
  text preferred overall. Otherwise a series or episode reads its texts from
  AniDB, from the entries it is linked to and from plugin contributions, and a
  group reads its main series' (`IShokoGroup.HasCustomTitle`,
  `HasCustomOverview`).

## When a choice is worked out again

The manager caches what it chose until something it read changes: a text
written through the manager or a store, a row saved by its store, a link
added or removed, a series moved between groups, or the language settings. A
provider that saves an entry's row any other way calls `Invalidate`.

## Managing texts

A text is named by its kind and its `ID` (`GetTitleByID`, `GetOverviewByID`)
and says which entry owns it (`EntityID`). `GetAllTexts` lists stored texts by
filter; `GetTitles` and `GetOverviews` list what an entry is chosen from, the
row's default first and a Shoko entry's linked entries included;
`GetOrphanedEntries`, `PurgeOrphanedTexts` and `RemoveTexts` clean up. A
default kept on a row has no ID: it can be picked (which stores a copy) but
not changed, disabled or removed. `/api/v3/Text/Management` offers the same
to admins, with which chooser step picked an entry's text.

## Titles and overviews from linked films

For a Shoko series, a source answers with what was contributed under it, then
with one linked entry:

- **Series links.** The one stored series the anime is linked to. The
  overview is the part the anime covers: the season it starts in, or its one
  linked episode when it covers only specials.
- **Film links.** Only normal episodes count. When all are linked to the same
  film, that film speaks; when all are linked to films of one collection, the
  collection speaks. Anything else says nothing.
- **Older film entries.** When every normal episode is named for the whole
  work (`Complete Movie`, `OVA`, `TV Special`) or a part of it
  (`Part 1 of 2`), with at least one whole, the whole episode's film speaks.
  Without a linked whole episode, the linked parts follow the film rule above.

A movie, web release or TV special tries its film links first, every other
type its series links first; the second side speaks only when the first has
no entry, and titles and overview come from the same side. When neither has
an entry, the next source in the order answers.

An episode reads the first stored episode it is linked to, else its one film.
A film shared by several episodes of one anime speaks only for an episode
AniDB gave a stand-in name, keeping the label: `Part 1 of 2` becomes
`Vampire Hunter D (Part 1 of 2)`. The stand-in names are AniDB's English
`Complete <type>`, `Movie`, `OVA`, `OAD`, `ONA` and `TV Special`; `Part <n>`,
`Part <n> of <m>`, `Part I`; and `Episode <n>`, `Volume <n>` and the like, each
optionally ending in `(Part <n>)` or `(<name> Version)`.

---

# `IMetadataOrderingService`

An ordering groups a series' episodes in viewing order, such as a DVD or arc
order. `ISeries.Orderings`, `ISeries.PreferredOrdering`, `IEpisode.Orderings`
and `IEpisode.PreferredOrdering` read through this service, so most code only
calls it to write.

| Kind | Made by | IDs | `Type` |
|---|---|---|---|
| Default | The core, from the series' own seasons; never stored | the series' ID for a core source (`anidb://ordering/1396`), `default/<series ID>` for any other (`tmdb://ordering/default/1396`) | `Default` |
| Global | A plugin, through `SaveOrdering` | the plugin's own, under its source | anything but `Default` and `User` |
| Local | A user, through `CreateLocalOrdering` | given by the core, under `user` | `User` |

Every season names the ordering it is a group of in `OrderingID`: the
default one for a series' own seasons, and the stored or source ordering for
the others. Group IDs
share the season namespace of their source; keep them apart from season IDs.
At most one group is special (`IsSpecial`). A global ordering's source may give
a regular group its own season number (`MetadataOrderingGroupData.SeasonNumber`);
no other number is set. An episode's place is an `IEpisodeOrderingInformation`:

| Ordering | `SeasonNumber` | `EpisodeNumber` | `EpisodeType` |
|---|---|---|---|
| Default | the episode's own season | the episode's own number | the episode's own type |
| Stored | `0` for the special group, else the group's own number or its place, from 1 | the place among the group's home episodes, from 1 | `Special` in the special group, else `Episode` |

So read `EpisodeType` on the place, not `IEpisode.Type`, when an ordering is
in use. An episode in two regular groups has two places.

**Placed specials.** An episode in the special group and in a regular one is
a placed special: it stays a special, numbered in the special group, and its
place in the regular group only says where it airs. It has one place, in the
special group, carrying `AirsBeforeSeasonNumber` and `AirsBeforeEpisodeNumber`
(the regular episode of that group that follows it) or `AirsAfterSeasonNumber`
(none follows), and the regular episodes around it in `AirsAfterEpisodeID` and
`AirsBeforeEpisodeID`. The regular group numbers its home episodes without it,
`ISeason.Episodes` leaves it out, and `IOrdering.Episodes` lists it where it
airs. The default ordering places specials too, without moving them out of
season 0: a plugin's series by the `AirsBefore*`/`AirsAfter*` its provider
gave each season 0 episode, and an AniDB anime or a Shoko series by titles
such as `Episode 17.5`.

**Placements by source.** `GetEpisodePlacements` tells where each source
places a Shoko special among the regular episodes of its series, as one
`IEpisodePlacement` per source, AniDB's first. AniDB places it by its titles,
in the anime's default ordering. Every linked plugin source places it where
its linked episode airs in the default ordering of that episode's series, and
the regular episodes around it are followed back through the episode links
to the Shoko episodes of the special's series. A source whose neighbours
lead outside the series is left out; of two linked episodes of one source
placing it, the first in link order counts. Only default orderings are read,
never a stored one such as a TMDb episode group. `AirsAfterEpisodeID` and
`AirsBeforeEpisodeID` are `shoko://episode/<id>` IDs, `null` when the special
airs first or last. A regular episode has none. The optional `source` keeps
one source.

`SaveOrdering` replaces a global ordering whole and is refused under a core
source, for a `default/` ID, for a group ID another ordering holds, for an
episode of another series and for a second special group.
`CreateLocalOrdering`, `UpdateLocalOrdering` and `DeleteLocalOrdering` keep
the users' own. An ordering follows its series: purging or deleting a series,
of any source, removes every ordering of it with the choice of one and the
hidden flags of its episodes.

`SetPreferredOrdering` chooses the ordering a series uses (`null` for the
default), and `SetEpisodeHidden` hides an episode. Both are kept on the
entry's own row (Shoko series or episode, AniDB anime or episode, or the
plugin's in the series store, whose saves keep them), so an entry only a
resolver serves can have neither.

The TMDb plugin stores its episode groups as global orderings of its shows,
`tmdb://ordering/<collection ID>`, whose groups keep their season IDs and
TMDb's numbers. An
ordering carries images like
any entry, but `IImageManager` links images only to users' and plugins'
stored orderings and their groups, never to a default ordering or one a core
source keeps.

---

# `IMetadataOrderingTransferService`

`Export` writes orderings of one series or many, and `Import` reads them back
as local orderings; APIv3 exposes both to admins
(`Shoko.Server/API/v3/Orderings.md`). The file is JSON, alone or as
`manifest.json` in a zip beside the images it embeds:

```json
{
  "format": "shoko-orderings",
  "version": 1,
  "exportedAt": "2026-09-26T09:00:00Z",
  "server": { "version": "5.3.0" },
  "orderings": [
    {
      "series": { "anidbAnimeId": 69, "title": "One Piece" },
      "origin": "user://ordering/1f0c…",
      "name": "Arcs",
      "type": "user",
      "isPreferred": true,
      "networks": ["tmdb://network/1"],
      "images": [],
      "groups": [
        {
          "name": "East Blue",
          "isSpecial": false,
          "episodes": [{ "anidbAnimeId": 69, "anidbEpisodeId": 1234, "type": "episode", "number": 1 }],
          "images": [
            {
              "imageType": "primary",
              "isPreferred": true,
              "source": "tmdb",
              "resourceId": "/abc.jpg",
              "url": "https://image.tmdb.org/t/p/original/abc.jpg",
              "sha256": "…",
              "contentType": "image/jpeg",
              "file": "images/<sha256>.jpg"
            }
          ]
        }
      ]
    }
  ]
}
```

Series and episodes are named by AniDB IDs, so a file reads the same on any
server; an ordering of another source's series is written against the anime
most of its episodes are linked to. The import falls back on an episode's type
and number only within the ordering's own anime. An ordering's networks are
written by their full IDs, and an import links them, keeping a network this
server has not stored as a stub; one of a source the server does not know is
left out. An image is written by its remote source, resource ID and URL, and
its file embedded as `ImageMode` asks.

The import reads the whole file before writing. `ConflictMode` decides what
happens to a local ordering of the same name, `DryRun` writes nothing, and
`ApplyPreferred` restores which orderings were chosen. Images go through
`IImageManager`: a remote image is found or added under its source (its
download queued when missing), an embedded file is checked against its SHA-256
when `VerifyHashes` is set, and a file with no remote source is uploaded as a
user's image.

---

# `IShokoGroupManager`

A group nests arbitrarily: an optional parent, any number of child groups and
any number of series. Mind the direct and recursive views:

| Member | |
|---|---|
| `Series` | The series directly in the group, by air date |
| `AllSeries` | Those plus every descendant group's, by air date |
| `Groups` | The direct child groups |
| `AllGroups` | Every descendant group |
| `ParentGroup`, `AllParentGroups`, `TopLevelGroup` | The other direction |

None is cached: `AllSeries` walks the tree and sorts on every access, so read
it once into a local rather than in a loop.

`MainSeries` is the series a group borrows its title, overview and images
from: the configured one (`DefaultAnimeSeriesID`), then `MainAniDBAnimeID`,
then the earliest in `AllSeries`. `HasConfiguredMainSeries` tells the first
case apart. It throws `NullReferenceException` for a group with no series,
which the manager never leaves behind.

## Creating, updating and moving

`CreateGroup(GroupData)` and `UpdateGroup(group, GroupUpdateData)` return the
group; `SetMainSeries` and `MoveSeries` wrap `UpdateGroup`. A group must hold
at least one series, directly or below, or the call throws
`GenericValidationException` under `Series` and `Groups`. So does a parent
cycle (`ParentGroup`), a main series outside the group (`PreferredSeries`), or
a series or group that is not the server's own. A group handed to the manager
that is not the server's own throws `ArgumentException`.

```csharp
var group = groupManager.CreateGroup(new()
{
    Series = [firstSeries, secondSeries],
    MainSeries = firstSeries,
    ParentGroup = parentGroup,
});

groupManager.MoveSeries(thirdSeries, group);
```

Assigning `Name`, `Overview`, `ParentGroup` or `MainSeries` on
`GroupUpdateData` sets its `Has…` flag, even when assigning `null`, which is
how a field is cleared: `Name = null` goes back to the main series' title,
`ParentGroup = null` makes the group top-level. `Groups` and `Series` only
add: a series always belongs to exactly one group, so it is moved rather than
removed, and a group left empty is deleted.

A name or overview a user gives is the group's `user` text
(`HasCustomTitle`, `HasCustomOverview`). `RenameAllGroups()` works every
series' title and overview out again, which unnamed groups follow.

`DeleteGroup(group, deleteSeries, deleteFiles)` moves each series into a
group of its own, or deletes them (and their files with `deleteFiles`). The
group row goes through the empty-group cleanup, which raises its
`GroupRemoved`.

## Auto-grouping

`IsAutoGroupingEnabled`, `UseAutoGroupingRelationWeighting`,
`AutoGroupingRelationExclusions` and `AllowDissimilarTitleExclusion` are live
views over the server settings and save on assignment. Nothing applies
retroactively except `RecreateAllGroups()`, which pauses the queue, drops
every group, manual ones included, and rebuilds them. Never call it on a
schedule or from a plugin's install.

- `AllowDissimilarTitleExclusion` reads backwards: `true` turns **on** the
  title check that keeps related series with dissimilar titles apart.
- `AutoGroupingRelationExclusions` is stored as strings the grouping task
  parses into its own enum: `RelationType.SharedCharacters` and `MainStory`
  are ignored when grouping, and names only the task knows are kept in storage
  but not read back.

## Events

`GroupAdded`, `GroupUpdated`, `GroupRemoved`, `SeriesMoved` and
`GroupsRecreated`, split on `UpdateReason` as the metadata events are. One
move raises several (`GroupUpdated`, `SeriesMoved`, and `GroupRemoved` for an
emptied source group), so handlers must tolerate repeats.

---

# `IImageManager`

Every image is one row, and every link from an image to an entry is a
cross-reference row naming the entry by its `MetadataGuid`.
`GetEntityForImage` turns that ID back into an entry through
`IMetadataService.GetEntry`; see
[`../Image/CrossReferences/README.md`](../Image/CrossReferences/README.md).

## Reading

`IWithImages` has default methods that forward to the manager:

```csharp
// The image to show, whatever the entity has.
var poster = series.GetBestImageForType(ImageEntityType.Primary);

// Everything, or a filtered view.
var backdrops = series.GetImages(new() { ImageType = ImageEntityType.Backdrop });

// What the user pinned, on the entity or a linked entry.
var preferred = series.GetPreferredImageForType(ImageEntityType.Primary);
```

The best image is the preferred one (own or inherited), then the entity's
default, then the first enabled and available one, degrading to enabled and
desired, then enabled.

`ImageFilteringOptions` fields are tri-state `bool?` filters, except
`AsPrimaryImage`. `LinkedEntityImages` matters most: `null` (the default)
means `true` for `IShokoGroup`, `IShokoSeries`, a Shoko series' seasons and
`IShokoEpisode` and `false` for everything else, which is how a Shoko series
shows the posters of what it is linked to. Results come own images first, `User` and `Generated`
sources first within that, deduplicated on image and type. Pass `false` to see
only what an entity itself owns.

When linked entries' images are included, the entity inherits the image
preferred on a linked entry unless it prefers one of its own. `IImage.IsPreferred`
reads only the entity's own cross-reference, and `SetPreferredImageForEntity`
always writes the entity's own, never the (often shared) linked entry's.

`GetImageByID(Guid)` finds an image; `GetImageBySourceAndRemoteResourceID` is
how a provider checks for one it stored before.

## Adding and linking

`AddImage(ImageData)` registers a remote image, and `UploadImage` stores bytes
you have (`userSubmitted: false` for something generated). Both reject a MIME
type outside `AllowedMimeTypes`. Neither links anything:

```csharp
var image = imageManager.UploadImage(stream, "image/jpeg", userSubmitted: false);
var xref = imageManager.AddImageCrossReference(episode, image, new()
{
    ImageType = ImageEntityType.Backdrop,
    Source = MySources.Artwork,
    IsDesired = true,
});
```

`Source` defaults to `MetadataSource.User`, so pass your own for attribution;
`SetPreferredImageForEntity` creates its row under `User` too. Duplicates are
judged on image, image type and source, and throw
`ImageCrossReferenceExistsException` carrying the existing row. Preferring one
image demotes the entity's previous preferred image of that type.

### A template URL for your source

A remote image's URL is rebuilt as `string.Format(template, image.ResourceID)`
on every download. The core registers AniDB's template; any other source
needs `RegisterTemplateUrl` on every start before its first `AddImage`,
which otherwise throws `MissingImageSourceTemplateUrlException`. The default
lives in memory; a user's own template (`SetTemplateUrlForSource`) takes
precedence, and clearing it goes back to yours.

```csharp
imageManager.RegisterTemplateUrl(MySources.Artwork, "https://assets.example.com/art/{0}");
```

`{0}` is the whole varying remainder of the URL, and that remainder is the
image's identity, stored in 128 characters. Skip an image whose remainder is
longer; a truncated one downloads nothing.

## Downloading and purging

`DownloadImage` fetches now, `ScheduleDownloadOfImage` queues;
`ScheduleAutoDownloadsForEntity` and `ScheduleAllAutoDownloads` queue every
desired image. `GetOrphanedImages`, `PurgeImage`, `PurgeOrphanedImages` and
`ValidateAllImages` look after the files.

## Events

`ImageAdded` means a row exists; `ImageDownloaded` means bytes are on disk.
Every image and cross-reference event is raised on its own thread-pool task,
so handlers may run in any order and at once: read state back from the
manager, and catch inside the handler, since nothing observes what it throws.
