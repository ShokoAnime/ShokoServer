using Shoko.Plugin.Abstractions.Events;

namespace Shoko.Server.API.SignalR.Models;

public class FileRelocatedEventSignalRModel : FileEventSignalRModel
{
    public FileRelocatedEventSignalRModel(FileRelocatedEventArgs eventArgs) : base(eventArgs)
    {
        Moved = eventArgs.Moved;
        Renamed = eventArgs.Renamed;
        PreviousRelativePath = eventArgs.PreviousRelativePath;
        PreviousManagedFolderID = eventArgs.PreviousManagedFolder.ID;
    }

    /// <summary>
    /// Whether or not the file was moved.
    /// </summary>
    public bool Moved { get; }

    /// <summary>
    /// Whether or not the file was renamed.
    /// </summary>
    public bool Renamed { get; }

    /// <summary>
    /// The relative path of the old file from the managed folder base location.
    /// </summary>
    public string PreviousRelativePath { get; }

    /// <summary>
    /// The ID of the old managed folder the event was detected in.
    /// </summary>
    /// <value></value>
    public int PreviousManagedFolderID { get; }
}
