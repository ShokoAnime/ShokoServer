using Shoko.Abstractions.Metadata.Enums;

namespace Shoko.Abstractions.Metadata;

/// <summary>
/// A cast role.
/// </summary>
public interface ICast
{
    /// <summary>
    /// The source the cast role is from. A cast role is no entry of its own;
    /// it is named by its character, its creator and its language.
    /// </summary>
    MetadataSource Source { get; }

    /// <summary>
    ///   The creator who voiced or played the role, if it is known.
    /// </summary>
    MetadataGuid? CreatorID { get; }

    /// <summary>
    ///   The character, if the cast role has a character shared with one or
    ///   more other cast roles.
    /// </summary>
    MetadataGuid? CharacterID { get; }

    /// <summary>
    ///   The entry the role is credited on: a series, a season, an episode or
    ///   a film.
    /// </summary>
    MetadataGuid ParentID { get; }

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
    ///   A description of the role, or notes on it such as the age or form
    ///   the character is played in, if available from the provider.
    /// </summary>
    string? Description { get; }

    /// <summary>
    ///   The group that made the dub the performance is in, if the provider
    ///   names one.
    /// </summary>
    string? DubGroup { get; }

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
    IMetadata? Parent { get; }

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
public interface ICast<TMetadata> : ICast where TMetadata : IMetadata
{
    /// <summary>
    /// Parent metadata entity.
    /// </summary>
    TMetadata? ParentOfType { get; }
}
