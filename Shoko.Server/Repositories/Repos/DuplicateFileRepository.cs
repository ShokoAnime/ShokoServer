using System.Collections.Generic;
using System.Linq;
using NutzCode.InMemoryIndex;
using Shoko.Models.Server;

namespace Shoko.Server.Repositories.Repos
{
    public class DuplicateFileRepository : BaseRepository<DuplicateFile, int>
    {
        private PocoIndex<int, DuplicateFile, string, int> FileImport1;
        private PocoIndex<int, DuplicateFile, string, int> FileImport2;
        private PocoIndex<int, DuplicateFile, int> Folder1;
        private PocoIndex<int, DuplicateFile, int> Folder2;

        internal override int SelectKey(DuplicateFile entity) => entity.DuplicateFileID;


        internal override void PopulateIndexes()
        {
            FileImport1 = new PocoIndex<int, DuplicateFile, string, int>(Cache, a => a.FilePathFile1, a => a.ImportFolderIDFile1);
            FileImport2 = new PocoIndex<int, DuplicateFile, string, int>(Cache, a => a.FilePathFile2, a => a.ImportFolderIDFile2);
            Folder1 = new PocoIndex<int, DuplicateFile, int>(Cache, a => a.ImportFolderIDFile1);
            Folder2 = new PocoIndex<int, DuplicateFile, int>(Cache, a => a.ImportFolderIDFile2);
        }
        internal override void ClearIndexes()
        {
            FileImport1 = null;
            FileImport2 = null;
            Folder1 = null;
            Folder2 = null;
        }

        public List<DuplicateFile> GetByFilePathsAndImportFolder(string filePath1, string filePath2, int folderID1,
            int folderID2)
        {
            using (CacheLock.ReaderLock())
            {
                if (IsCached)
                    return FileImport1.GetMultiple(filePath1, folderID1).Where(a=>a.FilePathFile2==filePath2 && a.ImportFolderIDFile2==folderID2).ToList();
                return Table.Where(a => a.FilePathFile1 == filePath1 && a.ImportFolderIDFile1==folderID1 && a.FilePathFile2 == filePath2 && a.ImportFolderIDFile2 == folderID2).ToList();
            }
        }

        public List<DuplicateFile> GetByFilePathAndImportFolder(string filePath, int folderID)
        {
            using (CacheLock.ReaderLock())
            {
                if (IsCached)
                    return FileImport1.GetMultiple(filePath, folderID).Union(FileImport2.GetMultiple(filePath,folderID)).ToList();
                return Table.Where(a =>
                    (a.FilePathFile1 == filePath && a.ImportFolderIDFile1 == folderID) ||
                    (a.FilePathFile2 == filePath && a.ImportFolderIDFile2 == folderID)).ToList();
            }
        }

        public List<DuplicateFile> GetByImportFolder1(int folderID)
        {
            using (CacheLock.ReaderLock())
            {
                if (IsCached)
                    return Folder1.GetMultiple(folderID);
                return Table.Where(a => a.ImportFolderIDFile1==folderID).ToList();
            }
        }

        public List<DuplicateFile> GetByImportFolder2(int folderID)
        {
            using (CacheLock.ReaderLock())
            {
                if (IsCached)
                    return Folder2.GetMultiple(folderID);
                return Table.Where(a => a.ImportFolderIDFile2 == folderID).ToList();
            }
        }
    }
}