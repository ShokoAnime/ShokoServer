using System;
using System.Collections.Generic;
using Shoko.Abstractions.Metadata.Containers;
using Shoko.Abstractions.Metadata.Enums;

namespace Shoko.Abstractions.Metadata;

/// <summary>
///   Character. Its main name is its default title, its official names per
///   language are titles of the <see cref="TitleType.Official"/> type, and
///   other names it goes by are <see cref="TitleType.Synonym"/> titles.
/// </summary>
public interface ICharacter : IMetadata, IWithTitles, IWithOverviews, IWithPrimaryImage, IWithCreationDate, IWithUpdateDate, IWithResources
{
    /// <summary>
    ///   When the core last asked the source to refresh the character, found or
    ///   not, in UTC. Set by the core alone; <c>null</c> when it
    ///   never did.
    /// </summary>
    DateTime? LastRefreshedAt { get; }

    /// <summary>
    /// The type of character.
    /// </summary>
    CharacterType Type { get; }

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
