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
    ///   Optional. Whether to keep the anime whose Shoko series has no
    ///   local files, leave them out, or keep only them. Anime without a
    ///   Shoko series count as having files. Defaults to keeping them.
    /// </summary>
    public InclusionFilter IncludeMissing { get; init; } = InclusionFilter.True;

    /// <summary>
    ///   Optional. Only the anime this user is allowed to see.
    /// </summary>
    public IUser? User { get; init; }

    /// <summary>
    ///   Optional. The order. Defaults to <see cref="AnidbAnimeListOrder.AirDate"/>
    ///   when <see cref="Seasons"/> is set, else to
    ///   <see cref="AnidbAnimeListOrder.Title"/>.
    /// </summary>
    public AnidbAnimeListOrder? OrderBy { get; init; }
}
