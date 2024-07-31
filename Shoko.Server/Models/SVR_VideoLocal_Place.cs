using System.IO;
using Shoko.Models.Server;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Server.Repositories;

namespace Shoko.Server.Models;

public class SVR_VideoLocal_Place : VideoLocal_Place, IVideoFile
{
    internal SVR_ImportFolder ImportFolder => RepoFactory.ImportFolder.GetByID(ImportFolderID);

    public string FullServerPath
    {
        get
        {
            if (string.IsNullOrEmpty(ImportFolder?.ImportFolderLocation) || string.IsNullOrEmpty(FilePath))
            {
                return null;
            }

            return Path.Combine(ImportFolder.ImportFolderLocation, FilePath);
        }
    }

    public string FileName => Path.GetFileName(FilePath);

    public SVR_VideoLocal VideoLocal => VideoLocalID == 0 ? null : RepoFactory.VideoLocal.GetByID(VideoLocalID);

    public FileInfo GetFile()
    {
        if (!File.Exists(FullServerPath))
        {
            return null;
        }

        return new FileInfo(FullServerPath);
    }

    #region IVideoFile Implementation

    int IVideoFile.ID => VideoLocal_Place_ID;

    int IVideoFile.ImportFolderID => ImportFolderID;

    int IVideoFile.VideoID => VideoLocalID;

    IVideo IVideoFile.Video => VideoLocal;

    string IVideoFile.Path => FullServerPath;

    string IVideoFile.RelativePath
    {
        get
        {
            var path = FilePath.Replace('\\', '/');
            if (path.Length > 0 && path[0] != '/')
                path = '/' + path;
            return path;
        }
    }

    long IVideoFile.Size => VideoLocal?.FileSize ?? 0;

    IImportFolder IVideoFile.ImportFolder => ImportFolder;

    public IHashes Hashes => VideoLocal;

    public IMediaContainer MediaInfo => VideoLocal?.MediaInfo;

    #endregion
}
