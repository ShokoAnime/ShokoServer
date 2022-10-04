using Shoko.Plugin.Abstractions;

namespace Shoko.Server.API.SignalR.Models;

public class FileHashedEventSignalRModel : FileEventSignalRModel
{
    public FileHashedEventSignalRModel(FileHashedEventArgs eventArgs) : base(eventArgs)
    {
    }
}
