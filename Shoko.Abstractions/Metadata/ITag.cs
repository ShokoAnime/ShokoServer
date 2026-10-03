using Shoko.Abstractions.Metadata.Enums;

namespace Shoko.Abstractions.Metadata;

/// <summary>
/// Basic tag metadata.
/// </summary>
public interface ITag : IMetadata
{
    /// <summary>
    /// The tag name.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// What does the tag mean/what's it for.
    /// </summary>
    string Overview { get; }

    /// <summary>
    /// Whether the tag is a descriptive tag, a genre or a keyword.
    /// </summary>
    TagKind Kind { get; }

    /// <summary>
    /// The group the source files the tag under, when it has one.
    /// </summary>
    string? Category { get; }

    /// <summary>
    /// Whether the tag gives something away. Read through an entity, whether
    /// it does for that entity.
    /// </summary>
    bool IsSpoiler { get; }

    /// <summary>
    /// Whether the tag is for adult content.
    /// </summary>
    bool IsRestricted { get; }

    /// <summary>
    /// How strongly the tag applies, on the source's own scale. Only set when
    /// the tag is read through an entity, and the source weighs its tags.
    /// </summary>
    int? Weight { get; }
}
