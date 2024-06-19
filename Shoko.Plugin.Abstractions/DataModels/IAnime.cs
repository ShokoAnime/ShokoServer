using System;
using System.Collections.Generic;

#nullable enable
namespace Shoko.Plugin.Abstractions.DataModels;

public interface IAnime : ISeries
{
    #region To-be-removed

    /// <summary>
    /// Relations for the anime.
    /// </summary>
    [Obsolete("Use ISeries.RelatedSeries instead.")]
    IReadOnlyList<IRelatedAnime> Relations { get; }

    /// <summary>
    /// The number of total episodes in the series.
    /// </summary>
    [Obsolete("Use ISeries.EpisodeCounts instead.")]
    EpisodeCounts EpisodeCounts { get; }

    /// <summary>
    /// The AniDB Anime ID.
    /// </summary>
    [Obsolete("Use ID instead.")]
    int AnimeID { get; }

    #endregion
}
