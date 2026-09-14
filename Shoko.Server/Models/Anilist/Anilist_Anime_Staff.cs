using System;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Server.Repositories;

#nullable enable
namespace Shoko.Server.Models.Anilist;

/// <summary>
/// AniList production staff role for an anime. Mirrors
/// <see cref="AniDB.AniDB_Anime_Staff"/>.
/// </summary>
public class Anilist_Anime_Staff
{
    #region Database Columns

    /// <summary>
    /// Local database ID.
    /// </summary>
    public int Anilist_Anime_StaffID { get; set; }

    /// <summary>
    /// AniList Anime ID.
    /// </summary>
    public int AnilistAnimeID { get; set; }

    /// <summary>
    /// AniList Staff ID.
    /// </summary>
    public int AnilistCreatorID { get; set; }

    /// <summary>
    /// The role, as free-form text from AniList, e.g. "Director" or
    /// "Original Creator".
    /// </summary>
    public string Role { get; set; } = string.Empty;

    /// <summary>
    /// Ordering within the anime's staff.
    /// </summary>
    public int Ordering { get; set; }

    #endregion

    #region Computed Properties

    /// <summary>
    /// The abstract crew role type, derived from the free-form role text.
    /// </summary>
    public CrewRoleType CrewRoleType => ParseCrewRoleType(Role);

    /// <summary>
    /// Map an AniList free-form staff role to a <see cref="CrewRoleType"/>.
    /// AniList roles are free text, so this keys off the words that appear
    /// consistently in their data.
    /// </summary>
    public static CrewRoleType ParseCrewRoleType(string role)
    {
        if (string.IsNullOrWhiteSpace(role))
            return CrewRoleType.None;

        // Strip parenthesised qualifiers like "Director (eps 1-12)" before matching.
        var index = role.IndexOf('(');
        var normalized = (index > 0 ? role[..index] : role).Trim();
        if (normalized.Contains("Original Creator", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("Original Story", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("Original Work", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("Original Character Design", StringComparison.OrdinalIgnoreCase))
            return CrewRoleType.SourceWork;
        if (normalized.Contains("Character Design", StringComparison.OrdinalIgnoreCase))
            return CrewRoleType.CharacterDesign;
        if (normalized.Contains("Series Composition", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("Script", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("Screenplay", StringComparison.OrdinalIgnoreCase))
            return CrewRoleType.SeriesComposer;
        if (normalized.Contains("Music", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("Theme Song", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("Composer", StringComparison.OrdinalIgnoreCase))
            return CrewRoleType.Music;
        if (normalized.Contains("Producer", StringComparison.OrdinalIgnoreCase))
            return CrewRoleType.Producer;
        if (normalized.Contains("Director", StringComparison.OrdinalIgnoreCase))
            return CrewRoleType.Director;
        return CrewRoleType.None;
    }

    #endregion

    #region Constructors

    /// <summary>
    /// Default constructor for NHibernate.
    /// </summary>
    public Anilist_Anime_Staff() { }

    /// <summary>
    /// Creates a new staff role.
    /// </summary>
    public Anilist_Anime_Staff(int anilistAnimeId, int anilistCreatorId, string role, int ordering)
    {
        AnilistAnimeID = anilistAnimeId;
        AnilistCreatorID = anilistCreatorId;
        Role = role;
        Ordering = ordering;
    }

    #endregion

    #region Navigation Properties

    /// <summary>
    /// Gets the associated AniList anime.
    /// </summary>
    public Anilist_Anime? Anime => RepoFactory.Anilist_Anime.GetByAnilistAnimeID(AnilistAnimeID);

    /// <summary>
    /// Gets the associated AniList creator.
    /// </summary>
    public Anilist_Creator? Creator => RepoFactory.Anilist_Creator.GetByAnilistCreatorID(AnilistCreatorID);

    #endregion
}
