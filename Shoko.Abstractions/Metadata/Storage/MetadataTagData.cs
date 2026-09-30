using Shoko.Abstractions.Metadata.Enums;

namespace Shoko.Abstractions.Metadata.Storage;

/// <summary>
///   A tag or genre to store.
/// </summary>
public sealed record MetadataTagData
{
    /// <summary>
    ///   The tag: its source, the <c>tag</c> kind and the source's own ID for
    ///   it, e.g. <c>anilist://tag/Action</c>. A source whose genres have no
    ///   IDs can use the genre's name.
    /// </summary>
    public required MetadataGuid ID { get; init; }

    /// <summary>
    ///   The tag's name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    ///   What the tag means.
    /// </summary>
    public string Overview { get; init; } = string.Empty;

    /// <summary>
    ///   Whether it is a descriptive tag, a genre or a keyword.
    /// </summary>
    public TagKind Kind { get; init; } = TagKind.Tag;

    /// <summary>
    ///   The group the source files the tag under.
    /// </summary>
    public string? Category { get; init; }

    /// <summary>
    ///   Whether the tag gives something away wherever it is used.
    /// </summary>
    public bool IsSpoiler { get; init; }

    /// <summary>
    ///   Whether the tag is for adult content.
    /// </summary>
    public bool IsRestricted { get; init; }
}

/// <summary>
///   A tag as it applies to one entry.
/// </summary>
public sealed record MetadataEntryTagData
{
    /// <summary>
    ///   The tag.
    /// </summary>
    public required MetadataGuid TagID { get; init; }

    /// <summary>
    ///   How strongly it applies, on the source's own scale.
    /// </summary>
    public int? Weight { get; init; }

    /// <summary>
    ///   Whether it gives something away for this entry in particular.
    /// </summary>
    public bool IsSpoiler { get; init; }
}
