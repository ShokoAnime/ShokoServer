using System.Collections.Generic;
using System.Linq;
using NHibernate;
using Shoko.Server.Models.AniDB;
using Shoko.Server.Models.CrossReference;
using Shoko.Server.Models.Trakt;
using Shoko.Server.Repositories;

#nullable enable
namespace Shoko.Server.Extensions;

public static class ModelDatabase
{
    public static AniDB_Creator? GetCreator(this AniDB_Character character)
        => RepoFactory.AniDB_Anime_Character_Creator.GetByCharacterID(character.CharacterID) is { Count: > 0 } characterVAs
            ? characterVAs.OrderBy(x => x.AnimeID).ThenBy(x => x.Ordering).First().Creator
            : null;

    public static IReadOnlyList<AniDB_Anime_Character> GetRoles(this AniDB_Character character)
        => RepoFactory.AniDB_Anime_Character.GetByCharacterID(character.CharacterID);
}
