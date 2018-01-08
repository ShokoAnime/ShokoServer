using System;
using System.Collections.Generic;
using System.Linq;
using NutzCode.InMemoryIndex;
using Shoko.Models.Enums;
using Shoko.Models.Server;
using Shoko.Server.Models;

namespace Shoko.Server.Repositories.Repos
{
    public class AniDB_VoteRepository : BaseRepository<AniDB_Vote, int>
    {
        private PocoIndex<int, AniDB_Vote, int> EntityIDs;
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
            switch (entity.VoteType)
            {
                case (int)AniDBVoteType.Anime:
                case (int)AniDBVoteType.AnimeTemp:
                    SVR_AniDB_Anime.UpdateStatsByAnimeID(entity.EntityID);
                    break;
                case (int)AniDBVoteType.Episode:
                    Repo.AnimeEpisode.BeginUpdate(entity.EntityID)?.Commit();
                    break;
            }
        }

        internal override void EndDelete(AniDB_Vote entity, object returnFromBeginDelete, object parameters)
        {
            switch (entity.VoteType)
            {
                case (int)AniDBVoteType.Anime:
                case (int)AniDBVoteType.AnimeTemp:
                    SVR_AniDB_Anime.UpdateStatsByAnimeID(entity.EntityID);
                    break;
                case (int)AniDBVoteType.Episode:
                    Repo.AnimeEpisode.BeginUpdate(entity.EntityID)?.Commit();
                    break;
            }
        }

        public AniDB_Vote GetByEntityAndType(int entID, AniDBVoteType voteType)
        {
            List<AniDB_Vote> cr;
            using (CacheLock.ReaderLock())
            {
                cr = IsCached
                    ? EntityIDs.GetMultiple(entID)?.Where(a => a.VoteType == (int) voteType).ToList()
                    : Table.Where(a => a.EntityID == entID && a.VoteType == (int) voteType).ToList();
            }

            if (cr==null || cr.Count == 0)
                return null;
            if (cr.Count > 1)
               Delete(cr.GetRange(1, cr.Count - 1));
            return cr[0];
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

        public Dictionary<int, AniDB_Vote> GetByAnimeIDs(IEnumerable<int> animeIDs)
        {
            //TODO Possible Optimization
            if (animeIDs == null)
                throw new ArgumentNullException(nameof(animeIDs));

            var votesByAnime = animeIDs.Where(a => GetByAnimeID(a) != null).ToDictionary(a => a, GetByAnimeID);

            return votesByAnime;
        }




    }
}