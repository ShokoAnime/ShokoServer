using System.Collections.Generic;
using Shoko.Server.Entities;
using Shoko.Models.Server;
using NHibernate.Criterion;
using Shoko.Server.Databases;

namespace Shoko.Server.Repositories.Direct
{
    public class CrossRef_Subtitles_AniDB_FileRepository : BaseDirectRepository<CrossRef_Subtitles_AniDB_File, int>
    {
        private CrossRef_Subtitles_AniDB_FileRepository()
        {
            
        }

        public static CrossRef_Subtitles_AniDB_FileRepository Create()
        {
            return new CrossRef_Subtitles_AniDB_FileRepository();
        }
        public List<CrossRef_Subtitles_AniDB_File> GetByFileID(int id)
        {
            using (var session = DatabaseFactory.SessionFactory.OpenSession())
            {
                var files = session
                    .CreateCriteria(typeof(CrossRef_Subtitles_AniDB_File))
                    .Add(Restrictions.Eq("FileID", id))
                    .List<CrossRef_Subtitles_AniDB_File>();

                return new List<CrossRef_Subtitles_AniDB_File>(files);
            }
        }
    }
}