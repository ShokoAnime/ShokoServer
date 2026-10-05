using System;
using Shoko.Abstractions.Metadata.Enums;

namespace Shoko.Abstractions.Metadata.Anidb.Models;

/// <summary>
///   The start season a user set by hand for an AniDB anime, in place of
///   the one the yearly season rule works out. Kept by AniDB anime ID, so it
///   holds for an anime outside the collection or not yet in the local
///   cache.
/// </summary>
/// <param name="AnidbAnimeID">The AniDB anime ID.</param>
/// <param name="Year">The year of the season the anime starts in.</param>
/// <param name="Season">The season the anime starts in.</param>
/// <param name="CreatedAt">When the override was first set, in UTC.</param>
/// <param name="UpdatedAt">When the override was last changed, in UTC.</param>
/// <param name="UserID">
///   The local ID of the user who last set it, or <c>null</c> when the
///   system did.
/// </param>
public sealed record AnidbStartSeasonOverride(
    int AnidbAnimeID,
    int Year,
    YearlySeason Season,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    int? UserID
);
