using System.Collections.Generic;
using Shoko.Abstractions.Metadata.Containers;
using Shoko.Abstractions.Metadata.Enums;

namespace Shoko.Abstractions.Metadata;

/// <summary>
/// Creator.
/// </summary>
public interface ICreator : IMetadata, IWithOverviews, IWithPrimaryImage, IWithUpdateDate, IWithResources
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
    /// The type of the creator.
    /// </summary>
    CreatorType Type { get; }

    /// <summary>
    ///   Other names the creator is known by, such as a pen name or a
    ///   spelling in another script, each with its language when known.
    /// </summary>
    IReadOnlyList<ITitle> AlternativeNames { get; }

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
}
