using System;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Airing;
using Shoko.Abstractions.Metadata.Enums;

namespace Shoko.Server.Services.Airing;

/// <summary>
/// Where an airing sorts among airings that share a time: by AniDB anime, else
/// by its schedule's series, then by episode type and number. A batch release
/// thus lists in episode order.
/// </summary>
internal readonly record struct AiringEpisodeOrder : IComparable<AiringEpisodeOrder>
{
    /// <summary>
    /// The AniDB anime, or <c>null</c> when none is known.
    /// </summary>
    public int? AnidbAnimeID { get; private init; }

    /// <summary>
    /// The schedule's series for an airing with no AniDB anime, else an empty
    /// string.
    /// </summary>
    public string SeriesKey { get; private init; }

    /// <summary>
    /// The episode type as a number, regular episodes first, or
    /// <see cref="int.MaxValue"/> when unknown.
    /// </summary>
    public int TypeRank { get; private init; }

    /// <summary>
    /// The episode number, or <see cref="int.MaxValue"/> when unknown.
    /// </summary>
    public int Number { get; private init; }

    /// <summary>
    /// The order of an airing. AniDB numbering wins over the episode's own, so
    /// airings of one anime compare alike whichever source they come from.
    /// </summary>
    /// <param name="airing">The airing.</param>
    /// <returns>The order.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="airing"/> is <c>null</c>.</exception>
    public static AiringEpisodeOrder For(IEpisodeAiring airing)
    {
        ArgumentNullException.ThrowIfNull(airing);

        var anidbAnimeID = airing.AnidbAnimeID;
        var (type, number) = GetPlace(airing);
        return new()
        {
            AnidbAnimeID = anidbAnimeID,
            SeriesKey = anidbAnimeID is null ? airing.Schedule?.SeriesID.ToString() ?? string.Empty : string.Empty,
            TypeRank = type is { } value ? (int)value : int.MaxValue,
            Number = number ?? int.MaxValue,
        };
    }

    /// <summary>
    /// The episode type and number of an airing, from its AniDB episode, else
    /// its AniDB place, else its episode, else its place on the line.
    /// </summary>
    /// <param name="airing">The airing.</param>
    /// <returns>The type and number, each <c>null</c> when unknown.</returns>
    private static (EpisodeType? Type, int? Number) GetPlace(IEpisodeAiring airing)
    {
        if (airing.AnidbEpisode is { } anidbEpisode)
            return (anidbEpisode.Type, anidbEpisode.EpisodeNumber);
        if (airing.AnidbEpisodeNumber is { } anidbEpisodeNumber)
            return (EpisodeType.Episode, anidbEpisodeNumber);
        if (((IEpisode?)airing.ShokoEpisode ?? airing.Episode) is { } episode)
            return (episode.Type, episode.EpisodeNumber);
        if (airing.EpisodeNumber is { } episodeNumber)
            return (EpisodeType.Episode, episodeNumber);

        return (null, null);
    }

    /// <inheritdoc/>
    public int CompareTo(AiringEpisodeOrder other)
    {
        // An airing with an AniDB anime sorts before one without.
        var result = (AnidbAnimeID, other.AnidbAnimeID) switch
        {
            ({ } left, { } right) => left.CompareTo(right),
            (not null, null) => -1,
            (null, not null) => 1,
            _ => string.CompareOrdinal(SeriesKey, other.SeriesKey),
        };
        if (result is not 0)
            return result;

        result = TypeRank.CompareTo(other.TypeRank);
        return result is not 0 ? result : Number.CompareTo(other.Number);
    }
}
