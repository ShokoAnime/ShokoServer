using System.Collections.Generic;
using System.Linq;
using Shoko.Models.Server;
using NHibernate.Criterion;
using Shoko.Server.Databases;
using Shoko.Server.Models;

namespace Shoko.Server.Repositories.Direct
{
    public class DuplicateFileRepository : BaseDirectRepository<DuplicateFile, int>
    {
        private DuplicateFileRepository()
        {
        }

        public static DuplicateFileRepository Create()
        {
            return new DuplicateFileRepository();
        }

        public List<DuplicateFile> GetByFilePathsAndImportFolder(string filePath1, string filePath2, int folderID1,
            int folderID2)
        {
            using (var session = DatabaseFactory.SessionFactory.OpenSession())
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

        public List<DuplicateFile> GetByFilePathAndImportFolder(string filePath, int folderID)
        {
            using (var session = DatabaseFactory.SessionFactory.OpenSession())
            {
                var dfiles = session
                    .CreateSQLQuery(
                        $"SELECT * FROM DuplicateFile WHERE (FilePathFile1 = :filePath OR FilePathFile2 = :filePath) AND (ImportFolderIDFile1 = :folderID OR ImportFolderIDFile2 = :folderID)")
                    .SetString("filePath", filePath)
                    .SetInt32("folderID", folderID)
                    .List<DuplicateFile>();
                return dfiles.ToList();
            }
        }

        public List<DuplicateFile> GetByImportFolder1(int folderID)
        {
            using (var session = DatabaseFactory.SessionFactory.OpenSession())
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
            using (var session = DatabaseFactory.SessionFactory.OpenSession())
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