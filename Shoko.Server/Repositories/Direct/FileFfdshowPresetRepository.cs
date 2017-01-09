using Shoko.Models.Server;
using NHibernate.Criterion;
using Shoko.Server.Databases;
using Shoko.Server.Entities;

namespace Shoko.Server.Repositories.Direct
{
    public class FileFfdshowPresetRepository : BaseDirectRepository<FileFfdshowPreset, int>
    {
        private FileFfdshowPresetRepository()
        {
            
        }

        public static FileFfdshowPresetRepository Create()
        {
            return new FileFfdshowPresetRepository();
        }
        public FileFfdshowPreset GetByHashAndSize(string hash, long fsize)
        {
            using (var session = DatabaseFactory.SessionFactory.OpenSession())
            {
                FileFfdshowPreset obj = session
                    .CreateCriteria(typeof(FileFfdshowPreset))
                    .Add(Restrictions.Eq("Hash", hash))
                    .Add(Restrictions.Eq("FileSize", fsize))
                    .UniqueResult<FileFfdshowPreset>();

                return obj;
            }
        }
    }
}