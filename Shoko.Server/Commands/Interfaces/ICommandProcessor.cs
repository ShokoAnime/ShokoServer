using Shoko.Commons.Queue;
using Shoko.Models.Server;
using Shoko.Server.Server;

namespace Shoko.Server.Commands.Interfaces;

public interface ICommandProcessor
{
    int QueueCount { get; }
    QueueStateStruct QueueState { get; set; }
}
