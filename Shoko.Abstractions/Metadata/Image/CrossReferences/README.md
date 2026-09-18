# Image Cross-Reference Resolvers

An image cross-reference (`IImageCrossReference`) is the row that says "this
image belongs to that entity". `IImageManager` owns every one of them, and it
addresses the entity side of the row not by object reference but by a triplet:
`(EntitySource, EntityType, EntityID)`.

Core knows how to build that triplet for the entities it ships, and how to turn
one back into an entity. `IImageCrossReferenceResolver` is the extension point
for the ones it doesn't: a plugin entity that no core interface covers, most
often under `DataSource.Plugin` and a type like `DataEntityType.Library`.

---

## Why a triplet instead of an entity

`ShokoImage_Entity` stores `EntitySource` (a byte), `EntityType` (a byte) and
`EntityID` (`NVARCHAR(128)`). Nothing about the entity object itself survives
into the database, which is what lets one image be shared by an AniDB anime, a
TMDB show and a plugin's own entity without three separate tables. The price is
that the manager needs two functions it cannot write for a type it has never
heard of:

| Direction | Core's implementation | Your resolver |
|---|---|---|
| entity → triplet | `ImageManager.TryGetMetadataForEntity` | `TryGetMetadataForEntity` |
| triplet → entity | `ImageManager.GetEntityForImage` | `GetEntity` |

Both core implementations consult the registered resolvers **last**, after every
built-in case has missed. That ordering is the single most important thing to
know about this interface, and the next two sections are about it.

### entity → triplet, and when you are asked

`TryGetMetadataForEntity` runs a `switch` over the entity, in this order:
`ICollection`, `IMovie`, `ISeries`, `ISeason`, `IEpisode`, `IVideo`, `ICreator`,
`ICharacter`, `IStudio`, `INetwork`, the four TMDB cross-reference interfaces
(`ITmdbShowCrossReference`, `ITmdbSeasonCrossReference`,
`ITmdbEpisodeCrossReference`, `ITmdbMovieCrossReference`) and `IUser`. Only if
all of those miss does it walk the resolvers:

```csharp
foreach (var resolver in _resolvers)
    if (resolver.TryGetMetadataForEntity(entity, out entitySource, out entityType, out entityID, out entitySeasonNumber, out entityEpisodeNumber, out releasedAt))
        return true;

return false;
```

So an entity of yours that implements `ISeries` is handled by core's `ISeries`
arm, with `entityID = series.ID.ToString()`, and your resolver is never called
for it. That is usually what you want. You need a resolver only for an entity
that implements `IWithImages` (directly or through `IMetadata`) without matching
any of the interfaces above.

### triplet → entity, and when you are asked

`GetEntityForImage` is a `switch` on the `(entitySource, entityType)` pair, with
arms for Shoko, AniDB and TMDB. The resolvers are its default arm:

```csharp
_ => _resolvers
    .Select(r => r.GetEntity(entitySource, entityType, entityID))
    .FirstOrDefault(result => result is not null),
```

Two consequences. Your resolver is asked for every pair core has no arm for, not
just yours, so it has to answer `null` for anything that isn't its own. And a
pair core *does* handle, `(AniDB, Anime)` for instance, never reaches you at
all, so you cannot use a resolver to override or extend a built-in entity.

---

## Implementing one

```csharp
public class MyImageCrossReferenceResolver(MyLibraryRepository repository) : IImageCrossReferenceResolver
{
    public string Name => "MyPlugin";

    // Triplet -> entity. Answer null for anything that isn't ours: this is the
    // default arm of core's switch, so it is called for unknown pairs too.
    public IWithImages? GetEntity(DataSource entitySource, DataEntityType entityType, string entityID)
        => entitySource is DataSource.Plugin && entityType is DataEntityType.Library && Guid.TryParse(entityID, out var id)
            ? repository.GetByID(id)
            : null;

    // Entity -> triplet. Every out parameter must be assigned on every path,
    // including the false one.
    public bool TryGetMetadataForEntity(
        IWithImages entity,
        out DataSource entitySource,
        out DataEntityType entityType,
        [NotNullWhen(true)] out string? entityID,
        out int? entitySeasonNumber,
        out int? entityEpisodeNumber,
        out DateOnly? releasedAt
    )
    {
        entitySource = DataSource.Plugin;
        entityType = DataEntityType.Library;
        entityID = null;
        entitySeasonNumber = null;
        entityEpisodeNumber = null;
        releasedAt = null;

        if (entity is not MyLibrary library)
            return false;

        entityID = library.ID.ToString();
        releasedAt = DateOnly.FromDateTime(library.CreatedAt);
        return true;
    }
}
```

