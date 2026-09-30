namespace Shoko.Abstractions.Metadata;

/// <summary>
///   An entry of one source, named by its <see cref="ID"/>. The source and
///   the kind of entry are read off the ID.
/// </summary>
public interface IMetadata
{
    /// <summary>
    ///   The identity of the entry: its source, its kind and the ID the
    ///   source gave it, e.g. <c>anidb://series/1</c>.
    /// </summary>
    MetadataGuid ID { get; }

    /// <summary>
    ///   The source of the entry.
    /// </summary>
    MetadataSource Source { get => ID.Source; }

    /// <summary>
    ///   The kind of entity the entry is.
    /// </summary>
    MetadataEntityType EntityType { get => ID.EntityType; }
}
