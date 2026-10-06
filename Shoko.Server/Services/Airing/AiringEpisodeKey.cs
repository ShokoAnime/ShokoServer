using System;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Airing;

#nullable enable
namespace Shoko.Server.Services.Airing;

/// <summary>
/// Which episode an airing counts as when a read de-duplicates, prefers,
/// reduces or merges airings. It is worked out within one read and never kept
/// past it.
/// </summary>
internal readonly record struct AiringEpisodeKey
{
    /// <summary>
    /// What the key names.
    /// </summary>
    public AiringEpisodeKeyKind Kind { get; private init; }

    /// <summary>
    /// The source of the episode, or of the series for a series position.
    /// </summary>
    public MetadataSource Source { get; private init; }

    /// <summary>
    /// The episode's ID, the AniDB anime's ID, or the series' ID, by
    /// <see cref="Kind"/>.
    /// </summary>
    public string ID { get; private init; }

    /// <summary>
    /// The season a series position is on, or an empty string for the whole
    /// series and for the other kinds.
    /// </summary>
    public string SeasonID { get; private init; }

    /// <summary>
    /// The episode number of a position, or <c>0</c> for a real episode.
    /// </summary>
    public int Number { get; private init; }

    /// <summary>
    /// The key of a real episode.
    /// </summary>
    /// <param name="source">The source of the episode.</param>
    /// <param name="id">The ID of the episode within its source.</param>
    /// <returns>The key.</returns>
    public static AiringEpisodeKey ForEpisode(MetadataSource source, string id)
        => new() { Kind = AiringEpisodeKeyKind.Episode, Source = source, ID = id, SeasonID = string.Empty };

    /// <summary>
    /// The key of a real episode.
    /// </summary>
    /// <param name="id">The ID of the episode.</param>
    /// <returns>The key.</returns>
    public static AiringEpisodeKey ForEpisode(MetadataGuid id)
        => ForEpisode(id.Source, id.ID);

    /// <summary>
    /// The key of a regular episode's place in an AniDB anime, whether AniDB
    /// lists the episode yet or not.
    /// </summary>
    /// <param name="anidbAnimeID">The AniDB anime.</param>
    /// <param name="episodeNumber">The regular episode number.</param>
    /// <returns>The key.</returns>
    public static AiringEpisodeKey ForAnidbPosition(int anidbAnimeID, int episodeNumber)
        => new() { Kind = AiringEpisodeKeyKind.AnidbPosition, Source = MetadataSource.AniDB, ID = anidbAnimeID.ToString(), SeasonID = string.Empty, Number = episodeNumber };

    /// <summary>
    /// The key of a place on a provider series' numbered line, for an airing
    /// nothing else places.
    /// </summary>
    /// <param name="source">The source of the series.</param>
    /// <param name="seriesID">The ID of the series within its source.</param>
    /// <param name="seasonID">The season the line is on, or an empty string for the whole series.</param>
    /// <param name="episodeNumber">The episode number on the line.</param>
    /// <returns>The key.</returns>
    public static AiringEpisodeKey ForSeriesPosition(MetadataSource source, string seriesID, string seasonID, int episodeNumber)
        => new() { Kind = AiringEpisodeKeyKind.SeriesPosition, Source = source, ID = seriesID, SeasonID = seasonID, Number = episodeNumber };

    /// <summary>
    /// The key of an airing nothing else can name, which only ever merges with
    /// itself.
    /// </summary>
    /// <param name="airingID">The airing's ID.</param>
    /// <returns>The key.</returns>
    public static AiringEpisodeKey ForAiring(Guid airingID)
        => new() { Kind = AiringEpisodeKeyKind.Airing, Source = MetadataSource.Shoko, ID = airingID.ToString(), SeasonID = string.Empty };

    /// <summary>
    /// The key an airing counts as: the one the read gathered it under, else
    /// its AniDB anime and regular episode number, else its AniDB or shoko
    /// episode, else the episode the read resolved it for, else what
    /// <see cref="ForUnlinked"/> names.
    /// </summary>
    /// <param name="airing">The airing.</param>
    /// <returns>The key.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="airing"/> is <c>null</c>.</exception>
    public static AiringEpisodeKey For(IEpisodeAiring airing)
    {
        ArgumentNullException.ThrowIfNull(airing);

        if (airing is EpisodeAiringView { TargetKey: { } targetKey })
            return targetKey;
        if (airing.AnidbAnimeID is { } anidbAnimeID && airing.AnidbEpisodeNumber is { } anidbEpisodeNumber)
            return ForAnidbPosition(anidbAnimeID, anidbEpisodeNumber);
        if (airing.AnidbEpisode is { } anidbEpisode)
            return ForEpisode(anidbEpisode.ID);
        if (airing.ShokoEpisode is { } shokoEpisode)
            return ForEpisode(shokoEpisode.ID);
        if (airing is EpisodeAiringView { ResolvedFor: { } resolvedFor })
            return ForEpisode(resolvedFor.ID);

        return ForUnlinked(airing);
    }

    /// <summary>
    /// The key an airing counts as without following any link: its own
    /// episode, else its place on its schedule's line.
    /// </summary>
    /// <param name="airing">The airing.</param>
    /// <returns>The key.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="airing"/> is <c>null</c>.</exception>
    public static AiringEpisodeKey ForUnlinked(IEpisodeAiring airing)
    {
        ArgumentNullException.ThrowIfNull(airing);

        if (airing.EpisodeID is { } episodeID)
            return ForEpisode(episodeID);
        if (airing.Schedule is { } schedule && airing.EpisodeNumber is { } episodeNumber)
            return ForSeriesPosition(schedule.SeriesID.Source, schedule.SeriesID.ID, schedule.SeasonID?.ID ?? string.Empty, episodeNumber);

        return ForAiring(airing.ID);
    }
}

/// <summary>
/// What an <see cref="AiringEpisodeKey"/> names.
/// </summary>
internal enum AiringEpisodeKeyKind
{
    /// <summary>
    /// A real episode, of any source.
    /// </summary>
    Episode = 0,

    /// <summary>
    /// A regular episode's place in an AniDB anime.
    /// </summary>
    AnidbPosition = 1,

    /// <summary>
    /// A place on a provider series' numbered line.
    /// </summary>
    SeriesPosition = 2,

    /// <summary>
    /// One airing, which nothing else names.
    /// </summary>
    Airing = 3,
}
