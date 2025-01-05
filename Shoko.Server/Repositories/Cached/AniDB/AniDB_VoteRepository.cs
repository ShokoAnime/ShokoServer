#nullable enable
using System.Collections.Generic;
using System.Linq;
using NutzCode.InMemoryIndex;
using Shoko.Models.Enums;
using Shoko.Models.Server;
using Shoko.Server.Databases;
using Shoko.Server.Scheduling;
using Shoko.Server.Scheduling.Jobs.Actions;

namespace Shoko.Server.Repositories.Cached.AniDB;

public class AniDB_VoteRepository : BaseCachedRepository<AniDB_Vote, int>
{
    private PocoIndex<int, AniDB_Vote, int>? _entityIDs;

    private PocoIndex<int, AniDB_Vote, (int, AniDBVoteType)>? _entityIDAndTypes;

    public AniDB_VoteRepository(JobFactory jobFactory, DatabaseFactory databaseFactory) : base(databaseFactory)
    {
        EndSaveCallback = cr =>
        {
            switch ((AniDBVoteType)cr.VoteType)
            {
                case AniDBVoteType.Anime:
                case AniDBVoteType.AnimeTemp:
                    jobFactory.CreateJob<RefreshAnimeStatsJob>(a => a.AnimeID = cr.EntityID).Process().GetAwaiter().GetResult();
                    break;
            }
        };
        EndDeleteCallback = cr =>
        {
            switch ((AniDBVoteType)cr.VoteType)
            {
                case AniDBVoteType.Anime:
                case AniDBVoteType.AnimeTemp:
                    jobFactory.CreateJob<RefreshAnimeStatsJob>(a => a.AnimeID = cr.EntityID).Process().GetAwaiter().GetResult();
                    break;
            }
        };
    }

    protected override int SelectKey(AniDB_Vote entity)
        => entity.AniDB_VoteID;

    public override void PopulateIndexes()
    {
        _entityIDs = new PocoIndex<int, AniDB_Vote, int>(Cache, a => a.EntityID);
        _entityIDAndTypes = new PocoIndex<int, AniDB_Vote, (int, AniDBVoteType)>(Cache, a => (a.EntityID, (AniDBVoteType)a.VoteType));
    }

    public AniDB_Vote? GetByEntityAndType(int entityID, AniDBVoteType voteType)
    {
        if (ReadLock(() => _entityIDAndTypes!.GetMultiple((entityID, voteType))) is not { } cr)
            return null;

        if (cr.Count <= 1)
            return cr.FirstOrDefault();

        return Lock(() =>
        {
            using var session = _databaseFactory.SessionFactory.OpenSession();
            foreach (var dbVote in cr.Skip(1))
            {
                using var transaction = session.BeginTransaction();
                DeleteWithOpenTransaction(session, dbVote);
                transaction.Commit();
            }

            return cr.FirstOrDefault();
        });
    }

    public IReadOnlyList<AniDB_Vote> GetByEntity(int entityID)
        => ReadLock(() => _entityIDs!.GetMultiple(entityID));

    public AniDB_Vote? GetByAnimeID(int animeID)
        => GetByEntityAndType(animeID, AniDBVoteType.Anime) ??
        GetByEntityAndType(animeID, AniDBVoteType.AnimeTemp);
}
