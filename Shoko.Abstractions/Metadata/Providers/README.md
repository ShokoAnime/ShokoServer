# Metadata providers

A metadata provider supplies series, episodes and movies from a source the core
does not serve itself. The core owns the links between your entries and AniDB,
the stores your entries are kept in, and the jobs that call you: it decides
when an entry is due and keeps one refresh of it at a time. You own the
refresh itself: fetch from your source and write into the stores.

| Interface | Implement it to |
|---|---|
| `IMetadataProvider` | Describe yourself: name, source, defaults, whether you are configured, a job limit and an optional clean-up. Never implemented alone. |
| `IMetadataSeriesProvider` | Refresh a linked series with its seasons and episodes into the stores. |
| `IMetadataSeriesLinkingProvider` | Let users search your series and match their episodes. |
| `IMetadataMovieProvider` | Refresh a linked movie into the stores. |
| `IMetadataMovieLinkingProvider` | Let users search your movies. |
| `IMetadataCollectionProvider` | Refresh a stored collection into the stores. |
| `IMetadataAutoLinkingProvider` | Work out what an anime is on your own, for the core to link. |
| `IMetadataImageProvider` | Offer the images your source has for its entities. |
| `IPausableMetadataProvider` | Say you cannot take work right now, so your jobs wait. |

