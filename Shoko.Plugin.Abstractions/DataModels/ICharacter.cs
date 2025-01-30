using System.Collections.Generic;
using Shoko.Plugin.Abstractions.Enums;

namespace Shoko.Plugin.Abstractions.DataModels;

/// <summary>
/// Character.
/// </summary>
public interface ICharacter : IMetadata<int>, IWithDescriptions, IWithPortraitImage
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
