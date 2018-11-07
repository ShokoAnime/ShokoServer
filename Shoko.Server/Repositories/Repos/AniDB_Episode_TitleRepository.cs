using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Models.Server;
using Shoko.Server.Repositories.Cache;
using Shoko.Server.Repositories.ReaderWriterLockExtensions;


namespace Shoko.Server.Repositories.Repos
{
    public class AniDB_Episode_TitleRepository : BaseRepository<AniDB_Episode_Title, int>//BaseCachedRepository<AniDB_Episode_Title, int>
    {
        private PocoIndex<int, AniDB_Episode_Title, int> Episodes;

        internal override void PopulateIndexes()
        {
            Episodes = new PocoIndex<int, AniDB_Episode_Title, int>(Cache, a => a.AniDB_EpisodeID);
        }
        internal override int SelectKey(AniDB_Episode_Title entity)
        {
            return entity.AniDB_Episode_TitleID;
        }

        public List<AniDB_Episode_Title> GetByAnimeID(int id)
        {
            using (RepoLock.ReaderLock())
            {
                if (IsCached) return Episodes.GetMultiple(id);
                return Table.Where(s => s.AniDB_EpisodeID == id).ToList();
            }
        }
        public List<AniDB_Episode_Title> GetByEpisodeIDAndLanguage(int id, string language)
        {
            using (RepoLock.ReaderLock())
            {
                if (IsCached) return Episodes.GetMultiple(id).Where(a =>
                   a.Language.Equals(language, StringComparison.InvariantCultureIgnoreCase)).ToList();
                return Table.Where(s => s.AniDB_EpisodeID == id && s.Language.Equals(language, StringComparison.InvariantCultureIgnoreCase)).ToList();
            }
        }

        public List<AniDB_Episode_Title> GetByEpisodeID(int ID)
        {
            using (RepoLock.ReaderLock())
            {
                if (IsCached) return Episodes.GetMultiple(ID);
                return Table.Where(s => s.AniDB_EpisodeID == ID).ToList();
            }
        }

        internal override void ClearIndexes() => Episodes = null;
    }
}
