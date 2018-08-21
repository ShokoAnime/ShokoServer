using System;
using System.Collections.Generic;
using System.Linq;
using NutzCode.InMemoryIndex;
using Shoko.Models.Enums;
using Shoko.Models.Server;
using Shoko.Server.Databases;
using Shoko.Server.Models;
using Shoko.Server.Repositories.ReaderWriterLockExtensions;

namespace Shoko.Server.Repositories.Cached
{
    public class AniDB_VoteRepository : BaseRepository<AniDB_Vote, int>
    {
        private PocoIndex<int, AniDB_Vote, int> EntityIDs;

        private AniDB_VoteRepository()
        {

        }
                
        public AniDB_Vote GetByEntityAndType(int entID, AniDBVoteType voteType)
        {
            lock (Cache)
            {
                List<AniDB_Vote> cr = EntityIDs.GetMultiple(entID)?.Where(a => a.VoteType == (int) voteType).ToList();

                if (cr == null) return null;
                if (cr.Count <= 1) return cr.FirstOrDefault();

                {
                    bool first = true;
                    foreach (AniDB_Vote dbVote in cr)
                    {
                        if (first)
                        {
                            first = false;
                            continue;
                        }
                        Repo.AniDB_Vote.Delete(dbVote);
                    }

                    return cr.FirstOrDefault();
                }
            }
        }

        public List<AniDB_Vote> GetByEntity(int entID)
        {
            lock (RepoLock.ReaderLock())
            {
                if (IsCached)
                    return EntityIDs.GetMultiple(entID)?.ToList();
                return Table.Where(s => s.EntityID == entID).ToList();
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

        internal override void EndSave(AniDB_Vote entity, object returnFromBeginSave, object parameters)
        {
            base.EndSave(entity, returnFromBeginSave, parameters);

            switch (entity.VoteType)
            {
                case (int)AniDBVoteType.Anime:
                case (int)AniDBVoteType.AnimeTemp:
                    SVR_AniDB_Anime.UpdateStatsByAnimeID(entity.EntityID);
                    break;
                case (int)AniDBVoteType.Episode:
                    Repo.AnimeEpisode.Touch(() => Repo.AnimeEpisode.GetByID(entity.EntityID));
                    break;
            }
        }

        internal override void EndDelete(AniDB_Vote entity, object returnFromBeginDelete, object parameters)
        {
            base.EndDelete(entity, returnFromBeginDelete, parameters);

            switch (entity.VoteType)
            {
                case (int)AniDBVoteType.Anime:
                case (int)AniDBVoteType.AnimeTemp:
                    SVR_AniDB_Anime.UpdateStatsByAnimeID(entity.EntityID);
                    break;
                case (int)AniDBVoteType.Episode:
                    Repo.AnimeEpisode.Touch(() => Repo.AnimeEpisode.GetByID(entity.EntityID));
                    break;
            }
        }
    }
}
