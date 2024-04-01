using System;
using System.Collections.Generic;

#nullable enable
namespace Shoko.Plugin.Abstractions.DataModels;

public interface IAnime : ISeries
{
    /// <summary>
    /// Relations for the anime.
    /// </summary>
    IReadOnlyList<IRelatedAnime> Relations { get; }

    /// <summary>
    /// The number of total episodes in the series.
    /// </summary>
    EpisodeCounts EpisodeCounts { get; }

    #region To-be-removed

    /// <summary>
    /// The AniDB Anime ID.
    /// </summary>
    [Obsolete("Use ID instead.")]
    int AnimeID { get; }

    #endregion
}
