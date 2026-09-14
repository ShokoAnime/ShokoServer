using Shoko.Abstractions.Metadata.Enums;

namespace Shoko.Abstractions.Metadata;

/// <summary>
/// A cast role.
/// </summary>
public interface ICast : IMetadata<string>
{
    /// <summary>
    /// Creator ID, if the cast role has a known creator.
    /// </summary>
    int? CreatorID { get; }

    /// <summary>
    /// Character ID, if the cast role has a character shared with one or more
    /// other cast roles.
    /// </summary>
    int? CharacterID { get; }

    /// <summary>
    /// Parent entity ID.
    /// </summary>
    int ParentID { get; }

    /// <summary>
    /// Casted role name.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Casted role name in the original language of the media, if available
    /// from
    /// </summary>
    string? OriginalName { get; }

    /// <summary>
    /// Role description, if available from the provider.
    /// </summary>
    string? Description { get; }

    /// <summary>
    /// Role type.
    /// </summary>
    CastRoleType RoleType { get; }

    /// <summary>
    /// The language of the performance. Sources that only carry the
    /// original-language cast (AniDB, TMDB) report the work's original
    /// language; sources with dubs (AniList) report the language the
    /// voice actor performs in.
    /// </summary>
    TitleLanguage Language { get; }

    /// <summary>
    /// The language code for <see cref="Language"/>.
    /// </summary>
    string LanguageCode { get; }

    /// <summary>
    /// Parent metadata entity.
    /// </summary>
    IMetadata<int>? Parent { get; }

    /// <summary>
    /// Character, if the cast role has a character shared with one or more
    /// other cast roles.
    /// </summary>
    ICharacter? Character { get; }

    /// <summary>
    /// Creator. Can be null if the cast role has no known creator or if the
    /// metadata is currently not locally available.
    /// </summary>
    ICreator? Creator { get; }
}

/// <summary>
/// A cast role for a parent entity.
/// </summary>
/// <typeparam name="TMetadata">Metadata type.</typeparam>
public interface ICast<TMetadata> : ICast where TMetadata : IMetadata<int>
{
    /// <summary>
    /// Parent metadata entity.
    /// </summary>
    TMetadata? ParentOfType { get; }
}
