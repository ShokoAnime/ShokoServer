using System.Collections.Generic;
using JMMServer.Entities;
using NHibernate.Criterion;

namespace JMMServer.Repositories
{
    public class DuplicateFileRepository
    {
        public void Save(DuplicateFile obj)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                // populate the database
                using (var transaction = session.BeginTransaction())
                {
                    session.SaveOrUpdate(obj);
                    transaction.Commit();
                }
            }
        }

        public DuplicateFile GetByID(int id)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                return session.Get<DuplicateFile>(id);
            }
        }

        public List<DuplicateFile> GetByFilePathsAndImportFolder(string filePath1, string filePath2, int folderID1,
            int folderID2)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                var dfiles = session
                    .CreateCriteria(typeof(DuplicateFile))
                    .Add(Restrictions.Eq("FilePathFile1", filePath1))
                    .Add(Restrictions.Eq("FilePathFile2", filePath2))
                    .Add(Restrictions.Eq("ImportFolderIDFile1", folderID1))
                    .Add(Restrictions.Eq("ImportFolderIDFile2", folderID2))
                    .List<DuplicateFile>();
                return new List<DuplicateFile>(dfiles);
            }
        }

        public List<DuplicateFile> GetByImportFolder1(int folderID)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                var dfiles = session
                    .CreateCriteria(typeof(DuplicateFile))
                    .Add(Restrictions.Eq("ImportFolderIDFile1", folderID))
                    .List<DuplicateFile>();
                return new List<DuplicateFile>(dfiles);
            }
        }

        public List<DuplicateFile> GetByImportFolder2(int folderID)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                var dfiles = session
                    .CreateCriteria(typeof(DuplicateFile))
                    .Add(Restrictions.Eq("ImportFolderIDFile2", folderID))
                    .List<DuplicateFile>();
                return new List<DuplicateFile>(dfiles);
            }
        }


        public List<DuplicateFile> GetAll()
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                var objs = session
                    .CreateCriteria(typeof(DuplicateFile))
                    .List<DuplicateFile>();

                return new List<DuplicateFile>(objs);
            }
        }


        public void Delete(int id)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                // populate the database
                using (var transaction = session.BeginTransaction())
                {
                    var cr = GetByID(id);
                    if (cr != null)
                    {
                        session.Delete(cr);
                        transaction.Commit();
                    }
                }
            }
        }
    }
}