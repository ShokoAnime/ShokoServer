using Shoko.Plugin.Abstractions;

namespace Shoko.Server.API.SignalR.Models;

public class FileEventSignalRModel
{
    public FileEventSignalRModel(FileEventArgs eventArgs)
    {
        RelativePath = eventArgs.RelativePath;
        FileID = eventArgs.FileInfo.VideoFileID;
        ImportFolderID = eventArgs.ImportFolder.ImportFolderID;
    }

    /// <summary>
    /// The relative path of the file from the import folder base location
    /// </summary>
    public string RelativePath { get; set; }

    /// <summary>
    /// Shoko file id.
    /// </summary>
    public int FileID { get; set; }

    /// <summary>
    /// The ID of the import folder the event was detected in.
    /// </summary>
    /// <value></value>
    public int ImportFolderID { get; set; }
}