A provider implementing none of `IMetadataSeriesProvider`,
`IMetadataMovieProvider` and `IMetadataAutoLinkingProvider` is dropped at
registration. Two contracts stand beside the providers, each declaring what it
covers with a `MetadataEntityScope`: `IMetadataResolver`
([resolving your own kinds](#resolving-your-own-kinds)) and
`IMetadataImageContributor`
([adding images to other sources' entries](#adding-images-to-other-sources-entries)).

A provider has no getters. The core reads every entry back from the stores
([storing your data](#storing-your-data)), so a disabled or uninstalled
plugin's data still reads, and a source that only links reads nothing.

## Choosing a source

A provider answers for exactly one `MetadataSource`, returned from `Source`
and read once at registration; a plugin serving several sources defines one
provider per source.

- **The core's sources are refused.** `anidb` and `tmdb` are served by the
  core, and `shoko`, `user` and `generated` name no provider. The full list is
  `IMetadataProviderManager.ReservedSources`, and it can grow.
- **Any other source is yours to claim** when your data is what it names.
  AniList is one of them, served by its own plugin like any other.
- **There is no catch-all source.** Register your own with
  `MetadataSource.Register` and pass that instance wherever a source is asked
  for.

A source's `Value` is kebab-case (`anilist`) and is what settings and stored
JSON hold. Lookups ignore case and read `_` as `-`, and `Aliases` are other
accepted spellings. The API still sends the spelling the old `DataSource`
enum used (`AniDB`, `TMDB`, `AniList`) while that spelling reads back as the
same source, so register it as an alias if it differs from your value by more
than case or underscores. `Name` and `Description` are for display only. No
value or alias may start with `unknown-`.

### Registering the sources you own

`MetadataSource.Register` works until every plugin's `IPlugin.Setup` has run
and throws `InvalidOperationException` after, so keep your sources in a static
class whose **static constructor** registers them, and touch it from
`IPluginServiceRegistration.RegisterServices`:

```csharp
public static class MySources
{
    static MySources()
    {
        AniList = MetadataSource.Register("AniList", "anilist", description: "The anime and manga database at anilist.co.");
    }

    public static MetadataSource AniList { get; }
}

public class MyServiceRegistration : IPluginServiceRegistration
{
    public static void RegisterServices(IServiceCollection services, IApplicationPaths applicationPaths)
    {
        // Runs the static constructor, and so the registration.
        _ = MySources.AniList;
    }
}
```

A static field initialiser without a static constructor may run late, after
registration has closed, and throw.

Registering a value twice returns the same instance and merges new aliases,
so two plugins serving one source can both register it; a value or alias that
belongs to another source is refused. Outside text becomes a source through
`MetadataSource.TryGet` (registered only) or `TryParse` (any valid value).
There is no `None`: a missing source is `null`.

#### Sources that depend on a setting

Registration has closed by the time a setting changes, so read the settings
and register from `Setup` at the latest, and raise a restart reason when a
user changes them:

```csharp
public void Setup(IServiceProvider serviceProvider)
{
    var systemService = serviceProvider.GetRequiredService<ISystemService>();
    var provider = serviceProvider.GetRequiredService<ConfigurationProvider<MyConfiguration>>();
    var registered = provider.Load().ExtraSources;
    foreach (var value in registered)
        MetadataSource.Register(value, value);

    IRestartRequirement? restart = null;
    provider.Saved += (_, e) =>
    {
        if (e.Configuration.ExtraSources.SequenceEqual(registered))
        {
            restart?.Dispose();
            restart = null;
        }
        else
        {
            restart ??= systemService.RequireRestart<MyPlugin>("The extra sources changed and are registered on the next start.");
        }
    };
}
```

See [restart reasons](../../Core/Services/README.md#restart-reasons).

### Supporting a source another plugin owns

Look it up on every access rather than registering or caching it, since the
owner may not have registered it yet when your class is first touched:

```csharp
public static MetadataSource? Example => MetadataSource.TryGet("example", out var source) ? source : null;
```

### Which provider answers

Two plugins may claim one source; the core refuses neither. Each source and
entity type is answered by one provider at a time, and every claimant gets to
clean up after a purge. What was never decided goes to the first provider
registered for the source, the core's before any plugin's: every entity type
no earlier provider was given, and auto-linking for the first one implementing
`IMetadataAutoLinkingProvider` (starting from `AutoLinkByDefault` and
`AutoLinkRestrictedByDefault`). The choice is saved, so a provider installed
later never takes over by itself, and an admin's decisions are always kept.
When the assigned provider disappears, its assignments go to the first
provider still claiming the source. Being turned on queues nothing: the whole
library is searched only when somebody asks
(`IMetadataRefreshService.AutoSearchAll`, the "Search for Metadata Matches"
action or `POST /api/v3/Metadata/{source}/Action/AutoSearchAll`).

Return `false` from `IsConfigured` while you lack what you need, such as an
API key, with the reason in `NotConfiguredReason`. Auto-linking then skips
you quietly, and a person's search is answered with `503` naming you and the
reason. Do not report a missing key as a pause. A call that finds out on its
own throws `MetadataProviderNotConfiguredException`, answered the same way.

The core does not police writes to `IMetadataCrossReferenceStore`: plugins
sharing a source are left to get along with each other.

## Entity types

A `MetadataEntityType` is shaped like a source: a kebab-case `Value`,
`Aliases`, a display-only `Name` and an optional `Description`, with the old
`DataEntityType` spellings (`Show`, `Franchise`) sent by the API. The core
registers `series`, `season`, `episode`, `movie`, `collection`, `studio`,
`network`, `channel`, `creator`, `character`, `tag`, `filter`, `video`, `user`
and `ordering`. There is no `Unknown`.

A plugin may register a kind of its own, such as a media server's `library`,
the same way as a source: a static constructor calling
`MetadataEntityType.Register`, touched from `RegisterServices`, or from
`Setup` when it depends on a setting. Only the core's kinds are served by
providers, stored in the typed stores and resolved by the core; anything else
needs a resolver of your own.

### Resolving your own kinds

`IMetadataService.GetEntry` turns any `MetadataGuid` back into its entry, and
the image manager, the airing schedules and the API all go through it. For a
source that is not the core's it asks the `IMetadataResolver` that took the
ID's source and kind first, then the stores and the ordering service:

```csharp
public class LibraryResolver(LibraryCache libraries) : IMetadataResolver
{
    public string Name => "Example libraries";

    public MetadataEntityScope Scope { get; } = MetadataEntityScope.ForSource(ExampleSources.Example, ExampleKinds.Library);

    // Only IDs in the scope reach this; null leaves the ID to the stores.
    public IMetadata? GetEntry(MetadataGuid id)
        => libraries.TryGet(id.ID, out var library) ? library : null;
}
```

`MetadataEntityScope` is a set of source and kind pairs, built with `Single`,
`ForSource`, `ForSources` or `FromPairs`. The core finds the resolver in your
assembly and reads its scope once. Each pair goes to the first resolver
claiming it, in plugin load order; a pair on a core source, or one already
taken, is refused with an error in the log. A resolver that returns `null` or
throws leaves the ID to the stores. It is called on every lookup, so answer
from memory, never from a remote service. An entry it answers that has images
is found by the image manager too.

## Identifiers

A `MetadataGuid` is `<source>://<entity type>/<id>`, such as
`anidb://series/1` or `tmdb://episode/a/b`: parsing splits at the first `://`
and then the first slash, so the ID may hold slashes. IDs are 1 to 128
characters; `IsNumericID`, `TryGetNumericID<T>` and `GetNumericID<T>` read a
plain non-negative integer. `MetadataGuid.For("anidb", "series", "1")` takes
registered values only.

The ID part is always the source's own ID: a Shoko row's local ID, a video's
`<ED2K>+<size>`, an airing channel's GUID, an AniDB or TMDB ID, or the ID you
stored an entry under. Where it is an integer it is also exposed as
`LocalID`, `AnidbID` or `TmdbID`. Every reference between entries is a
`MetadataGuid` too (`SeriesID`, `SeasonID`, both ends of a relation, a link's
`ProviderID`), built from the stored columns. A cast or crew credit has no
identifier of its own.

## Storing your data

The core keeps your shared data in its own tables, and you write it through
these services; what only your source has goes in
[a database of your own](../../Plugin/README.md#a-database-of-your-own),
which is not open until the server has started.

| Service | Holds |
|---|---|
| `IMetadataCrossReferenceStore` | Your links to AniDB, in the shared cross-reference tables. |
| `IMetadataSeriesStore` | Your series, with their seasons and episodes. |
| `IMetadataMovieStore` | Your movies. |
| `IMetadataCollectionStore` | Your collections, and the series and movies they gather. |
| `IMetadataPeopleStore` | Characters and creators, and who is credited on which entry. |
| `IMetadataTagStore` | Tags, genres and keywords, and which entries they apply to. |
| `IMetadataStudioStore` | Studios and networks, and which entries they worked on or aired. |
| `IMetadataRelationStore` | Relations your source authored between its entries. |
| `IMetadataSuggestionStore` | Suggestions your source makes between its entries. |
| `IMetadataOrderingService` | Your orderings of any source's series. |

The stores and the `Metadata*Data` records they take are in
`Shoko.Abstractions.Metadata.Storage`. They name every entry by its
`MetadataGuid`, write only under a plugin's source (a core source throws
`ArgumentException`), and refuse a reference to another source than the
entry being written.

**Entries.** `SaveSeries` takes a `MetadataSeriesData` whole: the series, its
titles and overviews, and every season and episode. It replaces what was
stored, so a season or episode left out is removed, with what the other stores
hold for it and the episode links naming it; do not save an empty episode list
when your source briefly answers none. An episode names its season by
`MetadataGuid`, one of the series' own. `SaveMovie` and `SaveCollection`
work the same way. Each returns how many entries it added, changed or
removed, and raises the `IMetadataService` events only for what changed.
`RemoveSeries`, `RemoveMovie` and `RemoveCollection` take an entry out with
everything the stores hold for it; `RemoveSeries` also removes every ordering
of the series.

**Links follow the store.** When a save adds or changes episodes, the core
copies their seasons and numbers onto the episode links naming them. A link to
an episode not stored yet is kept as written.

**Resources and ratings.** Series, episodes and movies carry `Resource` links;
fill `Resource.ID` with the site's bare ID (`tt0123456` for IMDb) whenever the
site has IDs. Series and movies carry one `MetadataContentRatingData` per
country.

**People.** `SetCast` and `SetCrew` make an entry's list exactly the credits
given. A cast credit is known by its character, creator and language, a crew
credit by its creator and job. Alternative names are stored as `Synonym`
titles, and birthdays and days of death are `FuzzyDateOnly`, where year, month
and day may each be unknown (`--07-04`).

**Tags, studios and networks.** A tag's `Kind` is a descriptive tag, a genre
or a keyword; keep loose keywords apart with `TagKind.Keyword`.
`IMetadataStudioStore` keeps networks as it keeps studios (`SaveNetworks`,
`SetNetworks`, `RemoveNetworks`).

**Orphans.** People, studios and networks are never removed on the spot:
their store stamps them when they lose their last use and clears the stamp
when they are used again. The core purges those unused for longer than the
admin's setting (a week unless changed) daily, so you may save them before or
after the entries naming them. `RemoveOrphaned` on either store is that purge
for one source; pass a cutoff a day or more in the past.

**Orderings.** `IMetadataOrderingService.SaveOrdering` keeps a global
ordering of any source's series whole, under your source, with the IDs you
give it and its groups. A group reads back as `<source>://season/<group ID>`,
so keep group IDs apart from season IDs, and set `IsSpecial` on at most one
group; you never number anything. IDs may not start with `default/`. See
[`IMetadataOrderingService`](../Services/README.md#imetadataorderingservice).

**Text on other entries** goes through `IMetadataTextManager.SetTitles` and
`SetOverviews` under your source, until you replace it or call
`RemoveContributions`. Your stored entries also speak for the Shoko entries
linked to them, by
[fixed rules](../Services/README.md#titles-and-overviews-from-linked-films):
save a collection with its films as members when an anime's episodes are
linked to several of your films.

## Refreshing

| Call | Asked for | Writes through |
|---|---|---|
| `IMetadataSeriesProvider.RefreshSeries` | Each series linked to an anime on your source, or that its episode links point into | `IMetadataSeriesStore.SaveSeries` |
| `IMetadataMovieProvider.RefreshMovie` | Each film linked to an anime, whole or through an episode | `IMetadataMovieStore.SaveMovie` |
| `IMetadataCollectionProvider.RefreshCollection` | Each stored collection in the library refresh, or one asked for by `RefreshEntry` | `IMetadataCollectionStore.SaveCollection` |

The core writes nothing of yours for you: fetch, then write the entry, its
cast and crew, and its tags, studios, networks, relations and suggestions.
When your source no longer has an entry, keeping or removing it is up to you.
A collection enters the store when you save it during a series or film
refresh.

`MetadataRefreshOptions` carries TMDB's switches (`DownloadImages`,
`DownloadCrewAndCast`, `DownloadAlternateOrdering`, `DownloadNetworks`,
`DownloadCollections`, `null` meaning your settings; ignore what means nothing
for you), `QuickRefresh` (skip what is costly; no images, and it does not count
as a refresh), `Reason` (`Scheduled`, `Linked` or `Requested`),
`LastRefreshedAt` and `AnidbAnimeID`.

There is no force flag. Before calling you the core:

- skips an entry refreshed without failing within the last hour, unless
  forced;
- holds the entry's lock, shared by every refresh, purge and image job, and
  merges duplicate requests;
- refreshes an entry asked for by ID only while something links to it (a
  collection while it is stored), unless `Reason` is `Requested`.

Throw when a refresh fails: it is logged, the entry keeps its last refresh
time, the anime's other entries still refresh, and the job retries. After a
refresh, the core syncs the series' episode links and queues your image job
unless it was a quick one.

## The core's jobs

You write no queue code. The core registers one job type per provider:

| Job | Calls | Queued by |
|---|---|---|
| `RefreshMetadataJob<TProvider>` | Your refresh calls for what an anime links to, or the one entry asked for | AniDB telling the core about an anime, `RefreshForAnime`, `RefreshEntry`, the search job after it linked something, the refresh actions |
| `SearchMetadataJob<TProvider>` | `IMetadataAutoLinkingProvider.FindAutoLinks`, then links what you took | An anime not linked on your source, `IMetadataLinkingService.AutoLink`, the search actions |
| `DownloadMetadataImagesJob<TProvider>` | `IMetadataImageProvider.GetImages` for each entity under the entry | A refresh asking for images, `IMetadataRefreshService.DownloadImages`, the image actions |
| `PurgeMetadataJob` | Your `CleanUp`, last | A link removed with `Purge`, `IMetadataPurgeService`, the purge actions |
| `DownloadContributedImagesJob<TContributor>` | `IMetadataImageContributor.GetImages` | The owner's image job, or `DownloadImages` for an entry no provider covers |
| `ClearContributedImagesJob` | Nothing of yours | A contributor turned off for a pair |

AniDB tells the core about an anime when it refreshes it (unless asked to
skip supplementary updates) and when a release links a file to it; the core
then queues a refresh from each enabled provider whose source the anime is
linked on and a search where it is linked on nothing. To hear about AniDB
refreshes yourself, subscribe to `IMetadataService.SeriesAdded` and
`SeriesUpdated` and check for `MetadataSource.AniDB`.

The refresh and image jobs only ask an enabled provider for the entity types
it is turned on for (any of `series`, `season` and `episode` covers a
series). A scheduled search runs only while the source auto-links, the anime
is not left alone and has no link on the source; no search runs while the
auto-linker is not configured.

### Auto-linking

`FindAutoLinks` runs inline in your own job and writes nothing: hand back
every series and film you scored, best first, the ones you turned down with a
`Rejection`. Throw `MetadataProviderUnavailableException` when your source is
unreachable. The core then turns down what it may not link (`KindDisabled`,
`InvalidID`), logs every candidate at debug level, links the rest, and queues
a refresh of what the anime now links to on your source.

Weigh the hints too: the entries the anime's AniDB resources name
(`AnidbResource`) and those its links on other sources name
(`CrossSourceLink`, from `IMetadataLinkingService.GetCrossSourceHints`). Rate
each through the matching engine and return it after your own candidates; the
core takes one at most. Send the episodes of your best candidates' seasons
(`MetadataSearchResultSeason.Episodes`) so the engine can line them up by air
date. See [linking](../Services/README.md#linking).

A search a person asks for one anime replaces every link the anime has on your
source, but only once something new was written. A library-wide search never
touches an anime already linked. `IMetadataLinkingService.PreviewAutoLink` is
the same call, unapplied.

What counts as missing a link is the same for every source, and is what the
`MissingSourceLink` filter and `GET /api/v3/Dashboard/MissingLinks` report: an
anime of a type the source links, not left alone, with no series or film link
on the source (a link made to nothing counts as a link).

### Purging

The purge job holds the entry's lock and leaves alone an entry something
still links to, unless forced, in which case it removes those links first.
Otherwise it removes the entry's rows and everything the stores hold for it,
its last refresh time and, for a series, every ordering of it; purging a
series or film queues the purge of each collection holding it. Then
`IMetadataProvider.CleanUp` is called on every provider claiming the source,
even disabled ones, for what you keep in your own database.

You never sweep your own entries: the core purges daily, for every plugin
source and TMDB, what nothing links to and was not refreshed within the
admin's setting (two weeks unless changed).

### Pausing and concurrency

- **Pausing.** Implement `IPausableMetadataProvider`, report a
  `MetadataProviderPauseStatus` (why, and until when if known) and raise
  `PauseStatusChanged`. Only your jobs wait, and the status shows through
  `IMetadataRefreshService.GetPauseStatus`.
- **Concurrency.** `MaxConcurrentJobs` caps each of your job types, in a pool
  of its own, read once at registration.

The actions a person can run are `Refresh Linked Metadata`,
`Auto-Search Metadata Links` and `Download Linked Metadata Images - Force` on a
series, and `Refresh All Linked Metadata`, `Search for Metadata Matches`,
`Download All Linked Metadata Images - Force`, `Purge Unused Metadata` and
`Purge Orphaned Metadata` across the library. Each is a call on
`IMetadataRefreshService` or `IMetadataPurgeService` that a plugin can make
too.

## Matching episodes

`IMetadataSeriesLinkingProvider.MatchEpisodes` is how a person lines an
anime's episodes up with yours, through `IMetadataLinkingService.MatchEpisodes`.
The core hands you the anime, its regular episodes and specials, your series,
optionally a season, and the links to keep. `considerOtherLinks` asks you to
leave out episodes other anime are linked to (`null` for your default). Answer
with an `EpisodeMatch` for every episode you decided on, kept links included,
and write nothing: the core saves the result when asked.

## Moving links between servers

`IMetadataCrossReferenceTransferService` exports and imports a source's links
as CSV over the core's store, in TMDB's format with your source's name in the
headers. You implement nothing for it.

## Images

Implement `IMetadataImageProvider` to have the core link and download your
images. After a refresh, or when a person asks, the core's image job walks the
entry as the stores read it back (the series with its seasons and episodes,
the film or the collection, and the people, studios and networks credited
under your source) and calls `GetImages` with each entity's `MetadataGuid`.
Answer with every `ImageCandidate` you have, in your source's order, or `null`
to leave an entity's images alone:

| Member | Meaning |
|---|---|
| `ResourceID` | What completes your template URL, at most 128 characters; also the image's identity |
| `ImageType` | `Primary` (poster), `Backdrop`, `Logo`, `Banner` or `Disc`; an episode's thumbnail is its `Backdrop` |
| `Width`, `Height` | The size, when known |
| `LanguageCode`, `CountryCode` | The language of the text in the image, and the country it is for |
| `Rating`, `RatingVotes` | The community rating from 1 to 10 and its votes |
| `IsDefault` | Whether your source uses it as the entity's default of its type |

An image you stop offering is unlinked, but only links your source made are
touched. What is downloaded follows the admin's image settings for your source
(or the shared defaults): per type a switch and a maximum, plus a language
order where "main" is the series' or film's `OriginalLanguageCode`. Register
your template URL with `IImageManager.RegisterTemplateUrl` on every start;
without one no image of your source is linked.

### Adding images to other sources' entries

A plugin with images but no entries of its own, such as an artwork service for
TMDB's shows and movies, implements `IMetadataImageContributor`. It names the
source its images are kept under (registered by the plugin as a remote source)
and the sources and kinds it adds images for, core sources included:

```csharp
public class ArtworkContributor(ArtworkClient client) : IMetadataImageContributor
{
    public string Name => "Example artwork";

    public MetadataSource Source => ExampleSources.Artwork;

    public MetadataEntityScope Scope { get; } = MetadataEntityScope.ForSource(
        MetadataSource.TMDB,
        MetadataEntityType.Series,
        MetadataEntityType.Movie
    );

    public int? MaxConcurrentJobs => 1;

    public async Task<IReadOnlyList<ImageCandidate>?> GetImages(IMetadata entity, CancellationToken cancellationToken = default)
        => entity switch
        {
            ITmdbMovie movie => await client.GetMovieArt(movie.ID.ID, cancellationToken),
            ISeries series => await client.GetShowArt(series.ID.ID, cancellationToken),
            _ => null,
        };
}
```

Whenever the core refreshes an entry's images it queues one
`DownloadContributedImagesJob<TContributor>` per contributor whose enabled
pairs cover the entry: after the owner's image job (even when that downloads
nothing), or at once through `IMetadataRefreshService.DownloadImages` when no
provider's image job covers the entry. The job resolves the entry when it
runs, skips one that is gone, and links your candidates under your `Source`
by your source's image settings.

Each contributor's jobs run in a pool of `MaxConcurrentJobs` (two when
`null`). Every pair starts on; an admin turns pairs off through
`IMetadataImageContributorManager` or
`PUT /api/v3/Metadata/ImageContributor/{contributorID}`, which queues
`ClearContributedImagesJob` to remove your links there. The core refuses a
contributor whose source is unregistered, core, local or another
contributor's, and drops pairs on your own source, which your provider's
image job serves.

## What clients see

A series or episode asked for with `includeDataFrom=<source>` gains a
`Sources` object keyed by your source, with a generic view of each linked
series, episode and movie read back through `IMetadataService`: ID, entity
type, titles, description, dates, rating and preferred images. AniDB and TMDB
keep their own blocks. A link to nothing, or to an entry not stored yet, is
left out.

## TMDB

TMDB is the one provider the core ships, and runs through the same jobs,
freshness check and locking. It differs where it keeps its entries in tables
of its own:

- Its refresh calls write TMDB's tables, which the core resolves TMDB's
  entries and last refresh times from.
- A purge removes nothing from the stores; TMDB's `CleanUp` clears its own
  tables, and a collection goes with its last movie. The core still removes a
  purged show's orderings.
- Its image settings are TMDB's entry in the per-source image settings. It
  marks no candidate as default, hands over a person's images only when a
  refresh fetched the person within two hours, and its image job holds only
  the entry's image lock.
- Its people and networks are stamped and purged from TMDB's tables by the
  same setting as a plugin source's; its companies go with the last entry
  naming them.
- Its links are in the shared tables and go through `IMetadataLinkingService`.
  Its `MatchEpisodes` uses `IMetadataMatchingEngine` with
  `DateAndTitleWithinSeasons`, and runs again on every full refresh.
- Its episode groups are served by `IMetadataOrderingService` as orderings of
  its shows.
- `ITmdbMetadataService` and `ITmdbLinkingService` are shims over the generic
  services; see [`../Tmdb/Services/README.md`](../Tmdb/Services/README.md).
