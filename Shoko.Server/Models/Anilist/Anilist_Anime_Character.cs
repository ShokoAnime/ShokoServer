using System.Collections.Generic;
using System.Linq;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Server.Repositories;

#nullable enable
namespace Shoko.Server.Models.Anilist;

/// <summary>
/// AniList Anime ↔ Character relationship.
/// </summary>
public class Anilist_Anime_Character
{
    #region Database Columns

    /// <summary>
    /// Local database ID.
    /// </summary>
    public int Anilist_Anime_CharacterID { get; set; }

    /// <summary>
    /// AniList Anime ID.
    /// </summary>
    public int AnilistAnimeID { get; set; }

    /// <summary>
    /// AniList Character ID.
    /// </summary>
    public int AnilistCharacterID { get; set; }

    /// <summary>
    /// The character's role in the anime, as reported by AniList
    /// (<c>MAIN</c>, <c>SUPPORTING</c> or <c>BACKGROUND</c>).
    /// </summary>
    public string Role { get; set; } = string.Empty;

    /// <summary>
    /// Ordering within the anime.
    /// </summary>
    public int Ordering { get; set; }

    #endregion

    #region Computed Properties

    /// <summary>
    /// The abstract cast role type.
    /// </summary>
    public CastRoleType CastRoleType => Role.ToUpperInvariant() switch
    {
        "MAIN" => CastRoleType.MainCharacter,
        "SUPPORTING" => CastRoleType.MinorCharacter,
        "BACKGROUND" => CastRoleType.BackgroundCharacter,
        _ => CastRoleType.None,
    };

    #endregion

    #region Constructors

    /// <summary>
    /// Default constructor for NHibernate.
    /// </summary>
    public Anilist_Anime_Character() { }

    /// <summary>
    /// Creates a new anime-character relationship.
    /// </summary>
    public Anilist_Anime_Character(int anilistAnimeId, int anilistCharacterId, string role, int ordering)
    {
        AnilistAnimeID = anilistAnimeId;
        AnilistCharacterID = anilistCharacterId;
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
    /// Gets the associated AniList character.
    /// </summary>
    public Anilist_Character? Character => RepoFactory.Anilist_Character.GetByAnilistCharacterID(AnilistCharacterID);

    /// <summary>
    /// Gets the voice actor cross-references for the character in the anime.
    /// </summary>
    public IReadOnlyList<Anilist_Anime_Character_Creator> CreatorCrossReferences
        => RepoFactory.Anilist_Anime_Character_Creator.GetByAnilistAnimeAndCharacterIDs(AnilistAnimeID, AnilistCharacterID);

    /// <summary>
    /// Gets the voice actors for the character in the anime.
    /// </summary>
    public IReadOnlyList<Anilist_Creator> Creators
        => CreatorCrossReferences.OrderBy(xref => xref.Ordering).Select(xref => xref.Creator).WhereNotNull().ToList();

    #endregion
}
