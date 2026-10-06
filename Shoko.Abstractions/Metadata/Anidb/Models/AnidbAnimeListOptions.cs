using System;
using System.Collections.Generic;
using Shoko.Abstractions.Filtering;
using Shoko.Abstractions.Metadata.Anidb.Enums;
using Shoko.Abstractions.Metadata.Anidb.Services;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.User;

namespace Shoko.Abstractions.Metadata.Anidb.Models;

/// <summary>
///   Filters and order for listing the cached AniDB anime through
///   <see cref="IAnidbService.GetCachedAnime"/>. Every filter left unset
///   lets everything through.
/// </summary>
public record AnidbAnimeListOptions
{
    /// <summary>
    ///   Optional. Only anime whose preferred title starts with this text,
    ///   ignoring case.
    /// </summary>
    public string? TitlePrefix { get; init; }

    /// <summary>
    ///   Optional. Only anime in any of these seasons, by the rule on
    ///   <see cref="Containers.IWithYearlySeasons"/>. Seasons after the one
    ///   following the season under way are yet to be decided and match
    ///   nothing.
    /// </summary>
    public IReadOnlyCollection<(int Year, YearlySeason Season)>? Seasons { get; init; }

    /// <summary>
    ///   Optional. Only anime of these types.
    /// </summary>
    public IReadOnlyCollection<AnimeType>? Types { get; init; }

    /// <summary>
    ///   Optional. Only anime with a stored airing on one of these channels,
    ///   hidden or not: in a season when it is counted for that season, or,
    ///   for the season under way or a later one, still to come. Without
    ///   <see cref="Seasons"/>, any such airing will do.
    /// </summary>
    /// <remarks>
    ///   An airing is in the calendar quarter it airs in. Old seasons may
    ///   have no airings left, since the airing schedule service keeps them
    ///   only for its retention window, so they count fewer anime or none.
    /// </remarks>
    public IReadOnlySet<Guid>? ChannelIDs { get; init; }

    /// <summary>
    ///   Optional. Whether to keep the anime with a Shoko series, those
    ///   without one, or both. Defaults to both.
    /// </summary>
    public InclusionFilter InCollection { get; init; } = InclusionFilter.True;

    /// <summary>
    ///   Optional. Whether to keep the restricted anime, the unrestricted
    ///   ones, or both. Defaults to both.
    /// </summary>
    public InclusionFilter IncludeRestricted { get; init; } = InclusionFilter.True;

    /// <summary>
    ///   Optional. Only the anime of the Shoko series the filter passes,
    ///   evaluated for <see cref="User"/>. Anime without a Shoko series are
    ///   left out, and series without local files only when the filter says
    ///   so. With a sorting expression, the filter's order replaces
    ///   <see cref="OrderBy"/>.
    /// </summary>
    /// <remarks>
    ///   The filter is evaluated once per read. A filter that depends on the
    ///   user needs <see cref="User"/> set, or the read throws an
    ///   <see cref="ArgumentNullException"/>.
    /// </remarks>
    public IFilter? Filter { get; init; }

    /// <summary>
    ///   Optional. Only the anime this user is allowed to see.
    /// </summary>
    public IUser? User { get; init; }

    /// <summary>
    ///   Optional. The order. Defaults to <see cref="AnidbAnimeListOrder.AirDate"/>
    ///   when <see cref="Seasons"/> is set, else to
    ///   <see cref="AnidbAnimeListOrder.Title"/>. Ignored when
    ///   <see cref="Filter"/> has a sorting expression.
    /// </summary>
    public AnidbAnimeListOrder? OrderBy { get; init; }

    /// <summary>
    ///   Optional. The time the listing is as of, in UTC, which decides the
    ///   season under way, by the server's time zone, and which airings on
    ///   <see cref="ChannelIDs"/> are still to come. <c>null</c> means now,
    ///   by the service's clock. A time without a kind is read as UTC.
    /// </summary>
    public DateTime? At { get; init; }
}