With that in place the entity works everywhere an entity works: attach an image
with `IImageManager.AddImageCrossReference(library, image, new() { ImageType = ImageEntityType.Primary, Source = DataSource.Plugin })`,
read them back with `library.GetImages()`, and `xref.GetEntity()` on any row of
yours resolves back to the `MyLibrary` instance.

### `Name` and the three members

`Name`, `GetEntity` and `TryGetMetadataForEntity` are the whole interface. None
of them has a default implementation, and there is no lifecycle hook, no
priority and no configuration variant. `Name` is documentation for the plugin
list, nothing dispatches on it.

### What the extra out parameters are for

`entitySeasonNumber`, `entityEpisodeNumber` and `releasedAt` are denormalised
onto the cross-reference row as `EntitySeasonNumber`, `EntityEpisodeNumber` and
`EntityReleasedAt` when it is created, refreshed when
`UpdateImageCrossReference` is passed an entity, and surfaced over the v3 API.
They are there so a consumer can sort or group image rows without resolving
every entity first. Leave all three `null` when they don't apply, which for most
custom entities is all three.

---

## Registering it

Usually you don't. `PluginManager.GetExports<IImageCrossReferenceResolver>()`
finds the type in your assembly, constructs it with constructor injection and
hands it to `IImageManager.AddParts`, which holds it for the life of the
process. Register the **concrete type** as a singleton only when your own code
resolves the resolver, and never register it under the
`IImageCrossReferenceResolver` interface.

The full rule, and why the interface registration is actively harmful, is in the
[abstractions README](../../../README.md).

---

## `LinkedEntityImages`, and why it is `bool?`

`ImageFilteringOptions.LinkedEntityImages` (and the same field on
`ImageCrossReferenceFilteringOptions`) decides whether a read returns only the
entity's own images or also the images of entities linked to it:

| Value | Meaning |
|---|---|
| `false` | Only rows whose triplet is exactly this entity's. |
| `true` | Those, plus the rows of every entity reachable through the link walk. |
| `null` (the default) | Let the service decide from the entity. |

The `null` case resolves to one line:

```csharp
linkedEntityImages ??= entity is IShokoGroup or IShokoSeries or IShokoSeason or IShokoEpisode;
```

A shoko entity is a wrapper whose images come from the providers behind it, so
it defaults to `true` and the walk collects the AniDB anime, the linked TMDB
seasons, the linked movies and so on, deduplicating by triplet on the way. A
provider entity such as a TMDB show is a leaf and defaults to `false`, because
its own rows are all there are. That is why the field is nullable rather than a
plain `bool`: `false` and "you decide" are different answers, and a caller that
passes neither should get the sensible one per entity rather than a fixed one.

For a plugin entity, the default is `false`, since it is none of those four
types. Passing `true` explicitly does not change the result today either: the
link walk's `switch` has arms for exactly `IShokoGroup`, `IShokoSeries`,
`IShokoSeason` and `IShokoEpisode`, and an entity matching none of them
contributes only its own rows. Images returned through the walk are wrapped with
a flag saying they came from a linked entity, which is what
`IImageManager.IsLinkedCrossReference` reports; for your entity that flag is
always false.

---

## Mistakes that are easy to make

- **Leaving an `out` parameter unassigned on the `false` path.** The compiler
  catches it, but the fix people reach for first is wrong: assign every
  parameter *before* the type test, the way the example above does, rather than
  only inside the success branch.
- **Assuming `entitySource` and `entityType` are handed to you.** Core seeds its
  own locals from `entity.Source` and `entity.EntityType` before its switch, but
  those are `out` parameters by the time your resolver sees them, so they arrive
  unassigned. Set them yourself, from the entity or from constants.
- **Returning a match for an entity you don't own.** The loop stops at the first
  resolver that answers `true`, and `GetEntity` takes the first non-null across
  all resolvers, both in registration order, which is plugin load order. A
  resolver that is loose about what it claims will quietly steal another
  plugin's entities depending on install order.
- **Expecting to be asked about a core entity.** If your type implements
  `ISeries`, `IEpisode`, `ICreator` or any of the other interfaces in the core
  switch, that arm wins and the resolver is dead code. Check the list before
  writing one.
- **An ID longer than 128 characters,** or one that isn't stable. `EntityID` is
  `NVARCHAR(128)` and is the only thing tying a stored row back to your entity.
  A Guid, an int or a short slug is fine; a path or a title is not.
- **Non-round-tripping identifiers.** `GetEntity(source, type, id)` must find
  the entity that `TryGetMetadataForEntity` produced `id` for. If the two
  disagree, rows are written that nothing can resolve, and they stay in the
  database as orphans.
- **Doing expensive work in either method.** Both sit on hot paths: every image
  read for an entity calls `TryGetMetadataForEntity` first, and a list of
  cross-references calls `GetEntity` once per row. Keep them to a dictionary
  lookup or a cached repository hit.
