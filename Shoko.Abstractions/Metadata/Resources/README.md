# Resource Resolvers

A `Resource` is an external link on an entity: an official site, a streaming
page, a Wikipedia article, or the same title in another database.
`IWithResources.Resources` is where they surface, and `IResourceResolver`
(in [`Providers/`](../Providers/IResourceResolver.cs)) is how a plugin adds
its own.

## How a resource reaches an entity

Each entity builds its own list in its `Resources` getter, then appends what
the resolvers answer. `AniDB_Anime` shows the shape:

```csharp
public IReadOnlyList<Resource> Resources
{
    get
    {
        var result = GetAnidbResources();
        result.AddRange(ISystemService.StaticServices.GetRequiredService<IMetadataService>().GatherResourcesForEntity(this));
        return result;
    }
}
```

`IMetadataService.GatherResourcesForEntity(entity)` calls `Resolve(entity)` on
every registered resolver, in registration order, and concatenates the
results: no filtering, deduplication or ordering.

Resources are also how a plugin reads an external ID the core holds but no
interface exposes. AniDB lists Syoboi Calendar IDs, for example, and the core
publishes each as a `CrossReference` resource named `syoboi` with the bare ID
in `ID`, so a plugin keyed on Syoboi IDs reads it from there. Other code may
parse your `Url` too, so treat it as data, not only as something to render.

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
                    Url = $"/api/plugin/MyPlugin/Assets/dashboard?aid={anime.AnidbID}",
                },
            ],
            _ => [],
        };
}
```

`Name` and `Resolve` are the whole interface. A `Resource` requires `Type`,
`Name` and `Url`, and may carry `LanguageCode` (ISO 639-1) and `ID`, the
entry's bare ID on the linked site (such as `tt0123456` for IMDb); fill it
whenever the site has IDs. `ResourceType` is `Website`, `Streaming`,
`Metadata`, `CrossReference`, `Social` or `Trailer`.

The core finds the resolver in your assembly
(`PluginManager.GetExports<IResourceResolver>()`) and holds it for the life of
the process. Register the concrete type as a singleton only when your own code
resolves it, and never under the interface (see the
[abstractions README](../../README.md)).

## Re-entrance

`GatherResourcesForEntity` returns an empty list for an entity it is already
resolving, so a resolver may read `entity.Resources` on the entity it was
handed: the nested call returns the built-in resources without the resolver
pass. The guard is per entity instance, so reading another entity's
`Resources` runs the resolvers again for that one.

## Mistakes that are easy to make

- **Forgetting kinds.** `IWithResources` is carried by `ISeries`, `IEpisode`,
  `IMovie`, `ICreator` and `ICharacter`. The AniDB, Shoko and stored plugin
  entities of those kinds all reach a resolver.
- **Handling only `IShokoSeries`.** The APIv3 series `Links` are built from
  the AniDB anime's `Resources`, so handle `IAnidbAnime` as well.
- **Not expecting duplicates.** A Shoko series' list concatenates its linked
  series' and films' lists (each already resolved) and then resolves itself,
  so a resolver answering for both `IShokoSeries` and `IAnidbAnime` appears
  twice there.
- **Slow work.** `Resolve` runs on every read of `Resources`, with no caching
  anywhere. Build the URL from what the entity carries; no I/O.
- **Throwing.** Nothing catches a resolver's exception: it propagates out of
  `entity.Resources`. Return an empty list instead.
- **Changing a `Url`'s shape.** Other plugins may parse it, so that is a
  breaking change.
