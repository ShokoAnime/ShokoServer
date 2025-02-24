using System.Collections.Generic;
using System.Linq;
using Shoko.Server.Extensions;
using Shoko.Server.Repositories;
using Shoko.Server.Server;

#nullable enable
namespace Shoko.Server.Models.AniDB;

public class AniDB_Anime_Character
{
    #region Server DB columns

    public int AniDB_Anime_CharacterID { get; set; }

    public int AnimeID { get; set; }

    public int CharacterID { get; set; }

    public string Appearance { get; set; } = string.Empty;

    public CharacterAppearanceType AppearanceType { get; set; }

    public int Ordering { get; set; }

    public SVR_AniDB_Anime? Anime
        => RepoFactory.AniDB_Anime.GetByAnimeID(AnimeID);

    public IReadOnlyList<AniDB_Anime_Character_Creator> CreatorCrossReferences
        => RepoFactory.AniDB_Anime_Character_Creator.GetByCharacterIDAndAnimeID(CharacterID, AnimeID);

    public IReadOnlyList<AniDB_Creator> Creators
        => CreatorCrossReferences
            .OrderBy(a => a.Ordering)
            .Select(a => a.Creator)
            .WhereNotNull()
            .ToList();

    public AniDB_Character? Character
        => RepoFactory.AniDB_Character.GetByCharacterID(CharacterID);

    #endregion
}
