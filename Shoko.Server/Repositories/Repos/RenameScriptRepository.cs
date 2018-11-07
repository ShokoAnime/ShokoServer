using System.Linq;
using Shoko.Models.Server;
using Shoko.Server.Repositories.ReaderWriterLockExtensions;
using Shoko.Server.Repositories.Cache;

namespace Shoko.Server.Repositories.Repos
{
    public class RenameScriptRepository : BaseRepository<RenameScript, int>
    {
        private PocoIndex<int, RenameScript, int> EnabledOnImport;
        private PocoIndex<int, RenameScript, string> ScriptNames;

        internal override int SelectKey(RenameScript entity) => entity.RenameScriptID;
            
        internal override void PopulateIndexes()
        {
            EnabledOnImport = new PocoIndex<int, RenameScript, int>(Cache, a => a.IsEnabledOnImport);
            ScriptNames = new PocoIndex<int, RenameScript, string>(Cache, a => a.ScriptName);
        }

        internal override void ClearIndexes()
        {
            EnabledOnImport = null;
            ScriptNames = null;
        }


        public RenameScript GetDefaultScript()
        {
            using (RepoLock.ReaderLock())
            {
                if (IsCached)
                    return EnabledOnImport.GetOne(1);
                return Table.FirstOrDefault(a => a.IsEnabledOnImport==1);
            }
        }

        public RenameScript GetDefaultOrFirst()
        {
            using (RepoLock.ReaderLock())
            {
                if (IsCached)
                    return EnabledOnImport.GetOne(1) ?? Cache.Values.FirstOrDefault();
                return Table.FirstOrDefault(a => a.IsEnabledOnImport == 1) ?? Table.FirstOrDefault();
            }
        }


        public RenameScript GetByName(string scriptName)
        {
            using (RepoLock.ReaderLock())
            {
                if (IsCached)
                    return ScriptNames.GetOne(scriptName);
                return Table.FirstOrDefault(a => a.ScriptName==scriptName);
            }
        }
    }
}