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
| `Containers/` | The `IWith…` interfaces entries are built from: titles, overviews, images, cast and crew, studios, tags, content ratings, resources and dates |
| `Shoko/` | Shoko's own entries: `IShokoSeries`, `IShokoSeason`, `IShokoEpisode`, `IShokoGroup` and `IShokoTag` |
| `Anidb/` | AniDB's entries, the MyList and AVDump models, and the [AniDB services](Anidb/Services/README.md) |
| `Tmdb/` | TMDB's entries, typed cross-references and orderings, and the [TMDB services](Tmdb/Services/README.md) |
| `CrossReferences/` | The link contracts, the link data the link store takes, and the CSV transfer options |
| `Storage/` | The typed stores a provider writes through, the `Metadata*Data` records they take, and `IMetadataCrossReferenceStore` |
| `Providers/` | The [provider contracts](Providers/README.md), `IMetadataResolver`, `IResourceResolver`, and the refresh options and pause status |
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
| `MetadataSource` | Where the data comes from | `shoko`, `user` and `generated` (local), `anidb` and `tmdb` (remote) |
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

The core serves `anidb` and `tmdb` and keeps `shoko`, `user` and `generated`
for data made on the server; no plugin provider may claim them
(`IMetadataProviderManager.ReservedSources`). Every other source, AniList
included, is a plugin's. See [choosing a source](Providers/README.md#choosing-a-source),
[entity types](Providers/README.md#entity-types) and
[identifiers](Providers/README.md#identifiers).

---

## Entries

An entry is anything with an ID (`IMetadata`). The kinds most code reads:

| Interface | Is | Implemented by |
|---|---|---|
| `ISeries` | A series, with its seasons, episodes and orderings | `IShokoSeries`, `IAnidbAnime`, `ITmdbShow`, a plugin source's stored series |
| `ISeason` | A season, or a group of an ordering (`OrderingID` set) | `IShokoSeason`, `IAnidbSeason`, `ITmdbSeason`, stored seasons, ordering groups |
| `IEpisode` | One episode | `IShokoEpisode`, `IAnidbEpisode`, `ITmdbEpisode`, stored episodes |
| `IMovie` | A film | `ITmdbMovie`, stored movies |
| `ICollection` | A collection of series or films | `IShokoGroup`, `ITmdbCollection`, stored collections |
| `IOrdering` | Another grouping of a series' episodes | Every series' default ordering, TMDB's episode groups, stored and local orderings |
| `ICreator`, `ICharacter` | The people credited through `ICast` and `ICrew` | Each source that has them |
| `ITag`, `IStudio`, `INetwork` | What describes, made or aired an entry | Each source that has them |

What an entry carries comes from the `Containers/` interfaces. Text about an
entry is an overview everywhere (`IWithOverviews`, `ITag.Overview`);
`IWithDescriptions` and `ITag.Description` are obsolete. A series also has
`ReleaseStatus`, `SourceMaterial`, `OriginalLanguageCode`, `Popularity`,
`FavoriteCount` and `Networks`, each `Unknown`, `null` or empty for a source
that does not say.

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
`LinkedMovies`). The AniDB and TMDB entries are also reachable as their own
types: `IShokoSeries.AnidbAnime`, `TmdbShows`, `TmdbMovies` and the rest.

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
`IWithTitles`). The rules are in
[`IMetadataTextManager`](Services/README.md#imetadatatextmanager) and
[managing texts](Services/README.md#managing-texts).

---

## Orderings and hidden episodes

An ordering groups a series' episodes in viewing order, such as a DVD order.
Every series has an unstored default one from its own seasons; a plugin saves
global orderings under its own source (`IMetadataOrderingService.SaveOrdering`),
TMDB's episode groups read as orderings of their shows, and users keep local
orderings under `user`.

| Read | From |
|---|---|
| Every ordering of a series, the default first | `ISeries.Orderings` |
| The one it uses | `ISeries.PreferredOrdering`, set with `SetPreferredOrdering` |
| An episode's places in them | `IEpisode.Orderings` and `IEpisode.PreferredOrdering` |
| Whether a user hid an episode | `IEpisode.IsHidden`, set with `SetEpisodeHidden` |

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
See [storing your data](Providers/README.md#storing-your-data).
`IMetadataRefreshService`, `IMetadataLinkingService`, `IMetadataPurgeService`
and `IMetadataCrossReferenceTransferService` work the same for every source,
TMDB included.

---

## What clients see

APIv3 serves every source's stored entries with no endpoint of the plugin's
own:

| Route | Serves |
|---|---|
| `/api/v3/Metadata/{source}/…` | A source's entries of every core kind, with their texts, images, credits, relations, suggestions, orderings and links; `PATCH …/CrossReferences` re-rates links (admins); `{kind}/{id}` answers any other kind but users and filters in a minimal form |
| `/api/v3/Metadata/Entry?id=` | Any entry by its full ID |
| `/api/v3/Metadata/Episode/Hidden` | Whether an episode of any source is hidden, and hiding one |
| `/api/v3/Metadata/Provider`, `/api/v3/Metadata/{source}/…` | The providers and their settings, and a source's status, search, CSV transfer and actions |
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

From a Shoko entry: `IShokoSeries.GetMetadataSeriesCrossReferences`,
`GetMetadataSeasonCrossReferences`, `GetMetadataEpisodeCrossReferences` and
`GetMetadataMovieCrossReferences` (and the matching ones on `IShokoSeason` and
`IShokoEpisode`), each taking an optional source. From the other end, the
`Metadata*CrossReferences` lists on `ISeries`, `ISeason`, `IEpisode` and
`IMovie` say which Shoko entries claim a provider's entry; a film is a level
of its own, so it only shows up in the film lists. `IVideoCrossReference`
carries the links of the anime and episode a file is linked to.
`IMetadataService` has the lookups by AniDB ID and
`GetCrossReferencesForProviderEntry`.

The extensions in `MetadataCrossReferenceExtensions` keep one source's links
of an entry, and their generic forms keep only the links whose provider entry
has the type asked for, resolving each link's entry to tell. The same
extensions read one source's entries out of a Shoko entry's `Linked*` lists
(`GetLinkedSeries`, `GetLinkedSeasons`, `GetLinkedEpisodes`,
`GetLinkedMovies`):

```csharp
IReadOnlyList<IMetadataSeriesCrossReference> links = series.GetSeriesCrossReferences(MetadataSource.TMDB);
IReadOnlyList<IMetadataSeriesCrossReference<ITmdbShow>> shows = series.GetSeriesCrossReferences<ITmdbShow>(MetadataSource.TMDB);
IReadOnlyList<ITmdbShow> linkedShows = shokoSeries.GetLinkedSeries<ITmdbShow>(MetadataSource.TMDB);
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

`IMovie` has the same four with `IMovie` as the base. `SuggestedBy` is the
same set read from the other end, and covers only the entries in the
collection. A Shoko series gives its anime's lists and those of every series
it is linked to; a source's own entry gives only that source's, narrowed to
its type:

```csharp
foreach (var suggestion in series.Suggestions)
    logger.LogInformation("{Source} suggests {ID}", suggestion.Source, suggestion.SuggestedID);

// AniDB only, with its vote counts.
foreach (var suggestion in series.AnidbAnime.Suggestions)
    logger.LogInformation("{Votes} votes, {Rating}% approval", suggestion.TotalVotes, suggestion.ApprovalRating);
```

| Entry | Its suggestions are | Which adds |
|---|---|---|
| `IAnidbAnime` | `IAnidbSuggestion` | `ApprovalVotes`, `TotalVotes` |
| `ITmdbShow`, `ITmdbMovie` | `ITmdbShowSuggestion`, `ITmdbMovieSuggestion` | nothing |
| A plugin source's series or movie | `ISuggestedMetadata<…>`, from `IMetadataSuggestionStore` | nothing the core defines |

`ISuggestedMetadata` is covariant, so an `IAnidbSuggestion` already is an
`ISuggestedMetadata<ISeries, ISeries>`; `IRelatedMetadata` is not. Only AniDB
fills a relation's `Verified`. A film's suggestions are on the film, reached
through `LinkedMovies`.

| Member | Meaning |
|---|---|
| `BaseID` / `SuggestedID` | The two ends. Always there. |
| `Base` / `Suggested` | The entries, when in the collection. `Suggested` is usually `null`, and that is normal. |
| `Kind` | `Recommended` or `Similar`. |
| `Order` | The source's ranking, best first from `0`, or `null`. |
| `ApprovalRating`, `Votes` | For a source that votes; `null` otherwise, never "nobody voted". |
| `Score` | A source's net score, may be negative. |

The ranking fields are not interchangeable: AniDB fills `ApprovalRating` and
`Votes`, TMDB only `Order`, and a scoring source such as AniList fills `Order`
and `Score`. Sort a mixed list within each source.

The core merges no directions on a source's behalf. A plugin serving a
symmetric source may merge both directions itself when it writes suggestions,
which recovers entries a paged list cuts off on one side. AniDB and TMDB are
not merged: their reverse entries carry different votes or ranks, or do not
exist.
