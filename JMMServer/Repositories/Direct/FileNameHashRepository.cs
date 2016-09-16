using System.Collections.Generic;
using JMMServer.Entities;
using NHibernate.Criterion;

namespace JMMServer.Repositories.Direct
{
    public class FileNameHashRepository : BaseDirectRepository<FileNameHash, int>
    {

        public List<FileNameHash> GetByHash(string hash)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
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
            using (var session = JMMService.SessionFactory.OpenSession())
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
            using (var session = JMMService.SessionFactory.OpenSession())
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