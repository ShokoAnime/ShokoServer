using System;
using System.Collections.Generic;
using Shoko.Abstractions.Metadata.Containers;
using Shoko.Abstractions.Metadata.Enums;

namespace Shoko.Abstractions.Metadata;

/// <summary>
///   Creator. Its main name is its default title, its official names per
///   language are titles of the <see cref="TitleType.Official"/> type, and
///   other names it goes by are <see cref="TitleType.Synonym"/> titles.
/// </summary>
public interface ICreator : IMetadata, IWithTitles, IWithOverviews, IWithPrimaryImage, IWithCreationDate, IWithUpdateDate, IWithResources
{
    /// <summary>
    ///   When the core last asked the source to refresh the creator, found or
    ///   not, in UTC. Set by the core alone; <c>null</c> when it
    ///   never did.
    /// </summary>
    DateTime? LastRefreshedAt { get; }

    /// <summary>
    /// The type of the creator.
    /// </summary>
    CreatorType Type { get; }

    /// <summary>
    ///   The creator's gender, when the source gives one.
    /// </summary>
    PersonGender Gender { get; }

    /// <summary>
    ///   The creator's date of birth. Any part of it may be unknown, so it may
    ///   be only a year, or only a month and a day.
    /// </summary>
    FuzzyDateOnly? BirthDay { get; }

    /// <summary>
    ///   The creator's date of death, when known. Any part of it may be
    ///   unknown, as with <see cref="BirthDay"/>.
    /// </summary>
    FuzzyDateOnly? DeathDay { get; }

    /// <summary>
    ///   Where the creator was born, as free text the way the source gives
    ///   it, such as <c>Tokyo, Japan</c>, or <c>null</c> when it does not say.
    /// </summary>
    string? PlaceOfBirth { get; }

    /// <summary>
    ///   Whether the source marks the creator as known for adult content
    ///   only.
    /// </summary>
    bool IsRestricted { get; }

    /// <summary>
    /// All episode cast roles the creator have participated in.
    /// </summary>
    IEnumerable<ICast<IEpisode>> EpisodeCastRoles { get; }

    /// <summary>
    /// All movie cast roles the creator have participated in.
    /// </summary>
    IEnumerable<ICast<IMovie>> MovieCastRoles { get; }

    /// <summary>
    /// All series cast roles the creator have participated in.
    /// </summary>
    IEnumerable<ICast<ISeries>> SeriesCastRoles { get; }

    /// <summary>
    /// All episode crew roles the creator have participated in.
    /// </summary>
    IEnumerable<ICrew<IEpisode>> EpisodeCrewRoles { get; }

    /// <summary>
    /// All movie crew roles the creator have participated in.
    /// </summary>
    IEnumerable<ICrew<IMovie>> MovieCrewRoles { get; }

    /// <summary>
    /// All series crew roles the creator have participated in.
    /// </summary>
    IEnumerable<ICrew<ISeries>> SeriesCrewRoles { get; }

    /// <summary>
    ///   Whether the creator is a stub: an entry the core made for a credit
    ///   before its source wrote it, named only by what that carried. It
    ///   fills in once the source saves the creator.
    /// </summary>
    bool IsStub { get; }
}
