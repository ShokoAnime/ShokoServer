using System;
using System.Collections.Generic;
using System.Linq;
using NutzCode.InMemoryIndex;
using Shoko.Models.Enums;
using Shoko.Models.Server;
using Shoko.Server.Databases;
using Shoko.Server.Models;

namespace Shoko.Server.Repositories.Cached
{
    public class AniDB_VoteRepository : BaseCachedRepository<AniDB_Vote, int>
    {
        private PocoIndex<int, AniDB_Vote, int> EntityIDs;
        private PocoIndex<int, AniDB_Vote, (int, AniDBVoteType)> EntityIDAndTypes;

        public AniDB_VoteRepository()
        {
            EndSaveCallback = cr =>
            {
                switch (cr.VoteType)
                {
                    case (int) AniDBVoteType.Anime:
                    case (int) AniDBVoteType.AnimeTemp:
                        SVR_AniDB_Anime.UpdateStatsByAnimeID(cr.EntityID);
                        break;
                    case (int) AniDBVoteType.Episode:
                        var ep = RepoFactory.AnimeEpisode.GetByID(cr.EntityID);
                        RepoFactory.AnimeEpisode.Save(ep);
                        break;
                }
            };
            EndDeleteCallback = cr =>
            {
                switch (cr.VoteType)
                {
                    case (int) AniDBVoteType.Anime:
                    case (int) AniDBVoteType.AnimeTemp:
                        SVR_AniDB_Anime.UpdateStatsByAnimeID(cr.EntityID);
                        break;
                    case (int) AniDBVoteType.Episode:
                        var ep = RepoFactory.AnimeEpisode.GetByID(cr.EntityID);
                        RepoFactory.AnimeEpisode.Save(ep);
                        break;
                }
            };
        }

        public AniDB_Vote GetByEntityAndType(int entID, AniDBVoteType voteType)
        {
            Lock.EnterReadLock();
            var cr = EntityIDAndTypes.GetMultiple((entID, voteType));
            Lock.ExitReadLock();

            if (cr == null) return null;
            if (cr.Count <= 1) return cr.FirstOrDefault();

            lock (GlobalDBLock)
            {
                using var session = DatabaseFactory.SessionFactory.OpenSession();
                foreach (var dbVote in cr.Skip(1))
                {
                    using var transact = session.BeginTransaction();
                    RepoFactory.AniDB_Vote.DeleteWithOpenTransaction(session, dbVote);
                    transact.Commit();
                }

                return cr.FirstOrDefault();
            }
        }

        public List<AniDB_Vote> GetByEntity(int entID)
        {
            Lock.EnterReadLock();
            var result = EntityIDs.GetMultiple(entID)?.ToList();
            Lock.ExitReadLock();
            return result;
        }

        public AniDB_Vote GetByAnimeID(int animeID)
        {
            return GetByEntityAndType(animeID, AniDBVoteType.Anime) ??
                   GetByEntityAndType(animeID, AniDBVoteType.AnimeTemp);
        }

        public Dictionary<int, AniDB_Vote> GetByAnimeIDs(IReadOnlyCollection<int> animeIDs)
        {
            if (animeIDs == null)
                throw new ArgumentNullException(nameof(animeIDs));

            var votesByAnime = animeIDs.Select(a => new { AnimeID = a, Vote = GetByAnimeID(a) }).Where(a => a.Vote != null).ToDictionary(a => a.AnimeID, a => a.Vote);
            return votesByAnime;
        }

        protected override int SelectKey(AniDB_Vote entity) => entity.AniDB_VoteID;

        public override void PopulateIndexes()
        {
            EntityIDs = new PocoIndex<int, AniDB_Vote, int>(Cache, a => a.EntityID);
            EntityIDAndTypes = new PocoIndex<int, AniDB_Vote, (int, AniDBVoteType)>(Cache, a => (a.EntityID, (AniDBVoteType)a.VoteType));
        }

        public override void RegenerateDb()
        {
        }
    }
}
