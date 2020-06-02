using System.Collections.Generic;
using NHibernate.Criterion;
using Shoko.Models.Server;
using Shoko.Server.Databases;

namespace Shoko.Server.Repositories.Direct
{
    public class FileNameHashRepository : BaseDirectRepository<FileNameHash, int>
    {
        public List<FileNameHash> GetByHash(string hash)
        {
            using (var session = DatabaseFactory.SessionFactory.OpenSession())
            {
                var xrefs = session
                    .CreateCriteria(typeof(FileNameHash))
                    .Add(Restrictions.Eq("Hash", hash))
                    .List<FileNameHash>();

                return new List<FileNameHash>(xrefs);
            }
        }

        public List<FileNameHash> GetByFileNameAndSize(string filename, long filesize)
        {
            using (var session = DatabaseFactory.SessionFactory.OpenSession())
            {
                var fnhashes = session
                    .CreateCriteria(typeof(FileNameHash))
                    .Add(Restrictions.Eq("FileName", filename))
                    .Add(Restrictions.Eq("FileSize", filesize))
                    .List<FileNameHash>();

                return new List<FileNameHash>(fnhashes);
            }
        }

        public FileNameHash GetByNameSizeAndHash(string filename, long filesize, string hash)
        {
            using (var session = DatabaseFactory.SessionFactory.OpenSession())
            {
                FileNameHash fnhash = session
                    .CreateCriteria(typeof(FileNameHash))
                    .Add(Restrictions.Eq("Hash", hash))
                    .Add(Restrictions.Eq("FileName", filename))
                    .Add(Restrictions.Eq("FileSize", filesize))
                    .UniqueResult<FileNameHash>();

                return fnhash;
            }
        }
    }
}