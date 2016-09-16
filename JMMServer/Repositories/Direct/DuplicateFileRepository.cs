using System.Collections.Generic;
using JMMServer.Entities;
using NHibernate.Criterion;

namespace JMMServer.Repositories.Direct
{
    public class DuplicateFileRepository : BaseDirectRepository<DuplicateFile, int>
    {
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
    }
}