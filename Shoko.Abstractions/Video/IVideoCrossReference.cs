using System.Collections.Generic;
using Shoko.Abstractions.Metadata.Anidb;
using Shoko.Abstractions.Metadata.Shoko;
using Shoko.Abstractions.Metadata.Tmdb.CrossReferences;
using Shoko.Abstractions.Release;

namespace Shoko.Abstractions.Video;

/// <summary>
/// Video cross-reference.
/// </summary>
public interface IVideoCrossReference : IReleaseVideoCrossReference
{
    /// <summary>
    /// ED2K hash used for video identification.
    /// </summary>
    string ED2K { get; }

    /// <summary>
    /// File size used for video identification.
    /// </summary>
    long Size { get; }

    /// <summary>
    /// AniDB episode ID. Will be available even if <see cref="AnidbEpisode"/>
    /// is not available yet.
    /// </summary>
    new int AnidbEpisodeID { get; }

    /// <summary>
    /// AniDB anime ID. Will be available even if <see cref="AnidbAnime"/> is
    /// not available yet.
    /// </summary>
    new int AnidbAnimeID { get; }

    /// <summary>
    /// Cross-reference percentage range, if the video covers less than 100%
    /// of the episode, then this field tells roughly how much it covers.
    /// </summary>
    int Percentage { get; }

    /// <summary>
    /// Inferred cross-reference percentage group size. E.g. 1 for 100%, 2 for 50%, etc.
    /// </summary>
    int PercentageGroupSize { get; }

    /// <summary>
    /// The local video, if available.
    /// </summary>
    IVideo? Video { get; }

    /// <summary>
    /// The <see cref="IReleaseInfo"/> associated with this
    /// <see cref="IVideoCrossReference"/>.
    /// </summary>
    IReleaseInfo Release { get; }

    /// <summary>
    /// The AniDB episode, if available.
    /// </summary>
    IAnidbEpisode? AnidbEpisode { get; }

    /// <summary>
    /// The AniDB anime series, if available.
    /// </summary>
    IAnidbAnime? AnidbAnime { get; }

    /// <summary>
    /// The Shoko episode, if available.
    /// </summary>
    IShokoEpisode? ShokoEpisode { get; }

    /// <summary>
    /// The Shoko series, if available.
    /// </summary>
    IShokoSeries? ShokoSeries { get; }

    /// <summary>
    /// All Shoko series/AniDB anime ↔ TMDB show cross references linked to the Shoko series/AniDB anime.
    /// </summary>
    IReadOnlyList<ITmdbShowCrossReference> TmdbShowCrossReferences { get; }

    /// <summary>
    /// All Shoko series/AniDB anime ↔ TMDB season cross references linked to the Shoko series/AniDB anime.
    /// </summary>
    IReadOnlyList<ITmdbSeasonCrossReference> TmdbSeasonCrossReferences { get; }

    /// <summary>
    /// All Shoko/AniDB episode ↔ TMDB episode cross references linked to the Shoko/AniDB episode.
    /// </summary>
    IReadOnlyList<ITmdbEpisodeCrossReference> TmdbEpisodeCrossReferences { get; }

    /// <summary>
    /// All Shoko/AniDB episode ↔ TMDB movie cross references linked to the Shoko episode.
    /// </summary>
    IReadOnlyList<ITmdbMovieCrossReference> TmdbMovieCrossReferences { get; }
}
