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

    int IVideoFile.VideoFileID => VideoLocalID;
    string IVideoFile.Filename => Path.GetFileName(FilePath);
    string IVideoFile.FilePath => FullServerPath;
    long IVideoFile.FileSize => VideoLocal?.FileSize ?? 0;
    public IAniDBFile AniDBFileInfo => VideoLocal?.GetAniDBFile();

    public IHashes Hashes => VideoLocal == null
        ? null
        : new VideoHashes
        {
            CRC = VideoLocal.CRC32, MD5 = VideoLocal.MD5, ED2K = VideoLocal.Hash, SHA1 = VideoLocal.SHA1
        };

    public IMediaContainer MediaInfo => VideoLocal?.Media;

    private class VideoHashes : IHashes
    {
        public string CRC { get; set; }
        public string MD5 { get; set; }
        public string ED2K { get; set; }
        public string SHA1 { get; set; }
    }
}
