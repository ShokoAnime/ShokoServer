using Shoko.Abstractions.Events;

#nullable enable
namespace Shoko.Server.API.SignalR.Models;

public class FileDetectedEventSignalRModel
{
    public FileDetectedEventSignalRModel(FileDetectedEventArgs eventArgs)
    {
        RelativePath = eventArgs.RelativePath;
        ManagedFolderID = eventArgs.ManagedFolder.ID;
    }

    /// <summary>
    /// The relative path of the file from the managed folder base location
    /// </summary>
    public string RelativePath { get; set; }

    /// <summary>
    /// The ID of the managed folder the event was detected in.
    /// </summary>
    /// <value></value>
    public int ManagedFolderID { get; set; }
}
