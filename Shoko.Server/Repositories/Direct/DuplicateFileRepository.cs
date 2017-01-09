using System.Collections.Generic;
using Shoko.Models.Server;
using NHibernate.Criterion;
using Shoko.Server.Databases;
using Shoko.Server.Entities;

namespace Shoko.Server.Repositories.Direct
{
    public class DuplicateFileRepository : BaseDirectRepository<SVR_DuplicateFile, int>
    {
        private DuplicateFileRepository()
        {
            
        }

        public static DuplicateFileRepository Create()
        {
            return new DuplicateFileRepository();
        }
        public List<SVR_DuplicateFile> GetByFilePathsAndImportFolder(string filePath1, string filePath2, int folderID1,
            int folderID2)
        {
            using (var session = DatabaseFactory.SessionFactory.OpenSession())
            {
                var dfiles = session
                    .CreateCriteria(typeof(SVR_DuplicateFile))
                    .Add(Restrictions.Eq("FilePathFile1", filePath1))
                    .Add(Restrictions.Eq("FilePathFile2", filePath2))
                    .Add(Restrictions.Eq("ImportFolderIDFile1", folderID1))
                    .Add(Restrictions.Eq("ImportFolderIDFile2", folderID2))
                    .List<SVR_DuplicateFile>();
                return new List<SVR_DuplicateFile>(dfiles);
            }
        }

        public List<SVR_DuplicateFile> GetByImportFolder1(int folderID)
        {
            using (var session = DatabaseFactory.SessionFactory.OpenSession())
            {
                var dfiles = session
                    .CreateCriteria(typeof(SVR_DuplicateFile))
                    .Add(Restrictions.Eq("ImportFolderIDFile1", folderID))
                    .List<SVR_DuplicateFile>();
                return new List<SVR_DuplicateFile>(dfiles);
            }
        }

        public List<SVR_DuplicateFile> GetByImportFolder2(int folderID)
        {
            using (var session = DatabaseFactory.SessionFactory.OpenSession())
            {
                var dfiles = session
                    .CreateCriteria(typeof(SVR_DuplicateFile))
                    .Add(Restrictions.Eq("ImportFolderIDFile2", folderID))
                    .List<SVR_DuplicateFile>();
                return new List<SVR_DuplicateFile>(dfiles);
            }
        }
    }
}