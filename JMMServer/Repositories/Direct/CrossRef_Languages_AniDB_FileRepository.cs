using System.Collections.Generic;
using JMMServer.Entities;
using NHibernate.Criterion;

namespace JMMServer.Repositories.Direct
{
    public class CrossRef_Languages_AniDB_FileRepository : BaseDirectRepository<CrossRef_Languages_AniDB_File, int>
    {

        public List<CrossRef_Languages_AniDB_File> GetByFileID(int id)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                var files = session
                    .CreateCriteria(typeof(CrossRef_Languages_AniDB_File))
                    .Add(Restrictions.Eq("FileID", id))
                    .List<CrossRef_Languages_AniDB_File>();

                return new List<CrossRef_Languages_AniDB_File>(files);
            }
        }
   
    }
}