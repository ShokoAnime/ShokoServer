using System.Collections.Generic;

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
}
