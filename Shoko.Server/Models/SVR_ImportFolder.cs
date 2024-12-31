using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Serialization;
using Newtonsoft.Json;
using Shoko.Models.Server;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Server.Repositories;

namespace Shoko.Server.Models;

public class SVR_ImportFolder : ImportFolder, IImportFolder
{
    public new string ImportFolderLocation
    {
        get => base.ImportFolderLocation;
        set
        {
            var nvalue = value;
            if (nvalue != null)
            {
                if (nvalue.EndsWith(":"))
                {
                    nvalue += Path.DirectorySeparatorChar;
                }

                if (nvalue.Length > 0 && nvalue.EndsWith(Path.DirectorySeparatorChar.ToString()))
                {
                    while (nvalue.EndsWith(Path.DirectorySeparatorChar.ToString()))
                    {
                        nvalue = nvalue.Substring(0, nvalue.Length - 1);
                    }
                }

                nvalue += Path.DirectorySeparatorChar;
            }

            base.ImportFolderLocation = nvalue;
        }
    }

    [JsonIgnore, XmlIgnore]
    public DirectoryInfo BaseDirectory
    {
        get
        {
            if (!Directory.Exists(ImportFolderLocation))
            {
                throw new Exception("Import Folder not found '" + ImportFolderLocation + "'");
            }

            return new DirectoryInfo(ImportFolderLocation);
        }
    }

    [JsonIgnore, XmlIgnore]
    public bool FolderIsWatched => IsWatched == 1;

    [JsonIgnore, XmlIgnore]
    public bool FolderIsDropSource => IsDropSource == 1;

    [JsonIgnore, XmlIgnore]
    public bool FolderIsDropDestination => IsDropDestination == 1;

    [JsonIgnore, XmlIgnore]
    public long AvailableFreeSpace
    {
        get
        {
            var path = ImportFolderLocation;
            if (!Directory.Exists(path))
                return -1L;

            try
            {
                return new DriveInfo(path).AvailableFreeSpace;
            }
            catch
            {
                return -2L;
            }
        }
    }

    public override string ToString()
    {
        return string.Format("{0} - {1} ({2})", ImportFolderName, ImportFolderLocation, ImportFolderID);
    }

    public bool CanAcceptFile(IVideoFile file)
        => file is not null && (file.ImportFolderID == ImportFolderID || file.Size < AvailableFreeSpace);

    [JsonIgnore, XmlIgnore]
    public IReadOnlyList<SVR_VideoLocal_Place> Places
        => RepoFactory.VideoLocalPlace.GetByImportFolder(ImportFolderID);

    #region IImportFolder Implementation

    int IImportFolder.ID => ImportFolderID;

    string IImportFolder.Name => ImportFolderName;

    string IImportFolder.Path => ImportFolderLocation;

    DropFolderType IImportFolder.DropFolderType
    {
        get
        {
            var flags = DropFolderType.Excluded;
            if (IsDropSource == 1)
                flags |= DropFolderType.Source;
            if (IsDropDestination == 1)
                flags |= DropFolderType.Destination;
            return flags;
        }
    }

    #endregion
}
