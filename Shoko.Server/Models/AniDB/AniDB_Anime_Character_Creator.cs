using Shoko.Server.Repositories;

#nullable enable
namespace Shoko.Server.Models.AniDB;

public class AniDB_Anime_Character_Creator
{
    #region DB columns

    public int AniDB_Anime_Character_CreatorID { get; set; }

    public int AnimeID { get; set; }

    public int CharacterID { get; set; }

    public int CreatorID { get; set; }

    public int Ordering { get; set; }

    #endregion

    public AniDB_Anime? Anime
        => RepoFactory.AniDB_Anime.GetByAnimeID(AnimeID);

    public AniDB_Creator? Creator
        => RepoFactory.AniDB_Creator.GetByCreatorID(CreatorID);

    public AniDB_Character? Character
        => RepoFactory.AniDB_Character.GetByCharacterID(CharacterID);

    public AniDB_Anime_Character? CharacterCrossReference
        => RepoFactory.AniDB_Anime_Character.GetByAnimeIDAndCharacterID(AnimeID, CharacterID);
}
