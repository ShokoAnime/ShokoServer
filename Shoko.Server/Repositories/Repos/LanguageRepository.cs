using System.Collections.Generic;
using System.Linq;
using Shoko.Models.Server;
using Shoko.Server.Repositories.ReaderWriterLockExtensions;
using Shoko.Server.Repositories.Cache;

namespace Shoko.Server.Repositories.Repos
{
    public class LanguageRepository : BaseRepository<Language, int>
    {

        private PocoIndex<int, Language, string> LanguageNames;
       
        internal override int SelectKey(Language entity) => entity.LanguageID;
            
        internal override void PopulateIndexes()
        {
            LanguageNames = new PocoIndex<int, Language, string>(Cache, a => a.LanguageName);
        }

        internal override void ClearIndexes()
        {
            LanguageNames = null;
        }

        public Language GetByLanguageName(string lanname)
        {
            using (RepoLock.ReaderLock())
            {
                if (IsCached)
                    return LanguageNames.GetOne(lanname);
                return Table.FirstOrDefault(a=>a.LanguageName==lanname);
            }
        }

        public List<string> GetAllUniqueAudioLanguages()
        {
            List<int> langsid = Repo.Instance.CrossRef_Languages_AniDB_File.GetDistincLanguagesId();
            using (RepoLock.ReaderLock())
            {
                return WhereMany(langsid).Select(a => a.LanguageName).ToList();
            }
        }
        public List<string> GetAllUniqueSubtitleLanguages()
        {
            List<int> langsid = Repo.Instance.CrossRef_Subtitles_AniDB_File.GetDistincLanguagesId();
            using (RepoLock.ReaderLock())
            {
                return WhereMany(langsid).Select(a => a.LanguageName).ToList();
            }
        }
    }
}