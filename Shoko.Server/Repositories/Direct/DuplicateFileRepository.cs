using System.Collections.Generic;
using System.Linq;
using Shoko.Models.Server;
using Shoko.Server.Databases;

namespace Shoko.Server.Repositories.Direct;

public class DuplicateFileRepository : BaseDirectRepository<DuplicateFile, int>
{
    public List<DuplicateFile> GetByFilePathsAndImportFolder(VideoLocal_Place place1, VideoLocal_Place place2)
        => GetByFilePathsAndImportFolder(place1.FilePath, place2.FilePath, place1.ImportFolderID, place2.ImportFolderID);

    public List<DuplicateFile> GetByFilePathsAndImportFolder(string filePath1, string filePath2, int folderID1,
        int folderID2)
    {
        return Lock(() =>
        {
            using var session = DatabaseFactory.SessionFactory.OpenSession();
            return session.Query<DuplicateFile>()
                .Where(a => a.FilePathFile1 == filePath1 && a.FilePathFile2 == filePath2 && a.ImportFolderIDFile1 == folderID1 &&
                            a.ImportFolderIDFile2 == folderID2).ToList();
        });
    }

    public List<DuplicateFile> GetByFilePathAndImportFolder(string filePath, int folderID)
    {
        return Lock(() =>
        {
            using var session = DatabaseFactory.SessionFactory.OpenSession();
            return session.Query<DuplicateFile>()
                .Where(a => a.FilePathFile1 == filePath && a.ImportFolderIDFile1 == folderID ||
                            a.FilePathFile2 == filePath && a.ImportFolderIDFile2 == folderID)
                .ToList();
        });
    }

    public List<DuplicateFile> GetByImportFolder1(int folderID)
    {
        return Lock(() =>
        {
            using var session = DatabaseFactory.SessionFactory.OpenSession();
            return session.Query<DuplicateFile>()
                .Where(a => a.ImportFolderIDFile1 == folderID).ToList();
        });
    }

    public List<DuplicateFile> GetByImportFolder2(int folderID)
    {
        return Lock(() =>
        {
            using var session = DatabaseFactory.SessionFactory.OpenSession();
            return session.Query<DuplicateFile>()
                .Where(a => a.ImportFolderIDFile2 == folderID)
                .ToList();
        });
    }
}
