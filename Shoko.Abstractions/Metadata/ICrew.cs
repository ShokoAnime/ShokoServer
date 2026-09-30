using Shoko.Abstractions.Metadata.Enums;

namespace Shoko.Abstractions.Metadata;

/// <summary>
/// A crew role.
/// </summary>
public interface ICrew
{
    /// <summary>
    /// The source the crew role is from. A crew role is no entry of its own;
    /// it is named by its creator and its job.
    /// </summary>
    MetadataSource Source { get; }

    /// <summary>
    ///   The creator credited.
    /// </summary>
    MetadataGuid CreatorID { get; }

    /// <summary>
    ///   The entry the role is credited on: a series, a season, an episode or
    ///   a film.
    /// </summary>
    MetadataGuid ParentID { get; }

    /// <summary>
    /// Name of the crew role, in English.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Programmatic type of the crew role.
    /// </summary>
    CrewRoleType RoleType { get; }

    /// <summary>
    /// The language the crew member worked in. Sources that only carry the
    /// original-language crew (AniDB, TMDB) report the work's original
    /// language; sources with dubs (AniList) report the creator's language.
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
    /// Creator. Can be null if the metadata is
    /// currently not locally available.
    /// </summary>
    ICreator? Creator { get; }
}

/// <summary>
/// A crew role for a parent entity.
/// </summary>
/// <typeparam name="TMetadata">Metadata type.</typeparam>
public interface ICrew<TMetadata> : ICrew where TMetadata : IMetadata
{
    /// <summary>
    /// Parent metadata entity.
    /// </summary>
    TMetadata? ParentOfType { get; }
}
