using System;
using Shoko.Abstractions.Metadata.Anidb.Models;
using Shoko.Abstractions.Metadata.Enums;

namespace Shoko.Server.Models.AniDB;

/// <summary>
///   The start season a user set by hand for an AniDB anime, in place of
///   the one the season rule works out. Keyed by the AniDB anime ID, so it
///   holds for an anime outside the collection or not yet in the local
///   cache, and survives its series being removed and added again.
/// </summary>
public class AniDB_Anime_StartSeasonOverride
{
    #region Database Columns

    /// <summary>
    ///   Local database ID.
    /// </summary>
    public int AniDB_Anime_StartSeasonOverrideID { get; set; }

    /// <summary>
    ///   The AniDB anime ID. One override per anime.
    /// </summary>
    public int AnimeID { get; set; }

    /// <summary>
    ///   The year of the season the anime starts in.
    /// </summary>
    public int Year { get; set; }

    /// <summary>
    ///   The season the anime starts in.
    /// </summary>
    public YearlySeason Season { get; set; }

    /// <summary>
    ///   The local ID of the user who last set it, or <c>null</c> when the
    ///   system did.
    /// </summary>
    public int? UserID { get; set; }

    /// <summary>
    ///   When the override was first set, in UTC.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    ///   When the override was last changed, in UTC.
    /// </summary>
    public DateTime UpdatedAt { get; set; }

    #endregion

    #region Helpers

    /// <summary>
    ///   The override as the abstractions hand it out.
    /// </summary>
    /// <returns>The override.</returns>
    public AnidbStartSeasonOverride ToAbstraction()
        => new(AnimeID, Year, Season, CreatedAt, UpdatedAt, UserID);

    #endregion
}
