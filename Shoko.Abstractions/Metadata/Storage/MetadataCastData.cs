using Shoko.Abstractions.Metadata.Enums;

namespace Shoko.Abstractions.Metadata.Storage;

/// <summary>
///   One cast credit on an entry: a character, who voiced or played it, or
///   both. It is known by its character, its creator and its language. A
///   character or creator not stored yet is kept as a stub.
/// </summary>
public sealed record MetadataCastData
{
    /// <summary>
    ///   The character, when the credit has one.
    /// </summary>
    public MetadataGuid? CharacterID { get; init; }

    /// <summary>
    ///   The character's name, the main title of the stub the core makes when
    ///   the character is not stored yet. <see cref="Name"/> is used when unset.
    /// </summary>
    public string? CharacterName { get; init; }

    /// <summary>
    ///   The creator who voiced or played it, when known.
    /// </summary>
    public MetadataGuid? CreatorID { get; init; }

    /// <summary>
    ///   The creator's name, the main title of the stub the core makes when
    ///   the creator is not stored yet.
    /// </summary>
    public string? CreatorName { get; init; }

    /// <summary>
    ///   The name the role is credited under.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    ///   How important the role is.
    /// </summary>
    public CastRoleType RoleType { get; init; }

    /// <summary>
    ///   The language of the performance, as a language code, for a voice
    ///   role recorded in more than one.
    /// </summary>
    public string? LanguageCode { get; init; }

    /// <summary>
    ///   Notes on the role, such as the age or form the character is played
    ///   in.
    /// </summary>
    public string? RoleNotes { get; init; }

    /// <summary>
    ///   The group that made the dub the performance is in, when the source
    ///   names one.
    /// </summary>
    public string? DubGroup { get; init; }
}
