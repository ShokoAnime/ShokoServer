using System.Collections.Generic;
using Shoko.Models.Server;
using NHibernate.Criterion;
using Shoko.Server.Databases;
using Shoko.Server.Models;

namespace Shoko.Server.Repositories.Direct
{
    public class FileNameHashRepository : BaseDirectRepository<FileNameHash, int>
    {
        private FileNameHashRepository()
        {
        }

        public static FileNameHashRepository Create()
        {
            return new FileNameHashRepository();
        }

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