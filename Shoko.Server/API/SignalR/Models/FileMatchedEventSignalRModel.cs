using System.Collections.Generic;
using System.Linq;
using Shoko.Plugin.Abstractions;
using Shoko.Server.Models;

#nullable enable
namespace Shoko.Server.API.SignalR.Models;

public class FileMatchedEventSignalRModel : FileEventSignalRModel
{
    public FileMatchedEventSignalRModel(FileMatchedEventArgs eventArgs) : base(eventArgs) { }
}
