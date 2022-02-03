using System;
using System.Collections.Generic;

namespace Shoko.Plugin.Abstractions.DataModels
{
    public interface IEpisode
    {
        IReadOnlyList<AnimeTitle> Titles { get; }
        /// <summary>
        /// The AniDB Episode ID
        /// </summary>
        int EpisodeID { get; }
        /// <summary>
        /// The AniDB Anime ID. This is included for matching with the anime, if needed
        /// </summary>
        int AnimeID { get; }
        /// <summary>
        /// The runtime in seconds
        /// </summary>
        int Duration { get; }
        /// <summary>
        /// The Episode Number
        /// </summary>
        int Number { get; }
        /// <summary>
        /// Episode Type
        /// </summary>
        EpisodeType Type { get; }
        DateTime? AirDate { get; }
    }

    public enum EpisodeType
    {
        Episode = 1,
        Credits = 2,
        Special = 3,
        Trailer = 4,
        Parody = 5,
        Other = 6
    }
}