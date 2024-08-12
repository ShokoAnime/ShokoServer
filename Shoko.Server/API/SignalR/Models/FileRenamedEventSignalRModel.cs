using Shoko.Plugin.Abstractions;
using Shoko.Plugin.Abstractions.Events;

namespace Shoko.Server.API.SignalR.Models;

public class FileRenamedEventSignalRModel : FileEventSignalRModel
{
    public FileRenamedEventSignalRModel(FileRenamedEventArgs eventArgs) : base(eventArgs)
    {
        FileName = eventArgs.FileName;
        PreviousFileName = eventArgs.PreviousFileName;
    }

    /// <summary>
    /// The new File name.
    /// </summary>
    public string FileName { get; }

    /// <summary>
    /// The old file name.
    /// </summary>
    public string PreviousFileName { get; }
}
