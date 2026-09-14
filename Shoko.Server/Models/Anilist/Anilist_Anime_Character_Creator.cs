using Shoko.Server.Repositories;

#nullable enable
namespace Shoko.Server.Models.Anilist;

/// <summary>
/// AniList voice actor for a character in an anime. Mirrors
/// <see cref="AniDB.AniDB_Anime_Character_Creator"/>.
/// </summary>
public class Anilist_Anime_Character_Creator
{
    #region Database Columns

    /// <summary>
    /// Local database ID.
    /// </summary>
    public int Anilist_Anime_Character_CreatorID { get; set; }

    /// <summary>
    /// AniList Anime ID.
    /// </summary>
    public int AnilistAnimeID { get; set; }

    /// <summary>
    /// AniList Character ID.
    /// </summary>
    public int AnilistCharacterID { get; set; }

    /// <summary>
    /// AniList Staff ID of the voice actor.
    /// </summary>
    public int AnilistCreatorID { get; set; }

    /// <summary>
    /// Notes about the role, e.g. "(young)", if set.
    /// </summary>
    public string? RoleNotes { get; set; }

    /// <summary>
    /// The dub group, if set.
    /// </summary>
    public string? DubGroup { get; set; }

    /// <summary>
    /// Ordering within the character's voice actors.
    /// </summary>
    public int Ordering { get; set; }

    #endregion

    #region Constructors

    /// <summary>
    /// Default constructor for NHibernate.
    /// </summary>
    public Anilist_Anime_Character_Creator() { }

    /// <summary>
    /// Creates a new voice actor link.
    /// </summary>
    public Anilist_Anime_Character_Creator(int anilistAnimeId, int anilistCharacterId, int anilistCreatorId, int ordering)
    {
        AnilistAnimeID = anilistAnimeId;
        AnilistCharacterID = anilistCharacterId;
        AnilistCreatorID = anilistCreatorId;
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

    /// <summary>
    /// Gets the associated AniList character.
    /// </summary>
    public Anilist_Character? Character => RepoFactory.Anilist_Character.GetByAnilistCharacterID(AnilistCharacterID);

    /// <summary>
    /// Gets the anime-character cross-reference this voice actor belongs to.
    /// </summary>
    public Anilist_Anime_Character? CharacterCrossReference
        => RepoFactory.Anilist_Anime_Character.GetByAnilistAnimeAndCharacterIDs(AnilistAnimeID, AnilistCharacterID);

    #endregion
}
