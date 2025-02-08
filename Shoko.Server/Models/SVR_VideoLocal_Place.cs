using System;
using System.IO;
using Shoko.Models.Server;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Server.Repositories;

#nullable enable
namespace Shoko.Server.Models;

public class SVR_VideoLocal_Place : VideoLocal_Place, IVideoFile
{
    internal SVR_ImportFolder? ImportFolder => RepoFactory.ImportFolder.GetByID(ImportFolderID);

    public string? FullServerPath
    {
        get
        {
            var importFolderLocation = ImportFolder?.ImportFolderLocation;
            if (string.IsNullOrEmpty(importFolderLocation) || string.IsNullOrEmpty(FilePath))
                return null;

            return Path.Join(importFolderLocation, FilePath);
        }
    }

    public string FileName => Path.GetFileName(FilePath);

    public SVR_VideoLocal? VideoLocal => VideoLocalID is 0 ? null : RepoFactory.VideoLocal.GetByID(VideoLocalID);

    public FileInfo? GetFile()
    {
        if (!File.Exists(FullServerPath))
        {
            return null;
        }

        return new FileInfo(FullServerPath);
    }

    #region IVideoFile Implementation

    int IVideoFile.ID => VideoLocal_Place_ID;

    int IVideoFile.VideoID => VideoLocalID;

    bool IVideoFile.IsAvailable => File.Exists(FullServerPath);

    IVideo IVideoFile.Video => VideoLocal
        ?? throw new NullReferenceException("Unable to get the associated IVideo for the IVideoFile with ID " + VideoLocal_Place_ID);

    string IVideoFile.Path => FullServerPath
        ?? throw new NullReferenceException("Unable to get the absolute path for the IVideoFile with ID " + VideoLocal_Place_ID);

    string IVideoFile.RelativePath
    {
        get
        {
            var path = FilePath.Replace('\\', '/');
            // Windows compat. home/folder -> /home/folder, but not C:/folder -> /C:/folder
            if (path.Length > 0 && path[0] != '/' && (path.Length < 2 || path[1] != ':'))
                path = '/' + path;
            return path;
        }
    }

    long IVideoFile.Size => VideoLocal?.FileSize ?? 0;

    IImportFolder IVideoFile.ImportFolder => ImportFolder
        ?? throw new NullReferenceException("Unable to get the associated IImportFolder for the IVideoFile with ID " + VideoLocal_Place_ID);

    Stream? IVideoFile.GetStream()
    {
        var filePath = FullServerPath;
        if (string.IsNullOrEmpty(filePath))
            return null;

        if (!File.Exists(filePath))
            return null;

        return File.OpenRead(filePath);
    }

    #endregion
}
