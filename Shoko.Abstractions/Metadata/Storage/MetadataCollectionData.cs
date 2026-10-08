using System.Collections.Generic;
using Shoko.Abstractions.Metadata.Enums;

namespace Shoko.Abstractions.Metadata.Storage;

/// <summary>
///   A collection to store, whole: its titles and overviews and the works
///   it gathers. Saving it replaces what was stored for the collection.
/// </summary>
public sealed record MetadataCollectionData
{
    /// <summary>
    ///   The collection: its source, the <c>collection</c> kind and the
    ///   source's own ID for it, e.g. <c>anilist://collection/7</c>.
    /// </summary>
    public required MetadataGuid ID { get; init; }

    /// <summary>
    ///   The collection's titles, in order. They are stored under the
    ///   collection's source, whatever source each title names.
    /// </summary>
    /// <remarks>
    ///   The main title (<see cref="TitleType.Main"/>) is the collection's default.
    ///   Without one, the collection gets a synthesized default such as
    ///   <c>TMDB Collection 10</c>, never stored.
    /// </remarks>
    public IReadOnlyList<ITitle> Titles { get; init; } = [];

    /// <summary>
    ///   The collection's overviews, in order, stored like the titles.
    /// </summary>
    public IReadOnlyList<IText> Overviews { get; init; } = [];

    /// <summary>
    ///   The series and movies in the collection, in order. Each must be on
    ///   the collection's source; one given twice keeps its first place.
    /// </summary>
    public IReadOnlyList<MetadataGuid> Members { get; init; } = [];

    /// <summary>
    ///   The source's resource ID of the collection's default image of each type,
    ///   which becomes its pinned default and is always the first downloaded
    ///   within the type's limit. <c>null</c> leaves the stored defaults as
    ///   they are, and an empty map clears them. A type the collection has no
    ///   images of is ignored.
    /// </summary>
    public IReadOnlyDictionary<ImageEntityType, string>? DefaultImageResourceIDs { get; init; }
}
