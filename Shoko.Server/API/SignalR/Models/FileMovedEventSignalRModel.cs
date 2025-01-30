using Shoko.Plugin.Abstractions.Events;

namespace Shoko.Server.API.SignalR.Models;

public class FileMovedEventSignalRModel : FileEventSignalRModel
{
    public FileMovedEventSignalRModel(FileMovedEventArgs eventArgs) : base(eventArgs)
    {
        PreviousRelativePath = eventArgs.PreviousRelativePath;
        PreviousImportFolderID = eventArgs.PreviousImportFolder.ID;
    }

    /// <summary>
    /// The relative path of the old file from the import folder base location.
    /// </summary>
    public string PreviousRelativePath { get; }

    /// <summary>
    /// The ID of the old import folder the event was detected in.
    /// </summary>
    /// <value></value>
    public int PreviousImportFolderID { get; }
}
