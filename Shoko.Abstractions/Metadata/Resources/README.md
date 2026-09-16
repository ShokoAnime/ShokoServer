# Resource Resolvers

A `Resource` is an external link hanging off an entity: an official site, a
streaming page, a Wikipedia article, or a pointer to the same title in another
database. `IWithResources.Resources` is where they surface, and
`IResourceResolver` is how a plugin adds its own.

---

## How a resource reaches an entity

Each entity builds its own list, in code, in its `Resources` getter, and then
appends whatever the plugins have to say. `AniDB_Anime` is the longest of them
and shows the shape:

```csharp
public IReadOnlyList<Resource> Resources
{
    get
    {
        var result = new List<Resource>();
        if (!string.IsNullOrEmpty(Site_EN))
            foreach (var site in Site_EN.Split('|'))
                result.Add(new() { Type = ResourceType.Website, Name = "Official Site (EN)", Url = site, LanguageCode = "en" });
        // ... Site_JP, Wikipedia, Crunchyroll, Funimation, HiDive, allcinema,
        //     Anison, syoboi, bangumi, .lain, AnimeNewsNetwork, VNDB, MyAnimeList ...

        result.AddRange(ISystemService.StaticServices.GetRequiredService<IMetadataService>().GatherResourcesForEntity(this));
        return result;
    }
}
```

That last line is the extension point.
`IMetadataService.GatherResourcesForEntity(entity)` calls `Resolve(entity)` on
every registered resolver, in registration order, and concatenates the results.
There is no filtering, no deduplication and no ordering pass: what you return is
appended as-is.

### What this is good for

Resources are the only structured way a plugin can publish a link, and the only
structured way a plugin can *read* an external ID that core holds but does not
expose on an interface. The Syoboi plugin is the worked example of the second
half, and it is worth understanding before writing a resolver, because it shows
what consumers actually do with the list.

`AniDB_Anime` stores a Syoboi Calendar ID in the `SyoboiID` column and publishes
it as a resource:

```csharp
if (SyoboiID.HasValue && SyoboiID.Value > 0)
    result.Add(new() { Type = ResourceType.CrossReference, Name = "syoboi", Url = $"https://cal.syoboi.jp/tid/{SyoboiID.Value}/time" });
```

`IAnidbAnime` has no `SyoboiID` property, so the plugin recovers the ID from the
URL instead, matching on `ResourceType.CrossReference` and a regex over
`Url`, and uses it to key its airing schedules. The `Name` is not what it
matches on, the URL is. Keep that in mind when you write a `Url`: it is data
other code parses, not only something a UI renders.

---

## Implementing one

```csharp
public class MyResourceResolver : IResourceResolver
{
    public string Name => "MyPlugin";

    public IReadOnlyList<Resource> Resolve(IWithResources entity)
        => entity switch
        {
            IShokoSeries series =>
            [
                new()
                {
                    Type = ResourceType.CrossReference,
                    Name = Name,
                    Url = $"/api/plugin/MyPlugin/Assets/dashboard?aid={series.AnidbAnimeID}",
                },
            ],
            IAnidbAnime anime =>
            [
                new()
                {
                    Type = ResourceType.CrossReference,
                    Name = Name,
                    Url = $"/api/plugin/MyPlugin/Assets/dashboard?aid={anime.ID}",
                },
            ],
            _ => [],
        };
}
```

`Name` and `Resolve` are the whole interface. Neither has a default
implementation, and there is no lifecycle hook, no priority and no configuration
variant.

A `Resource` requires `Type`, `Name` and `Url`, and optionally carries
`LanguageCode` (ISO 639-1, `null` when the resource is multi-language or the
language doesn't apply). `ResourceType` is one of `Website`, `Streaming`,
`Metadata`, `CrossReference`, `Social` or `Trailer`.

---

## Registering it

Usually you don't. `PluginManager.GetExports<IResourceResolver>()` finds the type
in your assembly, constructs it with constructor injection and hands it to
`IMetadataService.AddParts`, which holds it for the life of the process.
Register the **concrete type** as a singleton only when your own code resolves
the resolver, and never register it under the `IResourceResolver` interface.

The full rule, and why the interface registration is actively harmful, is in the
[abstractions README](../../README.md).

---

## Re-entrance

`GatherResourcesForEntity` keeps a set of the entities it is currently resolving
and returns an empty list for one already in it:

```csharp
if (!_isResolving.TryAdd(entity, true))
    return [];
```

A resolver may therefore read `entity.Resources` on the entity it was handed
without recursing forever: the nested call returns the entity's built-in
resources and skips the resolver pass, including your own contribution. The
guard is keyed per entity instance, so reading a *different* entity's
`Resources` from inside `Resolve` runs the resolvers again for that entity,
which terminates but is not free.

---

## Mistakes that are easy to make

- **Writing an arm for episodes.** `IWithResources` is carried by `ISeries`,
  `IMovie` and `ICreator`, and nothing else. `IEpisode` does not implement it,
  and no episode model calls `GatherResourcesForEntity`. An `IShokoEpisode` or
  `IAnidbEpisode` case in your `switch` is dead code today, however reasonable
  it looks. The entities that actually reach a resolver are `AniDB_Anime`,
  `AniDB_Creator`, `TMDB_Show`, `TMDB_Movie`, `TMDB_Person`, `Anilist_Anime`,
  `Anilist_Creator` and `AnimeSeries`.
- **Handling only `IShokoSeries`.** The v3 series endpoint builds its `Links`
  from `ser.AniDB_Anime.Resources`, not from the shoko series, so a resolver
  that answers only for `IShokoSeries` never shows up in the API. Handle
  `IAnidbAnime` as well, which is exactly why the shipping plugin resolvers
  have both arms.
- **Handling both and not expecting duplicates.** `AnimeSeries` builds its
  `Resources` by concatenating the resources of its AniDB anime, its TMDB
  movies and shows and its AniList anime (each of which has already run the
  resolvers) and *then* calling `GatherResourcesForEntity` on itself. A resolver
  answering for both `IShokoSeries` and `IAnidbAnime` contributes twice to a
  shoko series' list. Nothing deduplicates it. That is acceptable when the two
  entries mean the same thing, and it is a bug when a consumer counts them.
- **Treating `Resolve` as cheap to call once.** It runs on every read of
  `Resources`, which is a plain getter with no caching anywhere in the chain.
  A DTO build, a filter evaluation and a plugin reading the same series in a
  loop each pay for it. No I/O, no database round trip per call: build the URL
  from what the entity already carries.
- **Throwing.** Resolvers are invoked straight from a property getter through
  `SelectMany`, with no per-resolver try/catch. An exception propagates out of
  `entity.Resources` and takes the caller with it. Return an empty list instead.
- **An unstable `Url`.** Other plugins parse it, as the Syoboi case shows.
  Changing the shape of a URL you publish is a breaking change for whoever reads
  it, even though nothing in the type system says so.
