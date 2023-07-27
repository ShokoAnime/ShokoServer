using Shoko.Plugin.Abstractions;

namespace Shoko.Server.API.SignalR.Models;

public class FileMovedEventSignalRModel
{
    public FileMovedEventSignalRModel(FileMovedEventArgs eventArgs)
    {
        FileID = eventArgs.FileInfo.VideoFileID;
        NewRelativePath = eventArgs.NewRelativePath;
        NewImportFolderID = eventArgs.NewImportFolder.ImportFolderID;
        OldRelativePath = eventArgs.OldRelativePath;
        OldImportFolderID = eventArgs.OldImportFolder.ImportFolderID;
    }

    /// <summary>
    /// Shoko file id.
    /// </summary>
    public int FileID { get; set; }

    /// <summary>
    /// The relative path of the new file from the import folder base location.
    /// </summary>
    public string NewRelativePath { get; set; }

    /// <summary>
    /// The ID of the new import folder the event was detected in.
    /// </summary>
    /// <value></value>
    public int NewImportFolderID { get; set; }

    /// <summary>
    /// The relative path of the old file from the import folder base location.
    /// </summary>
    public string OldRelativePath { get; set; }

    /// <summary>
    /// The ID of the old import folder the event was detected in.
    /// </summary>
    /// <value></value>
    public int OldImportFolderID { get; set; }
}
