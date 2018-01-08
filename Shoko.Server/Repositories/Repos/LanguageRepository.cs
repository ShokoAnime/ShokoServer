using System.Linq;
using NutzCode.InMemoryIndex;
using Shoko.Models.Server;

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
            using (CacheLock.ReaderLock())
            {
                if (IsCached)
                    return LanguageNames.GetOne(lanname);
                return Table.FirstOrDefault(a=>a.LanguageName==lanname);
            }
        }
    }
}