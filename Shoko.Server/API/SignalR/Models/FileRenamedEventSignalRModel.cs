using Shoko.Plugin.Abstractions;

namespace Shoko.Server.API.SignalR.Models;

public class FileRenamedEventSignalRModel : FileEventSignalRModel
{
    public FileRenamedEventSignalRModel(FileRenamedEventArgs eventArgs) : base(eventArgs)
    {
        NewFileName = eventArgs.NewFileName;
        OldFileName = eventArgs.OldFileName;
    }

    /// <summary>
    /// The new File name.
    /// </summary>
    public string NewFileName { get; set; }

    /// <summary>
    /// The old file name.
    /// </summary>
    public string OldFileName { get; set; }
}
