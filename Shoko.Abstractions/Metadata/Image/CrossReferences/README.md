# Image Cross-References and Their Entities

An image cross-reference (`IImageCrossReference`) is the row saying "this
image belongs to that entity". `IImageManager` owns every one, and names the
entity by its `MetadataGuid` (`IImageCrossReference.EntityID`, such as
`tmdb://series/1`). This page covers the way back, from that ID to the entity,
and what a plugin writes for its own entities. Reading and adding images is in
[`IImageManager`](../../Services/README.md#iimagemanager).

## Why an ID instead of an entity

`ShokoImage_Entity` stores the ID's three parts in `EntitySource`,
`EntityType` and `EntityID` (`NVARCHAR(128)`, the most a `MetadataGuid` ID can
hold). Nothing else of the entity is stored, which lets one image be shared by
an AniDB anime, a TMDB show and a plugin's entity without a table each. The
ID part is the source's own ID, as `IMetadata.ID` carries it; a video's is
`<ED2K>+<file size>`.

`GetEntityForImage` answers:

1. Nothing for an ordering of a core source other than `user`: those keep
   what their source gives them.
2. Otherwise `IMetadataService.GetEntry`: the core's tables for the core's
   sources, and for any other source the `IMetadataResolver` that took the
   ID's source and kind, then the stores and the ordering service. The entry
   is used when it has images (`IWithImages`) and is not a default ordering,
   which is never stored.

So an entity of yours that the stores hold needs nothing more. One they do not
hold, most often of a kind your plugin registered (such as a `library`), needs
an `IMetadataResolver`
([resolving your own kinds](../../Providers/README.md#resolving-your-own-kinds)).

## Implementing one

```csharp
// Registered up front; see the providers README.
public static class MySources
{
    static MySources()
    {
        Mine = MetadataSource.Register("MyPlugin", "my-plugin");
        Library = MetadataEntityType.Register("Library", "library", description: "A library on the media server.");
    }

    public static MetadataSource Mine { get; }

    public static MetadataEntityType Library { get; }
}

public class MyLibraryResolver(MyLibraryRepository repository) : IMetadataResolver
{
    public string Name => "MyPlugin libraries";

    public MetadataEntityScope Scope { get; } = MetadataEntityScope.Single(MySources.Mine, MySources.Library);

    // Only IDs of the source and kind above reach this.
    public IMetadata? GetEntry(MetadataGuid id)
        => Guid.TryParse(id.ID, out var libraryID) ? repository.GetByID(libraryID) : null;
}

public class MyLibrary : IWithImages
{
    public Guid LibraryID { get; init; }

    // The ID the image manager stores and hands back to the resolver.
    public MetadataGuid ID => new(MySources.Mine, MySources.Library, LibraryID.ToString());
}
```

The entity then works everywhere an entity works:
`IMetadataService.GetEntry<MyLibrary>(id)`,
`IImageManager.AddImageCrossReference(library, image, new() { ImageType = ImageEntityType.Primary, Source = MySources.Mine })`,
`library.GetImages()` and `xref.GetEntity()`.

`Name`, `Scope` and `GetEntry` are the whole interface. `Scope` is read once
at registration. The core finds the resolver in your assembly
(`PluginManager.GetExports<IMetadataResolver>()`); register the concrete type
as a singleton only when your own code resolves it, and never under the
interface (see the [abstractions README](../../../README.md)). A pair on a
core source, or one another resolver took first in plugin load order, is
refused with an error; a resolver left with no pairs is never asked.

A cross-reference also records `EntitySeasonNumber`, `EntityEpisodeNumber`
and `EntityReleasedAt`, read off the entity when the row is created or
`UpdateImageCrossReference` is passed the entity, so rows sort without
resolving every entity.

`ImageFilteringOptions.LinkedEntityImages` defaults to `false` for a plugin
entity, and `true` adds nothing either: the link walk only covers
`IShokoGroup`, `IShokoSeries`, a Shoko series' seasons
(`ISeason<IShokoSeries, IShokoEpisode>`) and `IShokoEpisode`.

## Mistakes that are easy to make

- **Answering for what the stores hold.** A resolver is asked before the
  stores for its pairs, so its answer hides the stored entry. Return `null`
  for an ID you do not hold.
- **An unstable or long ID.** The ID part holds at most 128 characters and is
  all that ties a stored row to your entity: a GUID, number or short slug, not
  a path or a title.
- **IDs that do not round-trip.** `GetEntry(id)` must find the entity whose
  `ID` is `id`, or rows are written that nothing resolves.
- **Expensive work in `GetEntry`.** A list of cross-references calls it once
  per row; keep it to a dictionary or cached lookup.
