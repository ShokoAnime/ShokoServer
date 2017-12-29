using System;
using System.Collections.Generic;
using System.Linq;
using NutzCode.InMemoryIndex;
using Shoko.Models.Enums;
using Shoko.Models.Server;
using Shoko.Server.Databases;
using Shoko.Server.Models;

namespace Shoko.Server.RepositoriesV2.Repos
{
    public class AniDB_VoteRepository : BaseRepository<AniDB_Vote, int>
    {
        private PocoIndex<int, AniDB_Vote, int> EntityIDs;

        private AniDB_VoteRepository()
        {
            EndSaveCallback = (cr) =>
            {
                switch (cr.VoteType) {
                    case (int) AniDBVoteType.Anime:
                    case (int) AniDBVoteType.AnimeTemp:
                        SVR_AniDB_Anime.UpdateStatsByAnimeID(cr.EntityID);
                        break;
                    case (int) AniDBVoteType.Episode:
                        SVR_AnimeEpisode ep = RepoFactory.AnimeEpisode.GetByID(cr.EntityID);
                        RepoFactory.AnimeEpisode.Save(ep);
                        break;
                }
            };
            EndDeleteCallback = (cr) =>
            {
                switch (cr.VoteType) {
                    case (int) AniDBVoteType.Anime:
                    case (int) AniDBVoteType.AnimeTemp:
                        SVR_AniDB_Anime.UpdateStatsByAnimeID(cr.EntityID);
                        break;
                    case (int) AniDBVoteType.Episode:
                        SVR_AnimeEpisode ep = RepoFactory.AnimeEpisode.GetByID(cr.EntityID);
                        RepoFactory.AnimeEpisode.Save(ep);
                        break;
                }
            };
        }

       
        public AniDB_Vote GetByEntityAndType(int entID, AniDBVoteType voteType)
        {
            lock (Cache)
            {
                List<AniDB_Vote> cr = EntityIDs.GetMultiple(entID)?.Where(a => a.VoteType == (int) voteType).ToList();

                if (cr == null) return null;
                if (cr.Count <= 1) return cr.FirstOrDefault();

                lock (globalDBLock)
                {
                    using (var session = DatabaseFactory.SessionFactory.OpenSession())
                    {
                        bool first = true;
                        foreach (AniDB_Vote dbVote in cr)
                        {
                            if (first)
                            {
                                first = false;
                                continue;
                            }
                            using (var transact = session.BeginTransaction())
                            {
                                RepoFactory.AniDB_Vote.DeleteWithOpenTransaction(session, dbVote);
                                transact.Commit();
                            }
                        }

                        return cr.FirstOrDefault();
                    }
                }
            }
        }

        public List<AniDB_Vote> GetByEntity(int entID)
        {
            using(CacheLock.ReaderLock())
            {
                return IsCached ? EntityIDs.GetMultiple(entID)?.ToList() : Table.Where(a=>a.EntityID==entID).ToList();
            }
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

            var votesByAnime = animeIDs.Where(a => GetByAnimeID(a) != null).ToDictionary(a => a, GetByAnimeID);

            return votesByAnime;
        }

        internal override int SelectKey(AniDB_Vote entity) => entity.AniDB_VoteID;

        internal override void PopulateIndexes()
        {
            EntityIDs = new PocoIndex<int, AniDB_Vote, int>(Cache, a => a.EntityID);
        }

        internal override void ClearIndexes()
        {
            EntityIDs = null;
        }


    }
}