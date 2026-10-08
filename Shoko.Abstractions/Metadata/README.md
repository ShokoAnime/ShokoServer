# Metadata

`Shoko.Abstractions.Metadata` is the metadata half of the plugin contract: the
shapes every source's entries read back as, how an entry is named, how Shoko
links its entries to other sources' entries, and the services and stores a
plugin calls to read or write any of it. The server implements all of it; a
plugin reads through the entry interfaces and services, and a plugin serving a
source of its own also implements the provider contracts.

| You want to | Start at |
|---|---|
| Read series, episodes, movies and the rest | [Entries](#entries), then [`IMetadataService`](Services/README.md#imetadataservice) |
| Serve a source of your own | [`Providers/README.md`](Providers/README.md) |
| Keep a source's entries, people, tags and links | [Keeping a source's data](#keeping-a-sources-data) |
| Read, choose or add titles and overviews | [Titles and overviews](#titles-and-overviews) |
| Reorder a series' episodes, or hide some | [Orderings and hidden episodes](#orderings-and-hidden-episodes) |
| Link images to an entry | [`IImageManager`](Services/README.md#iimagemanager) and [`Image/CrossReferences/README.md`](Image/CrossReferences/README.md) |
| Publish when episodes air | [`Airing/README.md`](Airing/README.md) |
| Add external links to an entry | [`Resources/README.md`](Resources/README.md) |
| Walk relations, suggestions and links | [Relations, suggestions and cross-references](#relations-suggestions-and-cross-references) |

## What is in the folder

| Folder | Holds |
|---|---|
| (this one) | The entry interfaces, the identity types (`MetadataSource`, `MetadataEntityType`, `MetadataGuid`), `ITitle` and `IText`, relations and suggestions, `Resource`, `PartialDateOnly` and `FuzzyDateOnly` |
| `Containers/` | The `IWith…` interfaces entries are built from: titles, overviews, images, cast and crew, studios, tags, content ratings, resources, cross-source IDs, yearly seasons and dates |
| `Shoko/` | Shoko's own entries: `IShokoSeries`, `IShokoEpisode`, `IShokoGroup` and `IShokoTag` |
| `Anidb/` | AniDB's entries, the MyList and AVDump models, and the [AniDB services](Anidb/Services/README.md) |
| `CrossReferences/` | The link contracts, the link data the link store takes, and the CSV transfer options |
| `Storage/` | The typed stores a provider writes through, the `Metadata*Data` records they take, and `IMetadataCrossReferenceStore` |
| `Providers/` | The [provider contracts](Providers/README.md), `IMetadataResolver`, `IResourceResolver`, and the refresh options |
| `Services/` | The [services the server implements](Services/README.md) |
| `Search/`, `Matching/` | What a search, an auto-link preview and an episode match hand back |
| `Text/`, `Orderings/` | The data and options of the text manager and of the ordering transfer |
| `Image/` | Images, image candidates and image cross-references ([README](Image/CrossReferences/README.md)) |
| `Airing/` | Airing channels, schedules and episode airings ([README](Airing/README.md)) |
| `Resources/` | The [guide to `IResourceResolver`](Resources/README.md) (the interface is in `Providers/`) |
| `Events/`, `Enums/`, `Converters/` | Event arguments, enums, and converters for the identity types and partial dates |
| `Stub/` | `TitleStub` and `TextStub`, plain texts to pass in, and `ImageStub`, an image with its cross-reference |

---

## Sources, kinds and identifiers

Every entry names itself by a `MetadataGuid`: its source, its kind and the ID
the source gave it, written `<source>://<kind>/<id>`, such as
`anidb://series/1` or `shoko://video/<ED2K>+<file size>`. `IMetadata.ID`
carries it, and every reference from one entry to another is one too.

| Type | Names | The core registers |
|---|---|---|
| `MetadataSource` | Where the data comes from | `shoko`, `user` and `generated` (local), `anidb` (remote), and `tmdb`, which the bundled TMDB plugin serves |
| `MetadataEntityType` | What kind of entry it is | `series`, `season`, `episode`, `movie`, `collection`, `studio`, `network`, `channel`, `creator`, `character`, `tag`, `filter`, `video`, `user` and `ordering` |
| `MetadataGuid` | One entry: a source, a kind and an ID of 1 to 128 characters | nothing; any source and kind combine |

Sources and kinds are registries of shared instances:

- Each has a kebab-case `Value`, input `Aliases`, a display `Name` and an
  optional `Description`. Lookups ignore case and read `_` as `-`.
- `Register` works until every plugin's `IPlugin.Setup` has run.
- `Get` and `TryGet` find registered instances; `Parse` and `TryParse` also
  hand out an unregistered instance, so what an uninstalled plugin stored
  still reads.
- No value or alias may start with `unknown-`, and a source may not be called
  `provider`, `entry` or `episode`, which the `/api/v3/Metadata` routes keep.
- There is no `None` or `Unknown`: nothing is `null`.

The core serves `anidb` and keeps `shoko`, `user` and `generated` for data
made on the server; no plugin provider may claim them
(`IMetadataProviderManager.ReservedSources`). Every other source, TMDB and
AniList included, is a plugin's. `tmdb` is registered up front for the
bundled TMDB plugin, and is kept in the shared stores like any other. See
[choosing a source](Providers/README.md#choosing-a-source),
[entity types](Providers/README.md#entity-types) and
[identifiers](Providers/README.md#identifiers).

---

## Entries

An entry is anything with an ID (`IMetadata`). The kinds most code reads:

| Interface | Is | Implemented by |
|---|---|---|
| `ISeries` | A series, with its seasons, episodes and orderings | `IShokoSeries`, `IAnidbAnime`, a plugin source's stored series |
| `ISeason` | A season, a group of the ordering `OrderingID` names | Every series' own seasons, stored seasons, the groups of every other ordering |
| `IEpisode` | One episode | `IShokoEpisode`, `IAnidbEpisode`, stored episodes |
| `IMovie` | A film | Stored movies |
| `ICollection` | A collection of series or films | `IShokoGroup`, `IMovieCollection`, stored collections |
| `IMovieCollection` | A collection of films, with its `Movies` | Stored movie collections |
| `IOrdering` | Another grouping of a series' episodes | Every series' default ordering, stored and local orderings |
| `ICreator`, `ICharacter` | The people credited through `ICast` and `ICrew` | Each source that has them |
| `ITag`, `IStudio`, `INetwork` | What describes, made or aired an entry | Each source that has them |

A series, season, episode, ordering and place in an ordering also come typed:
`ISeries<TSeries, TEpisode>`, `ISeason<TSeries, TEpisode>`,
`IEpisode<TSeries, TEpisode>`, `IOrdering<TSeries, TEpisode>` and
`IEpisodeOrderingInformation<TSeries, TEpisode>` hand back the series and
episodes as their own types, and the base of a series' suggestions.
`IShokoSeries` is an `ISeries<IShokoSeries, IShokoEpisode>`, `IAnidbAnime`
an `ISeries<IAnidbAnime, IAnidbEpisode>`, and a plugin source's stored
entries use `ISeries` and `IEpisode`.
Films come typed the same way: `IMovie<TMovie>` types the base of a film's
suggestions and its `Collection`, and `IMovieCollection<TMovie>` its
`Movies`. There is no season type: a group of any ordering but the default
one is that ordering's group, not one of the source's seasons, so every
season reads as `ISeason<TSeries, TEpisode>` of its series' types. Every
season has its `LinkedSeasons` and `LinkedMovies`, read off its
cross-references.

What an entry carries comes from the `Containers/` interfaces. Text about an
entry is an overview everywhere (`IWithOverviews`, `ITag.Overview`). A series
also has `ReleaseStatus`, `SourceMaterial`, `OriginalLanguageCode`,
`ProductionCountries`, `Popularity`, `FavoriteCount` and `Networks`, each
`Unknown`, `null` or empty for a source that does not say. A film has
`OriginalLanguageCode`, `ProductionCountries` and the movie collection it is
part of (`CollectionID`, `Collection`). Genres and keywords are tags, told
apart by `ITag.Kind`. The IDs other sites give an entry, such as IMDb's, are
in `CrossSourceIDs`, under each site's own source.

Series, seasons, episodes, films, collections, orderings, creators and
characters say when they were first stored (`CreatedAt`) and when their data
last changed (`LastUpdatedAt`). Every implementer gives both; a season built
from its series, such as an AniDB or Shoko season, takes the series' dates.
Series, films, collections, people, studios and networks also carry
`LastRefreshedAt`, when their provider last refreshed them: the core keeps it
for every plugin source, seasons and episodes read their series', and Shoko's
own entries have none.

`IMetadataService.GetEntry` turns any `MetadataGuid` back into its entry, and
everything holding an ID goes through it: the core's sources from the core's
tables, any other source from the `IMetadataResolver` that took the ID's
source and kind, then the typed stores and the ordering service. No provider
is asked, so a disabled plugin's stored entries still resolve. See
[resolving your own kinds](Providers/README.md#resolving-your-own-kinds).

A Shoko series is built on its AniDB anime and reads its cast, crew, studios,
content ratings, resources, networks, relations and suggestions from the anime
and the entries it is linked to (`IShokoSeries.LinkedSeries`, which starts
with the anime, and `LinkedMovies`; `IShokoEpisode.LinkedEpisodes` and
`LinkedMovies`). The AniDB anime is also reachable as its own type,
`IShokoSeries.AnidbAnime`.

---

## Titles and overviews

Every title and overview of every entry goes through `IMetadataTextManager`.
An entry's `Titles`, `PreferredTitle`, `Overviews` and `PreferredOverview`
read through it, and one chooser picks what the user sees, by the user's picks
and the language and source order in the settings.

| Who | Writes through |
|---|---|
| A provider, for its own entries | The `Titles` and `Overviews` of the `Metadata*Data` it saves, or `SetTitles` and `SetOverviews` |
| A plugin, on any other entry | `SetTitles` and `SetOverviews` under its own source; `RemoveContributions` takes them back |
| A user | `AddText`, `UpdateText`, `EnableText`, `SetPreferredTitle`, `SetPreferredOverview` and the unsetting members, under `user` |

`ChoosePreferredTitle` and `ChoosePreferredOverview` apply the same rules to
any list of texts, such as a video chapter's names (`IChapterInfo` is an
`IWithTitles`). `GetLanguageOrder` hands a provider the language order itself,
for choosing which translations to store.

An entry's main title (`TitleType.Main`) is its default. A plugin source's
entry stored without one gets a synthesized default, listed first in `Titles` and
never stored (`ITitle.IsSynthesized`): an episode's or season's generic name,
such as `Episode 5`, `Staffel 2` or `Specials`, or `<source> <kind> <id>` for
any other kind. For an episode or season whose only name is generic, storing
nothing is the better choice, as the core synthesizes it in the user's languages;
the stores leave such titles out when they carry the entry's own number. The rules are in
[`IMetadataTextManager`](Services/README.md#imetadatatextmanager) and
[managing texts](Services/README.md#managing-texts).

---

## Orderings and hidden episodes

An ordering groups a series' episodes in viewing order, such as a DVD order.
Every series has an unstored default one from its own seasons; a plugin saves
global orderings under its own source (`IMetadataOrderingService.SaveOrdering`),
such as TMDB's episode groups, and users keep local orderings under `user`.

| Read | From |
|---|---|
| Every ordering of a series, the default first | `ISeries.Orderings` |
| The one it uses | `ISeries.PreferredOrdering`, set with `SetPreferredOrdering` |
| An episode's places in them | `IEpisode.Orderings` and `IEpisode.PreferredOrdering` |
| The ordering or place an entry is presented in | `ISeries.CurrentOrdering` and `IEpisode.CurrentOrdering` |
| Whether a user hid an episode | `IEpisode.IsHidden`, set with `SetEpisodeHidden` |

A place in an ordering (`IEpisodeOrderingInformation`) says where an episode
sits there: its group, season and episode number and type. A special placed
among the regular episodes stays a special and says where it airs
(`AirsBefore…`, `AirsAfter…`), in the default ordering too. An ordering also
lists the `Networks` it follows.

Any ordering but the default one presents its series and episodes in its own
numbering. Read untyped, an ordering's `Series` has the ordering's groups as
its `Seasons` and its episodes as its `Episodes`; those episodes, a group's
`Episodes` and a place's `Episode` are numbered and typed by their place,
with the episode's own ID, texts and images. An episode with no main title
gets synthesized titles in that numbering, such as `Episode 7`. Their
`CurrentOrdering` leads back to the ordering or the place, and a source's own
series and episodes answer with the default ordering. The typed members, such
as `IOrdering<TSeries, TEpisode>.Series` and the typed place's `Episode`,
always give the source's own entries.

See [`IMetadataOrderingService`](Services/README.md#imetadataorderingservice)
and [`IMetadataOrderingTransferService`](Services/README.md#imetadataorderingtransferservice).

---

## Keeping a source's data

| Store | Holds |
|---|---|
| `IMetadataSeriesStore`, `IMetadataMovieStore`, `IMetadataCollectionStore` | A source's series with their seasons and episodes, its movies, and its collections, each saved whole |
| `IMetadataPeopleStore` | Creators and characters, and the cast and crew of each entry |
| `IMetadataTagStore` | Tags, genres and keywords, and the entries they apply to |
| `IMetadataStudioStore` | Studios and networks, and the entries they made or aired |
| `IMetadataRelationStore`, `IMetadataSuggestionStore` | A source's relations and suggestions between its own entries |
| `IMetadataCrossReferenceStore` | The links between AniDB and any source's entries |

The typed stores write only under a plugin's source; what only a plugin keeps
goes in [a database of its own](../Plugin/README.md#a-database-of-your-own).
A credit or link naming a creator, character, studio or network not stored
yet keeps a stub of it, which the provider taking its kind refreshes; see
[refreshing people, studios and networks](Providers/README.md#refreshing-people-studios-and-networks).
See [storing your data](Providers/README.md#storing-your-data).
`IMetadataRefreshService`, `IMetadataLinkingService`, `IMetadataPurgeService`
and `IMetadataCrossReferenceTransferService` work the same for every source.

---

## What clients see

APIv3 serves every source's stored entries with no endpoint of the plugin's
own:

| Route | Serves |
|---|---|
| `/api/v3/Metadata/{source}/…` | A source's entries of every core kind, with their texts, images, credits, relations, suggestions, orderings and links; `PATCH …/CrossReferences` re-rates links (admins); `{kind}/{id}` answers any other kind but users and filters in a minimal form |
| `/api/v3/Metadata/Entry?id=` | Any entry by its full ID |
| `/api/v3/Metadata/Episode/Hidden` | Whether an episode of any source is hidden, and hiding one |
| `/api/v3/Metadata/Provider`, `/api/v3/Metadata/{source}/…` | The providers, their icons and settings, and a source's status, search, CSV transfer and actions |
| `/api/v3/Metadata/Source` | The sources, each source's icon, and its provider order per kind (admins change it) |
| `/api/v3/Metadata/ImageContributor` | The image contributors, their icons, and the pairs each one is on for |
| `/api/v3/Series/{seriesID}/Metadata/{source}/…`, `/api/v3/Episode/{episodeID}/Metadata/{source}/…` | The links of one Shoko series or episode to a source, and linking and matching them |

A Shoko series or episode asked for with `includeDataFrom=<source>` gains a
`Sources` block; see [what clients see](Providers/README.md#what-clients-see).

---

## Relations, suggestions and cross-references

Three ways one entry points at another, kept deliberately apart:

| | What it is | Whose claim | Contract |
|---|---|---|---|
| **Relation** | An authored fact: this is the sequel of that | the source's | `IRelatedMetadata` |
| **Suggestion** | An opinion: users who liked this suggested that, or the two resemble each other | its users' or its algorithm's | `ISuggestedMetadata` |
| **Cross-reference** | An identity: this AniDB entry and that entry are the same work | **Shoko's** | `IMetadataCrossReference` |

Relations form a symmetric graph meant to be walked (`Reversed` gives the
other side). Suggestions are one-directional and mostly point at things you do
not have. A cross-reference is the match Shoko made, which is why it carries a
`MatchRating` and is the only one you can correct. Each names both ends by
`MetadataGuid`, so the IDs are there even when the entry behind one is not.

### Cross-references

Split by the level the link is made at:

| Contract | What it claims | Adds |
|---|---|---|
| `IMetadataSeriesCrossReference` | The whole anime is the provider's series. | nothing |
| `IMetadataEpisodeCrossReference` | One episode is the provider's episode. | `AnidbEpisodeID`, `ShokoEpisode`, `ProviderParentID`, `SeasonID`, `SeasonNumber`, `EpisodeNumber` |
| `IMetadataMovieCrossReference` | The anime is the film, kept against the episode standing for it. | `AnidbEpisodeID`, `ShokoEpisode` |
| `IMetadataSeasonCrossReference` | The anime covers a season of the provider's show. | `ProviderParentID`, `SeasonNumber` |

Every level carries `AnidbAnimeID`, `Source`, `EntityType`, `MatchRating`, an
`Ordering` among the links of the same Shoko entry, and `ShokoSeries` when it
is in the collection. `ProviderID` is the entry the link points at, or `null`
when the AniDB entry is deliberately on no entry of the source. A film
claiming a whole anime (`MetadataSeriesLinkRequest` with `EntityType` set to
`movie`) is kept as a series-level link and reads back as
`<source>://movie/<film ID>`.

An episode link keeps its season (`SeasonID`, `SeasonNumber`) and
`EpisodeNumber` on the link itself, so it is useful with nothing of its source
cached; what a write leaves out is filled from the series store, and later
saves of the series keep them in step. A season link is never stored: it is
worked out from the episode links' seasons. Every source's links live in the
same tables through `IMetadataCrossReferenceStore`, which also records the
provider that wrote each (`WrittenBy`), though a link belongs to its source.
Each contract has an `<out TProvider>` variant narrowing `Provider`.

#### Reading and writing links

From a Shoko episode: `IShokoEpisode.GetMetadataSeriesCrossReferences`,
`GetMetadataEpisodeCrossReferences` and `GetMetadataMovieCrossReferences`, each
taking an optional source. From the other end, the
`Metadata*CrossReferences` lists on `ISeries`, `ISeason`, `IEpisode` and
`IMovie` say which Shoko entries claim a provider's entry; a film is a level
of its own, so it only shows up in the film lists. `IVideoCrossReference`
carries the links of the anime and episode a file is linked to.
`IMetadataService` has the lookups by AniDB ID and
`GetCrossReferencesForProviderEntry`.

The extensions in `MetadataCrossReferenceExtensions` keep one source's links
of an entry, and their generic forms keep only the links whose provider entry
has the type asked for, resolving each link's entry to tell. The same
extensions read one source's entries out of the `Linked*` lists of a Shoko
entry or a season (`GetLinkedSeries`, `GetLinkedSeasons`, `GetLinkedEpisodes`,
`GetLinkedMovies`):

```csharp
IReadOnlyList<IMetadataSeriesCrossReference> links = series.GetSeriesCrossReferences(MetadataSource.TMDB);
IReadOnlyList<IMetadataSeriesCrossReference<ISeries>> shows = series.GetSeriesCrossReferences<ISeries>(MetadataSource.TMDB);
IReadOnlyList<ISeries> linkedShows = shokoSeries.GetLinkedSeries<ISeries>(MetadataSource.TMDB);
```

Links are written through `IMetadataLinkingService`, with the checks a
person's change needs, or straight through the store's `Merge…Links`, which
police nothing.

### Reading relations and suggestions

Both hang off the entries. Every `ISeries` has:

```csharp
IReadOnlyList<IRelatedMetadata<ISeries, ISeries>> RelatedSeries { get; }
IReadOnlyList<IRelatedMetadata<ISeries, IMovie>> RelatedMovies { get; }
IReadOnlyList<ISuggestedMetadata<ISeries, ISeries>> Suggestions { get; }
IReadOnlyList<ISuggestedMetadata<ISeries, ISeries>> SuggestedBy { get; }
```

`IMovie` has the same four with `IMovie` as the base. The typed
`ISeries<TSeries, TEpisode>` and `IMovie<TMovie>` type the base end only, as
`ISuggestedMetadata<TSeries, ISeries>` and `ISuggestedMetadata<TMovie, IMovie>`;
the suggested end stays `ISeries` or `IMovie`. `SuggestedBy` is the same set
read from the other end, and covers only the entries in the collection. A
Shoko series gives the lists of its anime and of every series it is linked
to, based on itself (or, for `SuggestedBy`, on the Shoko series standing for
the suggesting entry); a source's own entry gives only that source's:

```csharp
foreach (var suggestion in series.Suggestions)
    logger.LogInformation("{Source} suggests {ID}", suggestion.Source, suggestion.SuggestedID);

// Only the suggestions a source voted on, with the counts.
foreach (var suggestion in series.Suggestions)
    if (suggestion.HasVotes)
        logger.LogInformation("{Approved} of {Votes} votes, {Rating}% approval", suggestion.ApprovalVotes.Value, suggestion.Votes.Value, suggestion.ApprovalRating);
```

| Entry | Its suggestions are |
|---|---|
| `IAnidbAnime` | `ISuggestedMetadata<IAnidbAnime, ISeries>` |
| `IShokoSeries` | `ISuggestedMetadata<IShokoSeries, ISeries>` |
| A plugin source's series or movie | `ISuggestedMetadata<…>`, from `IMetadataSuggestionStore` |

`ISuggestedMetadata` is covariant, so an AniDB suggestion already is an
`ISuggestedMetadata<ISeries, ISeries>`; `IRelatedMetadata` is not. Only AniDB
fills a relation's `Verified`. A film's suggestions are on the film, reached
through `LinkedMovies`.

| Member | Meaning |
|---|---|
| `BaseID` / `SuggestedID` | The two ends. Always there. |
| `Base` / `Suggested` | The entries, when in the collection. `Suggested` is usually `null`, and that is normal. |
| `Kind` | `Recommended` or `Similar`. |
| `Order` | The source's ranking, best first from `0`, or `null`. |
| `ApprovalVotes`, `Votes` | The votes in favour and in total, for a source that votes; `null` otherwise, never "nobody voted". `HasVotes` checks both. |
| `ApprovalRating` | The percentage in favour, worked out from the counts unless the source gives its own. `HasApprovalRating` checks it. |
| `Score` | A source's net score, may be negative. `HasScore` checks it. |

The ranking fields are not interchangeable: AniDB fills `ApprovalVotes`,
`Votes` and so `ApprovalRating`, TMDB only `Order`, and a scoring source such as AniList fills `Order`
and `Score`. Sort a mixed list within each source.

The core merges no directions on a source's behalf. A plugin serving a
symmetric source may merge both directions itself when it writes suggestions,
which recovers entries a paged list cuts off on one side. AniDB and TMDB are
not merged: their reverse entries carry different votes or ranks, or do not
exist.
