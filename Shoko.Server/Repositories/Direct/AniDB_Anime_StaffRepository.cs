using System.Collections.Generic;
using System.Linq;
using Shoko.Server.Databases;
using Shoko.Server.Models.AniDB;
using Shoko.Server.Server;

namespace Shoko.Server.Repositories.Direct;

public class AniDB_Anime_StaffRepository(DatabaseFactory databaseFactory) : BaseDirectRepository<AniDB_Anime_Staff, int>(databaseFactory)
{
    private const int ChunkSize = 500;

    public List<AniDB_Anime_Staff> GetByAnimeID(int animeID)
    {
        using var session = _databaseFactory.SessionFactory.OpenStatelessSession();
        return session.Query<AniDB_Anime_Staff>()
            .Where(a => a.AnimeID == animeID)
            .ToList();
    }

    public List<AniDB_Anime_Staff> GetByCreatorID(int creatorID)
    {
        using var session = _databaseFactory.SessionFactory.OpenStatelessSession();
        return session.Query<AniDB_Anime_Staff>()
            .Where(a => a.CreatorID == creatorID)
            .ToList();
    }

    /// <summary>
    ///   The studio roles of many anime at once, in one query per few
    ///   hundred anime.
    /// </summary>
    /// <param name="animeIDs">The AniDB anime IDs.</param>
    /// <returns>The roles of <see cref="CreatorRoleType.Studio"/>, in no set order.</returns>
    public virtual IReadOnlyList<AniDB_Anime_Staff> GetStudiosByAnimeIDs(IReadOnlyCollection<int> animeIDs)
    {
        if (animeIDs.Count is 0)
            return [];

        using var session = _databaseFactory.SessionFactory.OpenStatelessSession();
        return
        [
            .. animeIDs.Distinct()
                .Chunk(ChunkSize)
                .SelectMany(chunk => session.Query<AniDB_Anime_Staff>()
                    .Where(a => a.RoleType == CreatorRoleType.Studio && chunk.Contains(a.AnimeID))
                    .ToList()),
        ];
    }
}
