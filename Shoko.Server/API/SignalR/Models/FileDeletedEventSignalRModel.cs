using Shoko.Plugin.Abstractions;

namespace Shoko.Server.API.SignalR.Models;

public class FileDeletedEventSignalRModel : FileEventSignalRModel
{
    public FileDeletedEventSignalRModel(FileDeletedEventArgs eventArgs) : base(eventArgs) { }
}
