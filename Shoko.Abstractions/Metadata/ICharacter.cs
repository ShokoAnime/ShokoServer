using System.Collections.Generic;
using Shoko.Abstractions.Metadata.Containers;
using Shoko.Abstractions.Metadata.Enums;

namespace Shoko.Abstractions.Metadata;

/// <summary>
/// Character.
/// </summary>
public interface ICharacter : IMetadata, IWithOverviews, IWithPrimaryImage, IWithUpdateDate, IWithResources
{
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
    /// The type of character.
    /// </summary>
    CharacterType Type { get; }

    /// <summary>
    ///   Other names the character is known by, each with its language when
    ///   known.
    /// </summary>
    IReadOnlyList<ITitle> AlternativeNames { get; }

    /// <summary>
    ///   The character's gender, when the source gives one.
    /// </summary>
    PersonGender Gender { get; }

    /// <summary>
    ///   The character's birthday. Any part of it may be unknown, so it may be
    ///   only a year, or only a month and a day.
    /// </summary>
    FuzzyDateOnly? BirthDay { get; }

    /// <summary>
    /// All episode cast roles with the character.
    /// </summary>
    IEnumerable<ICast<IEpisode>> EpisodeCastRoles { get; }

    /// <summary>
    /// All movie cast roles with the character.
    /// </summary>
    IEnumerable<ICast<IMovie>> MovieCastRoles { get; }

    /// <summary>
    /// All series cast roles with the character.
    /// </summary>
    IEnumerable<ICast<ISeries>> SeriesCastRoles { get; }
}
