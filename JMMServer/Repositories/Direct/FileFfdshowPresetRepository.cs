using JMMServer.Entities;
using NHibernate.Criterion;

namespace JMMServer.Repositories.Direct
{
    public class FileFfdshowPresetRepository : BaseDirectRepository<FileFfdshowPreset, int>
    {
        public FileFfdshowPreset GetByHashAndSize(string hash, long fsize)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
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