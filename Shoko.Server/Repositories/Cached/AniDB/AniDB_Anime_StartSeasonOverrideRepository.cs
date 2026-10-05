using Shoko.Server.Databases;
using Shoko.Server.Models.AniDB;
using Shoko.Server.Utilities;

namespace Shoko.Server.Repositories.Cached.AniDB;

/// <summary>
///   Cached repository for <see cref="AniDB_Anime_StartSeasonOverride"/>,
///   the start seasons users set by hand for AniDB anime.
/// </summary>
/// <remarks>
///   Read for an anime each time its seasons are asked for, so it is
///   cached. The anime's cached seasons remember the override they were
///   worked out with and are worked out again once it changes, so a save
///   or delete here needs no further step.
/// </remarks>
/// <param name="databaseFactory">The database factory.</param>
public class AniDB_Anime_StartSeasonOverrideRepository(DatabaseFactory databaseFactory)
    : BaseCachedRepository<AniDB_Anime_StartSeasonOverride, int>(databaseFactory)
{
    private PocoIndex<int, AniDB_Anime_StartSeasonOverride, int>? _animeIDs;

    /// <inheritdoc/>
    protected override int SelectKey(AniDB_Anime_StartSeasonOverride entity)
        => entity.AniDB_Anime_StartSeasonOverrideID;

    /// <inheritdoc/>
    public override void PopulateIndexes()
        => _animeIDs = Cache.CreateIndex(entity => entity.AnimeID);

    /// <summary>
    ///   Gets the start season override of an AniDB anime.
    /// </summary>
    /// <param name="animeID">The AniDB anime ID.</param>
    /// <returns>The override, or <c>null</c> when none is set.</returns>
    public virtual AniDB_Anime_StartSeasonOverride? GetByAnimeID(int animeID)
        => _animeIDs!.GetOne(animeID);
}
