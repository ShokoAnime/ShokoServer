using System;
using System.Collections.Generic;
using Shoko.Abstractions.Metadata.Anidb;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Shoko;

namespace Shoko.Abstractions.Metadata.Airing;

/// <summary>
///   One cached AniDB anime of a yearly season, with what a season view
///   sorts and groups it by and its next airing.
/// </summary>
public sealed record SeasonAnimeEntry
{
    /// <summary>
    ///   The AniDB anime.
    /// </summary>
    public required IAnidbAnime Anime { get; init; }

    /// <summary>
    ///   Its Shoko series, or <c>null</c> when it is not in the collection.
    /// </summary>
    public required IShokoSeries? Series { get; init; }

    /// <summary>
    ///   The preferred title: the series' one when it is in the collection,
    ///   else the anime's.
    /// </summary>
    public required string Title { get; init; }

    /// <summary>
    ///   The season it starts in, by the rule the seasons are listed with, or
    ///   <c>null</c> when it has no dates to go by.
    /// </summary>
    public required (int Year, YearlySeason Season)? StartSeason { get; init; }

    /// <summary>
    ///   Whether <see cref="StartSeason"/> was set by hand for the anime
    ///   (see <see cref="Anidb.Services.IAnidbService.SetStartSeasonOverride"/>)
    ///   rather than worked out by the rule.
    /// </summary>
    public required bool IsStartSeasonOverridden { get; init; }

    /// <summary>
    ///   The usual length of a regular episode, or <c>null</c> when no
    ///   regular episode has a known length.
    /// </summary>
    public required TimeSpan? EpisodeDuration { get; init; }

    /// <summary>
    ///   Whether it has a next airing, has finished, or neither is known.
    /// </summary>
    public required SeasonAnimeAiringStatus Status { get; init; }

    /// <summary>
    ///   The next airing: the earliest episode still to come, on the airing
    ///   the server prefers for it, or a date-only entry for an episode known
    ///   only by its AniDB air date.
    /// </summary>
    public required IEpisodeAiring? NextAiring { get; init; }

    /// <summary>
    ///   The other upcoming airings of the next airing's episode, the next
    ///   one on each other channel, in airing order.
    /// </summary>
    public required IReadOnlyList<IEpisodeAiring> OtherAirings { get; init; }
}
