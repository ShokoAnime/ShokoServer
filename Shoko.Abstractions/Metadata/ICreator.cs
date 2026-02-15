using System.Collections.Generic;
using Shoko.Abstractions.Enums;
using Shoko.Abstractions.Metadata.Containers;

namespace Shoko.Abstractions.Metadata;

/// <summary>
/// Creator.
/// </summary>
public interface ICreator : IMetadata<int>, IWithDescriptions, IWithPortraitImage
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
